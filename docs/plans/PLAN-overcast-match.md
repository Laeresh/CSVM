# Overcast match — the C1 IA1 sky as one picture

✅ **COMPLETE 2026-08-09** (written 2026-08-08). All 21 items landed; archived in `docs/plans/`
and indexed in [`plans.md`](plans.md). **`PT-47` is owed at the controls** — the plan's own exit
verdict, flying what these tables could only photograph ([`playtest.md`](../../playtest.md)).
Closed with it: `BL-118`, `BL-312`, `BL-303`, `BL-100`, `BL-101`. Minted on the way and still open:
`BL-320`, `BL-321`, `BL-322`, `BL-325`, `BL-327`. Also minted and since closed: `BL-323` — the C4
"terrain-mesh spike" at `-4974,-3861` was never a defect; the pose sits *under* the ground and the
spike is a mountain (`git log --grep=BL-323`).

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
  ([`fogvol.md`](../formats/fogvol.md) has the full 8-chapter census).
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
17. ☑ B17 — Fog A/B at both reference poses + BL-303's three; close `BL-303`/`BL-100`/`BL-101`
18. ☑ B18 — The dome's cap/skirt seam: the skirt is painted `FOG_COLOR`, and we were applying it twice **Wave B complete.**

### Wave C — deck mesh brightness (BL-118 core)

21. ☑ C21 — Find the original's underside mechanism: the deck is the one world surface the
    original dims by the mission's SUNLIGHT and we do not (`lighting: false` gates it off);
    210.5 × 0.802 = 168.9 vs the original's 167.7. Also: the ceiling's fade is ordinary authored
    fog (the fog-exempt/baked-fade lead is refuted, C25) and `DeckCeilingHeight` re-estimates to
    **135 m**, not 400 — needs a user verdict, see the section
22. ☑ C22 — Implement the underside darkening
23. ☑ C23 — Re-measure the tops per population; blend the mesh↔sprite cut — **measurement +
    four disproofs, no engine code**: the tops boxes are 65–97 % sprite (the deck bleeds 3–35 %
    through card alpha, which the flat mask could not see), our card plateau **222.7** is faithful
    to the data while the original's is **209**, and the cut has two independent causes —
    `A7`'s above-band floor sits *inside* the card band, and `C22`'s dimming put that floor 54
    units under the cards. **Fork taken by the user 2026-08-09 and LANDED the same day: `M-a`** —
    the deck's `WorldLight` dimming became regime-conditional (undimmed above the band, a
    per-instance mesh swap at `A7`'s own flip) and the `fvol` cards took a `225/240` vertex-colour
    TUNE onto the original's measured 209 plateau. Card `p90` 222.7 → **209.2**, floor↔card gap
    **49 → −4**, the sub-`FOG_COLOR` tail gone (outboard `p1` 166.2 → **176.0**); every below-band
    frame and all 13 goldens bit-identical. ⚠ Two residuals for `C24`: the 1700 m far field
    (`tops-L`/`tops-R` still +15/+29) and C1C/C2B's three-tone split, which M-a **widens**.
24. ☑ C24 — Final match: both stills within the bar; mint PT + night-moonlit BL; close `BL-118` —
    **every comparable region inside ±10 on both reference stills**, per population: the river
    ceiling +6.5/+5.6/+0.5 and its terrain −3.3/−3.1, with the fog saturating at elevation **20 px
    against the original's 16** (`B17` read 64 before Wave C); the above-deck dome +3.4…+5.4, near
    sheet +0.5/−3.4, card plateau **209.24 against 209**, floor↔card **−4.11**, sub-`FOG_COLOR`
    tail **gone** (0.000 % below 176). Three rows are ⚠ *not comparable* — the original's terrain
    silhouette sits 50 px lower and its above-deck pitch differs (`SHOT-23`), with the sd evidence
    in the tables — and **one row fails with its item**: the original's above-deck far field
    carries 74 dead-flat `FOG_COLOR` rows and ours carries 0 (`BL-327`). Six goldens re-pinned,
    `.\RunTests.ps1` **exit 0**; `BL-118` deleted, `BL-325`/`BL-327`/`PT-47` minted.
    **Wave C complete; plan complete.**
25. ☑ C25 — The below-band ceiling covers to the horizon and fades like the original's — landed as
    a `K` correction (400 → 135 m); `C21`'s refutation meant no extension and no baked fade were
    needed at all
26. ☑ C26 — The last strip: the dome wall's base must meet the fog wall seamlessly — traced first
    (the wall's base ring IS `FOG_COLOR` to the decimal; the strip is the wall's own authored
    gradient over the rim the deck sheet leaves open, not a defect), then **landed as the fork's
    own recommendation**: the below-band ceiling's half-span extends 6144 m → 20,480 m via a
    fog-saturated untextured annulus around the 144 tiles (`WorldBuilder.AddDeckAnnulus`), moving
    the rim 13 px → ~4 px where the wall's own gradient is near-invisible. `K` untouched, deck
    census still 144; goldens' standing six all still move (`C22`/`C25`'s own cause), and this
    item's own contribution moves five of the six further (only `c1-waterfall`'s pose misses the
    strip) — no golden outside that set moved, `0 broken` of 13.

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
hiding the deck's brightness — so C21/C22 own the river pose's remaining fog symptom too. Inside Wave C — **amended 2026-08-08 (C25 minted from the user's B17-screenshot observation)**:
C21 blocks everything (its research pass now also owns the ceiling's fade mechanism and a
re-estimate of A7's `DeckCeilingHeight` 400 m from the fogged river still — B15 flagged it as
possibly ~3× too large); then C25 (ceiling extent + fade treatment — the river-pose luminance
can only be judged through it); then C22 (underside brightness); C22 before C23 (the cut can
only be judged with the underside fixed); C24 last.

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
   [`clutter.md`](../formats/clutter.md)) and its decoration offsets are **authored explicitly, one
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
not the mission selector. Hand that to `B14`; do not re-derive the sky zone from it.
(Landed since: `fog_zone` is a bool arming the in-volume whiteout and camera state 3 —
`PLAN-weather-decompile-match` `A2`/`C21`/`C22`, docs/formats/fogvol.md.)

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
render behaviour, and it is what makes the collapse lethal. **Decided since** (`PLAN-weather-decompile-match`
`C22`): the remake does not clip, it fogs — B11's own kept divergence, carried here deliberately.

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
interior-fog keys; the render feature was held separately, B11 owns whether zones and interior fog
are one mechanism. **Both landed** — they ARE one mechanism: `fog_zone` arms camera state 3, the
volume curtain is `C21` and `ZONE3`'s fog is `C22` (`PLAN-weather-decompile-match`). Note C1's `fvol` nodes carry `zone_id: 2` — check the sign of that
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

## B17 ☑ Fog A/B at both reference poses + BL-303's three; close BL-303/BL-100/BL-101

**Landed.** (2026-08-08) Measurement and bookkeeping only — no engine code. The final Wave B build
(post `B11`–`B16`, `B18`, `B13`, `B14`, `B15` — that is this wave's actual commit order, `B15`
landing last) was re-shot at all ten poses this item needs (`.scratch/b17/shoot.ps1` +
`measure.py`, reusing `B15`'s `boxes.py`). Nine of the ten are **MD5-identical** to `B15`'s own
`landed-*.png`, confirming the build has not moved since `B15` landed and that this item's fresh
renders and `B15`'s own numbers describe the same current state; `c2babovedeck` is the one pose
`B15` never shot. `.\RunTests.ps1` moved exactly the 11 goldens `B15`'s own record predicted, 0
broken, 0 unexpected — re-pinned, and a clean run now exits 0 (BL-320's viewer-bhawk hang worked
around per its own note: killing the one hung Godot process let the suite finish normally).

### Table 1 — river pose (`-7323,192,-3829` / `-0.997,-0.1,0.070`) vs `OriginalScreenshots/C1 IA1 Fog river.png`

| region | ours | original | Δ | verdict |
|---|---|---|---|---|
| sky (mid-sky gradient) | 180.6 | 169.4 | +11.2 | borderline — waits on Wave C (below) |
| near valley (river + grass) | 58.8 | 62.2 | −3.3 | ✓ within ±10 |
| mid terrain (horizon band, cliff/ridge) | 71.5 | 175.0 | −103.4 | ✗ waits on Wave C |

Plus the established reach instrument (`SHOT-23`, `B15`'s own, re-confirmed here on a
bit-identical frame): mottling dies at row 236 (elevation **64 px** above the true horizon,
saturation distance ≈3.7 km) against the original's row 337 (elevation **19 px**, ≈12.6 km).

**Population note (`SHOT-21`):** no `cloudsprite` billboard is visible anywhere in this frame —
`A3`'s top-anchoring keeps every card ≥986 m, far above both the 192 m camera and the
terrain-level view here. Every pixel measured is the `CloudDeck` **mesh** (seen from below) or
bare terrain; the sprite population plays no part at this pose.

**Both misses are the same, already-diagnosed residual, not a fresh fog defect.** `B15` measured
it directly: the original's overcast ceiling is *already at the fog colour* (row-mean 166.5→175.0
down the original's sky, against a 176 `FOG_COLOR`), while ours renders the same ceiling at
200–220 unfogged — so no combination of range factor or ramp shape can close the gap, because the
fog has nothing washed-out to hide the difference behind. `mid terrain` lands squarely on this
ceiling-vs-terrain handover and reads the full 103-unit gap; `sky`'s milder +11.2 is the same
mechanism sampled higher in the gradient, where the two images already sit closer together. This
is `BL-118`/Wave C's own underside-brightness delta (**167.7 original vs 213.9 ours, +46**,
`C21`), not this item's fog mechanism — attributed there, not logged as a fog failure.

### Table 2 — above-deck pose (`-7323,1192,-3829` / `0,0,-1`) vs `OriginalScreenshots/C1 IA1 Fog above clouddeck.png`

| region | ours | original | Δ | verdict |
|---|---|---|---|---|
| zenith / dome (apex) | 72.8 (B17) / 75.5 (`B12`/`B16`, same pose) | 69.4 (B17) / 74.3 (`B12`) | +3.5 / +1.2 | ✓ within ±10 |
| horizon / gray band (`B16`'s rows 265–355) | mean 153.3, sd 30.71 (textured) | — (qualitative) | — | ✓ — the flat sd-5.92 band `B16` found is gone in both; both images now show a real mottled gradient into the cloud tops |
| deck tops / plateau (sprite population) | 221.2 | 157.2 | +64.1 | ✗ waits on Wave C |

**Population note (`SHOT-21`):** the plateau this pose looks down onto **is** the top-anchored
`cloudsprite` field (the fvol slab tops out at 1090.55 m, 101 m below the 1192 m camera) — this is
`C21`–`C23`'s own "tops from above" target (`CAP-12` already has this population reading 211/214
ours vs 196/202 original at a different altitude), a sprite-population brightness/character
question the plan hands to Wave C, not a fog miss. The zenith and horizon-band regions, which
involve no deck content at all, both hold.

### Table 3 — `BL-303`'s three poses

**C3 canyon** (`-3504,710,-3619` / `-0.40673,0,-0.91355`) vs
`playtest/CAP-11/t0.5-c3-spawn-canyon.png`. Numbers are `B15`'s own landed record, re-confirmed
current here by the bit-identical rebuild check: `B17`'s own box replay against the ORIGINAL still
reproduced `near slope`/`mid ridge` within ~1 unit but landed nowhere near `far ridge`/`sky`
(14.2/180.1 against the recorded 60.3/194.9) — exactly `B15`'s own caveat for this pose, that a
fixed fractional box lands on different terrain on the two sides because the CAP-11 original
carries HUD/plane on a chase-cam pitch our matched freecam shot doesn't share. `B15`'s
framing-robust numbers are cited below rather than a fresh, noisier re-derivation:

| region | ours | original | Δ | verdict |
|---|---|---|---|---|
| sky | 202.9 / 198.1 blue-gradient (`B16`) | 194.9 blue-gradient, B=221 | ≈+4–8 | ✓ — dome fixed by `B16` |
| near slope | 106.1 | 36.5 | +69.6 | ✗ still open |
| mid ridge | 143.7 | 19.9 | +123.8 | ✗ still open |
| far ridge | 182.4 | 60.3 | +122.1 | ✗ still open |
| vegetation fraction (lower frame, `G−R>8`) | 22.1 % | 47.8 % | −25.7 pp | ✗ still open |

**C2B above-deck** (`-3843,1500,-1101` / `-0.391,0,-0.921`) vs
`playtest/CAP-11/t50-c2b-above-deck.png` — a fresh `B17` shot, not in `B15`'s set:

| region | ours | original | Δ | verdict |
|---|---|---|---|---|
| sky/dome | 75.5 | 82.0 (B17) / 79.5–82.7 (CAP-11's clean-window citation) | −6.5 to −7.2 | ✓ within ±10 — matches `B16`'s own landed 75.2 (unaffected by `B15`'s range/curve change, since the dome doesn't fog at all) |

**C5 night** (`-9187,90,-2037` / `-0.1219,0,-0.9925`) vs
`playtest/CAP-11/t0.5-c5-spawn-night-city.png`:

| region | ours | original | Δ | verdict |
|---|---|---|---|---|
| sky (clear patch) | 16.6 | 15.3 (`BL-303`/CAP-11) / 14.1 (B17 box) | +1.3 / +2.5 | ✓ within ±10 |
| street/facades (adjunct, not this item's mechanism) | lit facades: tower 10.2, low-rise 21.7 (CAP-11) | tower 15.5, low-rise 37.6 (CAP-11) | ×0.58–0.66 | still open — flagged NOT-fog by `BL-303` itself (WorldLight already at clamp 1.0); untouched here per the item's own trap |

**Verdict: two of `BL-303`'s three scenes are fully inside the bar (C2B, C5); C3's sky is inside
the bar and C3's terrain murk is not.** The sky half of all three was `B16`'s fix; C3's terrain
residual and C5's facade adjunct are named below, not fixed here.

### Residuals the orchestrator must mint follow-up items for

Per this item's own instruction, neither residual is minted here — both are named with their exact
evidence so a fresh `BL` can cite it directly:

1. **C3's near-slope terrain murk — 106.1 measured against the original's 36.5**, and the same
   shape on `mid ridge` (143.7/19.9) and `far ridge` (182.4/60.3). `B15` tried the full
   `{factor, curve} × 2×2` and every cell is bounded away from the target — the fog is not
   calibrated wrong here, it is modeled wrong — and named two untried candidates in its own
   landing record, neither implemented:
   - *"The fog mix happens in LINEAR space; the DX7 chain blended in FRAMEBUFFER (gamma) space...
     At half fog between a dark slope (40 sRGB) and 176 the two spaces differ by 21 units — gamma
     108, linear 129 — with the linear result always the washier one... This is the first thing to
     try."* (`B15`, "What this does NOT fix")
   - *"A7's `f·h ≈ 2.4e5` (hence `DeckCeilingHeight` 400 m) may be ~3× too large... the fogged
     still now offers a second, independent estimator of the same product, which A7 did not
     have."* (`B15`, same section)
2. **C5's lit-facade dimming — tower faces 10.2 vs 15.5 (×0.66), low-rise 21.7 vs 37.6 (×0.58),
   WorldLight already at clamp 1.0.** `BL-303`'s own text: *"Adjacent from the same capture,
   probably NOT fog: C5's lit facades read ×0.58–0.66 of the original... with WorldLight already
   at clamp 1."* Unrelated to any fog/dome/scatter mechanism this plan touched; needs its own
   investigation.

### Table 4 — healthy-scene regression, final build

Re-confirmed via the bit-identical rebuild check rather than re-derived: `B15`'s own table is
still current, since nothing has rendered differently since it was measured (`B16`/`B18`/`B13`/
`B14` all landed *before* `B15` in this wave's actual commit order).

| pose | region | ours | original | note |
|---|---|---|---|---|
| C2B spawn | sky | 169.9 | 177.0 | ✓ |
| C2B spawn | ocean near | 41.0 (B15) / 40.9 (B17) | 52.1 (B15) / 52.0 (B17) | open — `BL-304` water/WorldLight exemption, untouched, out of this plan's scope |
| C1B spawn | island/sea | 23.4 | 26.8 / 27.4 | ✓ |
| C1B spawn | moonlit cloud tops p90 | 187.5 | 155.4 (t5) / 181.9 (t16) | residual noted on `BL-118`, out of scope here |
| C2 `dogfight_ace[4]` | ground near | 105.7 | 89.1 / 88.0 | pre-existing gap, out of this plan's three-defect scope |
| C2 `dogfight_ace[4]` | ground mid | 109.4 | ≈88 | pre-existing gap, out of scope |
| C5 `dogfight_ace[3]` | street | 30.2 | — | unchanged |
| C5 `dogfight_ace[3]` | sky | 16.6 | 15.3 | ✓ |
| C4 `-4974,300,-3861` | every box | 191.5–191.9 | — | degenerate instrument (`INSTR-7`): C4's snow terrain and 192-gray fog are near-isomorphic here, unmeasurable at this pose |
| C1 `CAP-12` t8.0 | near ground | 74.5 (B15) / 74.9 (B17) | 108.2 (B15) / 115.3 (B17) | pre-existing gap, out of scope |
| C1 `CAP-12` t8.0 | sky (overhead deck ceiling) | 213.9 | 167.7 / 167.8 | same reading as `C21`'s dedicated 670 m underside box — Wave C's own target |

**Nothing regressed.** Every open delta above either pre-dates this plan (C2's suburb grid, C1's
terrain-lighting gap, `BL-304`'s water exemption) or is Wave C's own scope (the deck
underside/tops brightness) — none is a fresh miss from this wave's fog work.

### `BL-303`/`BL-100`/`BL-101` closed

- **`BL-303`** — deleted. Sky half of all three scenes fixed (`B16`); C2B and C5 are fully inside
  the ±10 bar; C3's sky is inside the bar and its terrain murk is not — see the two residual
  candidates above, left for the orchestrator to mint. C5's lit-facade adjunct likewise left for
  the orchestrator.
- **`BL-100`** — deleted, fully closed. All eight chapters now have a zone verdict: C1B/C2/C3/C5 =
  `zone1` (settled before this plan, from geometry — `horizon/zone2` is a bare marker in each);
  **C1/C2B/C4 = `zone2`, verified against original footage** (`B12`: C1's moon + star field, C4's
  moon over the deck, C2B's sky-colour match) — **C1C = `zone2` on asset-identity + parity only**,
  since no original C1C IA1 footage exists (C1C is unreachable in Instant Action; its
  `horizon/zone2` is the same four meshes at the same bboxes as C1's, its `zone1` a single
  featureless mesh).
- **`BL-101`** — deleted per decision 10. This plan's three waves *are* the fine-tune-and-compare
  method the item asked for, run to completion for the scenes this milestone measures; a future
  chapter-specific fog mismatch mints its own item, not a reopened `BL-101`.

### Files

`.scratch/b17/` (`shoot.ps1`, `measure.py`, ten renders, per-pose weather logs, three
`runtests-*.out.log` full runs, `run-tests-guarded.ps1` — the `BL-320` hang workaround, kept since
the hazard is still live), `analysis/goldens/manifest.json` (11 hashes re-pinned), `backlog.md`
(`BL-303`/`BL-100`/`BL-101` deleted), `PROJECT_CONTEXT.md` ("Current status" wave-position
clause), this section and the checklist line.

### Original brief (kept for reference)

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

## C26 ☑ The last strip: the dome wall's base must meet the fog wall seamlessly

**Landed** (2026-08-09), as the fork's own recommendation: candidate (1)'s extent half, an
untextured fog-saturated annulus extending the below-band ceiling's half-span from 6144 m to
20,480 m. `K` is untouched. **STOPPED at candidate (b) — no code written, by this item's own
brief**, is the record of the analysis pass that found the fork; kept below exactly as written.
The symptom
reproduces exactly and its mechanism is now traced to the byte: **the strip is the dome wall's own
authored vertex gradient, rendered correctly.** Candidate (a) — "our render is off (sRGB, mip
selection, a vertex-colour product)" — is **refuted three independent ways**; the base ring lands on
`FOG_COLOR` to the decimal. So the residual is candidate (b) — the data itself darkens immediately
above the base — and per the brief that "needs a user decision, not an invention". **The fork the
user must take is below.** Nothing in `CSVM/` changed; `git diff` over the tree is the plan section,
the checklist line and one `weather.md` decode.

### The repro — a 13-px band, altitude-invariant, hard-edged at the deck rim

C1 freecam, west from the pinned x/z (`--pos=-7323,<y>,-3829 --direction=-1,0,0`) — open water past
the map's west edge, no terrain in frame, level pose so the horizon is row 360 (`SHOT-23`).
`.scratch/c26/ladder/`, measured by `.scratch/c26/rung.py` (flat rows only, `SHOT-22`):

| rung | dip below `FOG_COLOR` | at | rim step | reading |
|---|---|---|---|---|
| 300 m | **3.47** (172.53) | 8 px | +1.76 | terrain still covers the lowest rows |
| 400–900 m | **7.40** (168.60) | 13 px | **+5.25** at 13→14 px | **bit-identical at every rung** |
| 1000 m | — | — | +2.73 | inside `CLOUD_COVER`'s whiteout (970–1124), band reads 204 |

Both surfaces are camera-anchored — the dome by construction, the deck ceiling at `camera.y + K` —
so the strip does not move with altitude at all. What changes while climbing is only that the
terrain silhouette sinks below it, which is why the user sees it appear on a climb and why `C25`'s
river-pose prep (terrain onset 14–17 px) under-called it at 168.67.

The band's shape, row by row (500 m rung): 176.00 at the horizon, 175.11 / 174.39 / 173.93 …
168.60 at 13 px, then **173.85 / 176.00** — the deck rim's hard edge at `f·K/6144` = 13.2 px, exactly
where `C21`'s rim formula puts it.

### Why it is NOT a render defect — candidate (a), refuted three ways

C1 `zone2`'s wall (`h_zone2scroll`, model 773) authors its base ring **(176,176,176) = `FOG_COLOR`
byte-exact**, grading to (99,112,154) at the ring above it, which sits at only **9.8°** elevation.
At the same pose:

| probe | result | what it proves |
|---|---|---|
| `--tex-override=Sky1.tif=00ff00` | strip → (0, G, 0) with **G equal to the base render's G at every row** (176.00, 175.00, 174.41 … 168.49) | the strip is the wall, and the texture factor is exactly 1.0 in G |
| `--tex-override=Sky1.tif=ffffff` | **byte-identical frame** (0 / 921,600 px changed) | the texture contributes nothing at all here — nothing to mis-sample, no mip artifact |
| `--tex-census --no-fog` | strip = (119,59,176) against `sky1`'s census colour (175,89,255) ⇒ ×(176/255) | the modulate is one clean multiply by the authored vertex colour |

The white override is an able-to-fail control, not a null result: the **same** override at the
above-deck pose moves 261,029 px (28.3 %, max 48), so the instrument works and the strip really is
texture-free. The reason is in the data — the shipped `rtexture*` `sky1` carries a **27-row white
band** at top and bottom, and the wall's base UV (`v = 0.97265625`) sits inside it; the band runs out
at **13.6 px** of elevation, just past the deck rim. So the authored colour lands **once**,
unsquared and unmodulated: 176.00 at elevation 0, and 166.99/168.49/173.38 (RGB) at 13 px against the
authored interpolation's 166.4/166.7/172.8. **B18's family of defect is simply not present on the
wall.**

The design is install-wide and now in `weather.md`: every chapter's wall base ring wears its flown
zone's `FOG_COLOR` — C1/C1C/C2B 176, C1B (16,24,48), C2 (205,215,255), C3 200, C4 192, C5 0 — the
wall-side sibling of `B18`'s skirt decode. The dome is *authored* to meet the fog wall seamlessly,
and ours does. What is authored above that ring is a steep sky gradient.

### What the original does instead — and why only the user can pick the fix

The original does not show the gradient near its horizon, and the frames say so flatly:

| original frame | horizon row | above it |
|---|---|---|
| `C1 IA1 Fog river.png` (below band) | **370** — terrain onset row 411 minus `C21`'s measured 41 px | rows 335–370 are **exactly 175.00, per-row sd 0.00, for 36 px**; the deck's mottling only starts at 334 |
| `CAP-12 t20` 670 m (below band, no terrain in frame at all) | inside its flat band | a ~17-row dead-flat 175.00 run, then structure both sides |

⇒ **The bar and our number: the original's step at the wall base is 0.00 over ≥17 px; ours is 7.40
with a +5.25 edge.** Side by side, magnified, at `.scratch/c26/AB-horizon-strip.png`.

Three mechanisms can produce the original's flat band, and **every one of them is outside this
item's licence**:

1. **The ceiling's rim is higher than ours.** Our rim sits at 13 px (`K` = 135 over the sheet's
   6144 m half-span); the original's below-band frames keep FOG_COLOR flat to 36 px. Either `K`
   or the sheet's *extent* would hide the gradient by construction — but `K` = 135 is the **user's
   own pick at the controls** (`C25`), and the extension is the approach `C21` refuted. Cheapest to
   test, and it is a `C25` reopening, not a C26 fix. ⚠ Note the rim elevation is `atan(K/R)`: extending
   the sheet alone (R 6144 → 20 km) moves the rim 13 px → 4 px **without touching `K`**, so the two
   halves of `C25` are separable if the user wants only one of them.
2. **Fog on the dome, with the altitude term doing the work.** C1 `zone2`'s `FOG_ALTITUDE` is
   4000→5000 m, and at our 2.5× dome scale the wall crosses it — low fragments would pull to
   `FOG_COLOR`, high ones stay clear, which is the observed shape. **`B16` is a landed verdict:
   no fog on the dome, in any form.** Recorded because it fits, not because it may be built.
3. **A different dome anchor or vertical scale.** `B18` settled the geometry (camera-anchored,
   uniformly scaled), and uniform scale cannot change an angle, so this means a *non-uniform* Y
   scale or a lower anchor — a real geometry change to a landed decode.

**Recommendation for the fork: (1), and specifically the extent half.** It is the only one that
touches neither a landed verdict nor the user's own `K` choice, it is bounded and deterministic
(`A5`'s map-edge precedent), and it predicts the original's flat band by construction rather than by
taste. But it changes an approved look, so it is the user's call — same shape as `C25`'s.

**→ An anchoring-probe run after this analysis closed the remaining door on candidate (3):** the
dome is authored at a map corner with an 8.7 km radius, not centred on the flown area, so an
authored-STATIC placement could never cover the map from every camera position the way the
original plainly does — the original must move the dome with the camera too, exactly like `B18`
found. Candidate (3) (a different anchor or vertical scale) was already the least-favoured reading;
this closes it outright rather than leaving it as a live alternative. Camera-anchoring stands.
**The user took (1)/extent the same day**, and it is what landed below.

### Fork resolved (user, 2026-08-09): the extent half of (1) lands as a fog-saturated annulus

**Mechanism.** `WorldBuilder.AddDeckAnnulus` gives the below-band ceiling a second piece: a flat,
untextured four-quad picture frame around the 144 textured tiles' own measured footprint, reaching
a 20,480 m half-span (20×1024 m — the next tile boundary up from the sheet's own 6144 m). By
`C21`/`C25`'s own rim formula (`f·K/halfSpan`, `f` = 599.1 px, `K` = `DeckCeilingHeight` = 135 m,
both untouched), that moves the rim from 13 px to ≈3.95 px — inside which the dome WALL's own
authored base-ring gradient (this item's own finding: the wall is correct, not a defect) has lost
only ~2 units instead of ~7. The annulus is never invented colour: its material is
`SceneBuilder.BuildFlatQuadMesh`'s call into `GetMaterial(-1, …)`, the SAME no-texture branch of
`BuildMaterial` a `Colored` gamez polygon with no material index gets (`fogged: true`), so its
`ALBEDO = mix(ALBEDO, csky_fog_color, fog_amt)` line is byte-for-byte the deck tiles' own — not a
hand-picked `FOG_COLOR` constant, but the SAME fog pipeline computing the SAME output. Every point
it is built for sits beyond every deck chapter's own authored `FOG_RANGES` far (C1/C1C/C2B 4000 m,
C4 4500 m — the EXISTING 144-tile sheet's own edge, at 6144 m, already exceeds all four), so
`fog_amt` is 1.0 there regardless of the per-regime SUNLIGHT-dimming swap `C23` built for the
tiles — ONE static mesh serves both regimes, verified below, not assumed. 20,480 m is close to a
ceiling, not just a tidy number: C1/C1C/C2B/C4's zone2 dome renders at 8.74 km × 2.5 = 21.85 km,
and the annulus stays a 1.37 km / 6% margin inside it — the next tile boundary (21,504 m) was
rejected as too close. Symmetric around the tile grid's OWN measured AABB centre
(`WorldBuilder.MergedLocalAabb`, a full recursive walk — a first, shallower version of this method
that checked only `deck`'s direct children came back a degenerate zero box and mis-centred the
annulus on world-local (0,0,0), producing a huge mis-placed quad; the climb-ladder probe below
caught it before landing, which is why the method's own comment records the failure), so
`GameSession.AssignCloudDeckIfBuilt`'s later `OrbitCamera.MergedAabb` re-measurement lands on the
identical centre and every existing tile pixel is unperturbed. Tagged
`WorldBuilder.DeckExtensionMeta` node metadata, read by `WeatherRig.CollectDeckTiles`, so it counts
toward neither this file's own "144 tiles" print (which reads `_deckNodes.Count`, fixed before
either loop runs, not the live child count) nor the "N of M deck tile(s) carry an undimmed twin"
census — both stay 144/144.

**Verification.**

*Climb ladder* (`--chapter=C1 --freecam --det --mute --pos=-7323,<y>,-3829 --direction=-1,0,0`,
`.scratch/c26/rung.py`, horizon row 360):

| rung | before (dip / rim step) | after (dip / rim step) | original |
|---|---|---|---|
| 300 m | 3.47 at 8 px / +1.76 | 1.37 at 28 px / +0.87 (4→5) | — |
| 400–900 m | **7.40 at 13 px / +5.25 (13→14)** | **2.07 at 4 px / +1.11 (4→5)** | 0.00 over ≥17 px |
| 1000 m (inside `CLOUD_COVER` whiteout) | −28.29 / +2.73 | −30.17 / +0.48 | — |

The 400–900 m rungs are bit-identical to each other before and after, as before (both surfaces are
camera-anchored). The rim STEP — the thing the eye actually catches — falls from a hard +5.25 to a
soft +1.11 flowing straight into flat `FOG_COLOR`; the residual dip lands at 2.07 units, a hair over
the fork's own illustrative "≤2" mark (the practical ceiling is the dome radius above, not the fog
ramp) and no longer a discontinuity. Full row dump at elevations 7–21 px (the old rim, now deep
inside the annulus) reads **RGB(176.00,176.00,176.00), per-row sd 0.00** — including row 347, the
EXACT boundary between the 144 textured tiles and the annulus — a zero-unit, zero-variance step:
the two surfaces are computing the identical value, not matching by luck.

*Floor regime (above-deck pinned pose, `-7323,1192,-3829` / `0,0,-1`).* The stale `.scratch/c25`
reference PNGs no longer reproduce on this machine/build even with zero code changes (a same-day
control shot from a temporarily reverted build already differs from them by 46% of pixels — an
environmental drift unrelated to any plan item, not a regression; METHOD-3). Comparing instead
against a FRESH same-day control (reverted build, identical flags): fog-on is **byte-identical, 0 /
921,600 px** — the existing sheet already covers the downward view from above and the floor regime
needs no annulus. `--no-fog` (never part of normal play) shows a confined artifact — 0.62% of
pixels (rows 364–373 only), the annulus rendering its raw white `ALBEDO` where fog would otherwise
be the only thing painting it `FOG_COLOR`; noted, not fixed (fixing it would mean special-casing
`_spec.NoFog` in geometry code, which the item's own "do not touch fog code" trap rules out for a
debug-only flag with no gameplay path).

*C4 below-band spot check* (`-4974,900,-3861` / `-1,0,0`, fogged, vs a fresh same-day control):
**0.03% of pixels changed (251 / 921,600), max 3**, confined to rows 347–348, and every changed
pixel moves from the wall's own gradient (189,190,193) to exact `FOG_COLOR` (192,192,192) — the
mechanism landing at C4 exactly as it does at C1, at a smaller magnitude because C4's own gradient
was already flatter there. `--no-fog` shows the same confined white-annulus artifact as the C1
above-deck check, for the same reason.

*Flip ladder* (`-7325,<y>,-3829` / `0,0,-1`, `--frames=20`, C1): the four rungs spanning the regime
crossing — 1035/1046/1048/1060 — are **bit-identical to each other**, flat mean lum 243.00, same as
`C23` found; 1025 vs 1075 (outside the opaque core) differ by 46.7% of pixels, confirming the
instrument would have caught a leak had there been one.

*8-chapter `--freecam --frames=20` regression*: **zero errors, all eight.** Deck census
**144 tiles** at y=960 (C1/C1C/C2B) / y=1050 (C4) in every chapter, exactly as on record; `deck
lighting: 144 of 144 deck tile(s) carry an undimmed twin` in all four; clusters C1 28 · C1B 70 ·
C1C 30 · C4 45; sprites C1/C2B/C4 22,201 · C1C 22,748 · C5 16,170 — every number unchanged. C1's own
`gamez nodes`/`mesh instances` count (7064 / 3423) is identical between a fresh control build and
this one — the annulus is one new `MeshInstance3D` per deck chapter, which this broader census does
not even count the way the deck-tile print does.

**`.\RunTests.ps1`** — build PASS (0 warnings), **units 683/683**, **engine 26/26, errors clean**,
goldens **6 moved, 0 broken of 13** (exit 1 is the golden stage alone, per convention). The six
movers are the SAME six names as `C22`/`C25`'s own standing un-repinned set — `c1-waterfall`,
`c1c-rain`, `c2b-rain`, `c4-snow`, `c1-flight`, `c1-destroy-effects` — and no shot outside that set
moved, which is `GOLD-5`'s own pattern check: every mover is a deck chapter (C1/C1C/C2B/C4), every
`ok` is not (`C1B`/`C2`/`C3`/`C5`) or has no deck/sky in frame (`c1-crash`, `viewer-bhawk`,
`empty-stage`).
⚠ **First run of this stage used a stale working tree** — a `git stash` opened to shoot a
same-day control comparison was left un-popped across the launch, so the FIRST `RunTests.ps1` here
actually built and tested the PRE-`C26` code; caught by re-checking `git status` before trusting
the result, corrected by popping the stash, rebuilding and re-running clean (`METHOD-6`/`METHOD-16`
— confirm which code a run used, and force/verify the rebuild after restoring). The accidental
run is not wasted: it is exactly `GOLD-8`'s **reverted-build A/B**, run for free. Reverted-build
hashes (`c1-waterfall c81a000a…`, `c1c-rain 81e9dccd…`, `c2b-rain 57f097e1…`, `c4-snow 421ead8f…`,
`c1-flight 9e162b5a…`, `c1-destroy-effects 0d749511…`) are exactly `C23`'s own on-record values;
the corrected, landed-code run's hashes (`c1-waterfall c81a000a…` — unchanged — `c1c-rain
ab24ff16…`, `c2b-rain a092c1ca…`, `c4-snow 00e3ffb4…`, `c1-flight 9a5369f6…`, `c1-destroy-effects
dfcad9ed…`) show **this item DOES move five of the six further**, on top of their existing
`C22`/`C25` cause — `c1-waterfall` alone is unaffected, so its own pose does not reach the strip.
Consistent with everything else measured (a small, sky/ceiling-confined shift) and with `0 broken`
of 13 — `RunTests`' own broken/moved split did not flag any of the five as structurally different,
only hash-different. Not re-pinned — `C24` re-pins once, per the plan's own convention, and its own
landing should cite this item's contribution alongside `C22`/`C25`'s. `viewer-bhawk` hit `BL-320`'s
known hang; the guarded runner (`.scratch/b17/run-tests-guarded.ps1`) killed the one stray Godot
process and the shot reported `ok` on retry, hash valid.

**Probes worth human eyes:** `.scratch/c26/AB-horizon-strip.png` (the original still record); a
fresh side-by-side of `.scratch/c26/after/c1-500.png` against `.scratch/c26/ladder/c1-500.png`
(before) is the single clearest before/after of the rim closing.

**Not touched, per the brief:** `K` (`DeckCeilingHeight`, still 135), the dome, the `fvol` field,
`cloudparent`, the whiteout, and every line of `shaders/csky_atmosphere.gdshaderinc` (the annulus
reuses the EXISTING fog-mix code path rather than adding to it).

### Files

`CSVM/src/Mech3/WorldBuilder.cs` (`AddDeckAnnulus`, `MergedLocalAabb`, `DeckExtensionMeta`, the
"144 tiles" print switched from `deck.GetChildCount()` to `_deckNodes.Count`),
`CSVM/src/Mech3/SceneBuilder.cs` (`BuildFlatQuadMesh`, reusing `GetMaterial`'s existing no-texture
branch), `CSVM/src/Session/WeatherRig.cs` (`CollectDeckTiles`'s `Collect` skips
`DeckExtensionMeta`-tagged instances), `docs/formats/weather.md` (one paragraph pointing the
wall-base-ring decode at this engine-side consequence), `docs/architecture.md` (the
`WorldBuilder.cs`, `SceneBuilder.cs` and `Session/WeatherRig.cs` entries), this section and the
checklist line. `PROJECT_CONTEXT.md` untouched. Probes and
instruments in `.scratch/c26/`: `scout.ps1` (the clean-horizon search), `identify.ps1` (the three
identification probes), `texcontrib.ps1` (the able-to-fail control), `ladder.ps1` + `ladder/` (the
stop-first pass's before evidence), `band.py` / `rung.py` / `diff.py` / `texrows.py` / `mat.py` /
`montage.py`, `AB-horizon-strip.png`, and the landing's own `after/` (the fixed-build ladder,
above-deck and C4 shots), `control/` (fresh same-day reverted-build baselines, METHOD-3 —
the stale `.scratch/c25` PNGs stopped reproducing on this machine independent of this item, so the
landing's own comparisons are against these, not those), `after/flipladder/` and `after/regress/`
(the whiteout-core and 8-chapter sweeps).

### Original brief (kept for reference)

**Goal.** Climbing below the band over a clean horizon shows no residual strip between the fog
wall and the deck rim — the dome wall's lowest rows read as the fog colour, as the original's do.

**Evidence (confidence: symptom user-confirmed at the controls 2026-08-09; mechanism lead-only).**
C25's own prep measured the residual and under-called it: after K=135 the rim step is
176.00 → 174.02 → 168.67 over 2 rows at 13–14 px elevation — "soft" in a still at the river pose
(terrain hides the lowest rows there), visible in motion over a clean horizon. The strip is the
dome **wall**'s base (textured, from local Y=0 up) rendering darker than the `FOG_COLOR` it must
meet; B18 fixed exactly this family on the untextured **skirt** below Y=0 (authored to wear
`FOG_COLOR`, we applied the colour twice). Candidates: (a) the wall's base row/vertex colours are
authored to land on `FOG_COLOR` and our render is off (sRGB, mip selection at grazing angles, a
vertex-colour product — B18's cross-tab found 99 real vertex-gradient polygons; the wall may be
one, with a gradient our pipeline mis-lands); (b) the base is genuinely darker in the data and
the original hides it otherwise (unlikely — the original shows no strip at any altitude).

**Approach.** Reproduce first: a climb ladder over a clean horizon (open water/flat area, e.g.
toward the map's south rim), 300→1000 m, measuring the 0–20 px elevation band per rung. Then read
the wall's authored data (base vertex colours, texture bottom rows, per-chapter) and compare the
predicted vs rendered base colour. Fix per the evidence — the B18 pattern (land the authored
colour once, correctly) is the precedent; never fog, never an invented gradient.

**Model recommendation.** high — same easy-to-fake-and-wrong shape as B18.

**Verify.** The climb ladder clean at every rung (step at the wall base ≤ the original's own,
measured from an original frame with a clean horizon); river + above-deck poses unchanged beyond
the strip rows; 8-chapter freecam horizon sweep (B18's poses) unregressed; RunTests (goldens —
list, C24 re-pins).

**⚠ Traps.** B16/B18 are landed verdicts — no fog on the dome, no re-fogging the skirt. SHOT-23:
compare like-pitched frames or state the pitch. The strip rows overlap the rim math from
C21/C25 — change the WALL's rendering, not K.
— *all three held, and the last one is what stopped the item: the WALL's rendering turned out to be
correct, so there was nothing there to change and the surviving mechanisms all live on the other
side of that line.*

## C25 ☑ The below-band ceiling covers to the horizon and fades like the original's

**Landed.** (2026-08-08) This item's original approach — extend the below-band ceiling past its
rim and give it its own baked fade toward `FOG_COLOR` — was **refuted by `C21` before any of it
was built**: the deck tiles author `fog: true` (not exempt), the original river still's ceiling
fits the zone's own **authored** 1000→4000 m linear fog ramp to 1.0–1.3 units rms with no other
mechanism needed, and the "~12.6 km reach" that motivated the extension was `A7`'s `f·h` read
~3× too large, not evidence of a long fade. What was actually wrong is the ceiling's one free
parameter, `DeckCeilingHeight` (`K`): at the shipped 400 m the sheet's rim sits at an elevation
(39 px) where the original still plainly shows deck, so the region between our rim and the
horizon shows the un-fogged dome instead — that gap is the "sky stripe," and it is a `K`
artifact, not a missing extension or a missing fade. **The landing is `K`: 400 m → 135 m, nothing
else.** No ceiling extension, no authored fade, no `fog: false` on the deck — the existing fogged
sheet already covers to the horizon and fades exactly like the original's once its height is
right. The bracket (110–155 m) is `C21`'s fog-ramp fit; the point inside it, 135, is the **user's
pick from the controls**, not a further measurement — 135 and 400 were rendered side by side
against the original still and the user chose 135 (`.scratch/c25/k-decision-montage.png`,
2026-08-08, "K=135 (Recommended)" selected), because changing `K` revisits `A7`'s approved look
("it looks a lot better. approved.") and no fit can make that call on its own.

### The stripe is `K` alone, confirmed at the pixel

`C21`'s rim-elevation formula, `f·K/6144` (half the 144-tile, 12,288 m span), predicted the fix
before it was built: at `K` = 400 the rim sits at row 321 = **39 px**, matching our own render's
deck edge to the pixel (`599.1×400/6144 = 39.0`); at `K` = 135 the rim moves to **13 px**, inside
the fog-saturated band (completion ≈17.5 px) and inside this pose's own terrain onset
(~14–17 px, `SHOT-23`) — so the deck's rim can never be reached before the ground silhouette
covers the sky anyway, and the stripe cannot appear by construction, not by luck.

### Verification

**1. River pose — the stripe is gone, measured.** `-7323,192,-3829` / `-0.997,-0.1,0.070`, fog on,
`.scratch/c21/measure.py rows` at horizon row 301 (this render's own pitch, `SHOT-23`) — before
reused from `.scratch/c22/river-fog.png` (`K`=400, current HEAD at the time this item started),
after shot fresh on this build (`.scratch/c25/river-fog-k135.png`):

| elevation | K=400 (before) | K=135 (after) | reading |
|---|---|---|---|
| 44–46 px | 176.00, hp 0.000 (pure fog, d < 4000 m) | 174.5–174.6, hp ≈0.28 (deck, near-saturated) | both past the rim at their own `K` |
| 34–40 px | **142.5–152.6, hp up to 3.26** — the dome's raw, unfogged colour showing through | **169.5–172.6, hp ≤0.30** — flat, fogged deck, no dome visible | **the stripe itself: present at 400, gone at 135** |
| ≤12 px | 160.79–165.97, byte-identical between builds | 160.79–165.97, byte-identical between builds | terrain onset (`SHOT-23`) — independent of `K`, confirms the horizon row is right |

The predicted mechanism and the measurement agree exactly: the K=400 dip (142.5 at its lowest) is
the dome wall B16 correctly un-fogged, sitting exactly where the rim math places it; at K=135 that
same elevation band is now inside the deck's own reach and reads as the deck's ordinary 7-unit
fog fade, not the dome.

**2. Above-deck pinned pose — byte-identical, both fog states.** `-7323,1192,-3829` / `0,0,-1`
(above C1's ~1047 m band centre, where `DeckRegime` puts the deck on the world-fixed-floor
branch that never reads `DeckCeilingHeight`) — verified, not assumed: full-frame diff
(`.scratch/a7/compare.py`) against `.scratch/c22/abovedeck-dome-{fog,nofog}.png`, this build's
`.scratch/c25/abovedeck-dome-{fog,nofog}-k135.png`:

| variant | mean\|d\| | max | px changed |
|---|---|---|---|
| fog on | 0.000 | 0 | 0 / 921,600 (0.00 %) |
| `--no-fog` | 0.000 | 0 | 0 / 921,600 (0.00 %) |

**3. C4 spot check — same regime, no artefact.** C4's deck shares the one `DeckCeilingHeight`
constant (`A7`), so a below-band C4 pose should move (the ceiling is genuinely closer now) without
anything breaking. `-4974,900,-3861` / `-1,0,0` (below C4's 1050 m band centre), `--no-fog`,
against `.scratch/c22/c4-900-nat.png`: **mean\|d\| = 1.046, max = 168, 3.49 % of pixels changed**
— all of it in the sky/ceiling band; the terrain silhouette (including the sharp near-vertical
feature this plan read as a mesh spike and minted `BL-323` for — actually a mountain seen from a
pose below the ground, `git log --grep=BL-323`; present identically in both frames) is
pixel-for-pixel the same.
Expected movement, no new artefact, no regime break.

**4. Climb ladder 900→1250 m — the flip is still masked inside the whiteout core.** 25 m steps
plus the 1046–1048 m bracket on the regime flip (`.scratch/c25/ladder/`): the four frames spanning
the flip — **1035, 1046, 1048, 1060** — are **bit-identical to each other** (`mean|d| = 0.000,
max = 0`, flat `mean lum = 243.00`), and the ramp either side is monotone (154.13 → 198.93 →
233.31 → [243.00 ×4] → 237.91 → 229.83 → 159.41). The jump is now 135 m instead of 400 m, but the
core it hides inside didn't move, so the flip is exactly as unobservable as `A7` found it.

**5. 8-chapter `--freecam --frames=20` regression — zero errors, census unchanged.** Every count
matches the on-record numbers exactly: decks C1/C1C/C2B 144 tiles @ y=960, C4 144 @ y=1050;
clusters C1 28 · C1B 70 · C1C 30 · C4 45; sprites C1/C2B/C4 22,201 · C1C 22,748 · C5 16,170 — a
constant-only change touches no geometry or placement, so an unchanged census is expected, not a
coincidence.

**`.\RunTests.ps1`** — build PASS (0 warnings), **units 682/682**, **engine 26/26, errors clean**,
goldens **6 moved, 0 broken of 13**. Exit 1 is the golden stage alone, per convention. The movers
are `A7`'s original six, not `C22`'s five — **`c4-snow` moves again**, because `K` is a shared
constant read by every below-band deck regardless of `C22`'s `WorldLight` clamp, and C4's golden
camera sits below its own band centre:

| golden | moved? | why |
|---|---|---|
| `c1-waterfall`, `c1-flight`, `c1-destroy-effects`, `c1c-rain`, `c2b-rain`, `c4-snow` | **moved** | every below-band deck chapter with sky in frame — `K` changes what the ceiling projects to |
| `c1b-night-sea`, `c2-city`, `c3-island`, `c5-city-night`, `c1-crash`, `viewer-bhawk`, `empty-stage` | **ok** | no deck (`C1B`/`C2`/`C3`/`C5`), no chapter world (`viewer-bhawk`/`empty-stage`), or no sky in frame (`c1-crash`, straight-down) |

**Not re-pinned — `C24` re-pins once**, per the plan's own convention (`GOLD-1`/`GOLD-8`). The
`viewer-bhawk` hang `BL-320` recorded did not reproduce this run.

### Files

`CSVM/src/Session/WeatherRig.cs` (`DeckCeilingHeight` 400f → 135f, its `TUNE` comment rewritten to
carry `C21`'s fog-ramp derivation, the rim-math confirmation, and the user's choice from the
bracket), `docs/architecture.md` (the `WeatherRig.cs` entry's `K` note), this section and the
checklist line. `PROJECT_CONTEXT.md` untouched. `.scratch/c25/` — the decision montage, the
before/after river and C4 renders, `river-fog-k135.png`, `abovedeck-dome-{fog,nofog}-k135.png`,
`ladder/` (the climb-through set) and `regress/` (the 8-chapter sweep); reuses `.scratch/c21/`'s
`measure.py` and `.scratch/a7/`'s `compare.py` as instruments, and `.scratch/c22/`'s renders as
the `K`=400 "before" side (this build's own code had not changed since `C22` landed, so those
stills are still valid controls).

### Original brief (kept for reference)

**Goal.** From below the band, no skybox is visible between the fog wall and the deck's edge —
the ceiling reads continuous to the horizon, fading into the haze the way the original's does.

**Evidence (confidence: direction traced — user observation on the B17 screenshots, 2026-08-08;
mechanism has one strong lead).** The A7 ceiling's rim sits at the sheet's half-span (~6.1 km →
~3.7° above the horizon at low altitude), and since B16 un-fogged the dome, its sky texture
shows in that stripe — the user's "skybox below the deckcloud". Pure extension can never close
the stripe (`atan(400/R)` > 0), so the fix pairs extension with the fade treatment. The lead:
B15 measured the original's deck texture readable to **~12.6 km** at the river pose while C1's
fog fully saturates at 4 km — under our semantics a fogged deck cannot do that, so the
original's ceiling is plausibly **fog-exempt with its own baked fade toward `FOG_COLOR`**, the
same authored pattern as B18's skirt (the horizon is built from pieces that already wear the
fog's colour). C21's research pass owns confirming this from the stills before this item builds.
— ⚠ **`C21` REFUTED this lead (2026-08-08): read its section before starting here.** The deck
tiles author `fog: true` (the `cloudsprite` cards beside them author `fog: false`), and the
original river still's ceiling fits the **authored** 1000→4000 m linear ramp to 1.3 units rms.
The 12.6 km reading is retired — 19 px is where the authored far range saturates, not where a
long fade ends, and it read as 12.6 km only through A7's `f·h`, which `C21` puts ~3× too large.
`C21`'s verdict is that this item becomes **`K` = 135 m, then verify** — no extension, no
authored fade, no `fog: false` — with the extension needed again only if the user rejects the
`K` change (which alters an approved look, so it is a user decision).
— *the user did not reject it: `K` = 135 was chosen at the controls from a side-by-side render,
and the item landed exactly as `C21` scoped it — a constant change, nothing else.*

**Approach.** Per C21's verdict: extend the below-band ceiling well past the current rim (A5's
map-edge-extension precedent — virtual tiles, deterministic, bounded by where the fade ends) and
land the evidenced fade (deck `fog: false` + a fade toward the zone's `FOG_COLOR` by horizontal
distance, if that is what the stills show — the fade's reach measured from the original, not
invented). Above-band (the world-fixed floor) may need the same extension for the from-above
horizon; verify at the above-deck pose.
— *not followed: the extension and the fade were never built. `C21`'s refutation made them moot
before this item started coding, and the verify section above confirms the existing fogged sheet
covers to the horizon and fades correctly on its own once `K` is corrected.*

**Model recommendation.** high — a render-mechanism change judged against the plan's own
reference stills.
— *revised down in practice: once `C21` closed off the mechanism question, this item was a
one-constant change plus verification, not a render-mechanism change.*

**Verify.** River pose: no sky stripe below the deck, mottling reach toward the original's
row-337/19-px-above-horizon reading (SHOT-23 pitch caveat applies); above-deck pose horizon
unchanged or improved; C4's deck (same regime) spot-checked; 8-chapter freecam; RunTests
(goldens move — list, C24 re-pins).
— *all done; see Verification above. The row-337/19-px original reading is the far-range
saturation point (`C21`), not a fade target — the stripe check above is the item's real bar.*

**⚠ Traps.** Do not touch the whiteout, the dome, or the fvol field — this is the DECK mesh
population only. The fade must come from measured original behaviour, not taste; if C21's
stills-read contradicts the fog-exempt hypothesis, build what the stills show instead. K
(`DeckCeilingHeight`) may change under C21's re-estimate — re-verify the ceiling look at 192 m
AND ~900 m so a K change and the extension are not conflated.
— *moot: no extension was built, so there was nothing to conflate. The whiteout, dome and fvol
field are untouched — confirmed by the ladder (whiteout), the above-deck byte-identity (dome path)
and the unchanged sprite censuses (fvol field) above.*

## C21 ☑ Find the original's underside mechanism (research)

### VERDICT (2026-08-08 — research only, no `.cs`, no shader, no `weather.md` touched)

**The mechanism is the mission's own SUNLIGHT world dimming, and we are the ones not applying
it.** `cloudlayer.tif` has a mean luminance of **210.54** (64×64, sd 8.05, range 190–223); the
original's underside measures **167.7–169.7**; and `210.54 × 0.802 = 168.9`, where **0.802 is
`WeatherState.WorldLight` for C1/IA1** (`AMBIENT 0.25 + DIFFUSE 1.2 × SunIncidence 0.46`). Our
deck renders the **raw texture, modulated by nothing at all** — measured, not inferred:
`--tex-override=cloudlayer.tif=808080 --no-fog` at CAP-12's own underside pose reads back
**RGB(128,128,128) on 100.0 % of the box** (210,432 px, one value). The reason is one line:
`SceneBuilder` gates `ALBEDO *= csky_world_light` on the model's `lighting` flag, and all 144 C1
deck tiles author **`lighting: false`**.

**This is a regression against a value that was once matched, not a new fit.** `SunIncidence`
0.46 was *calibrated on this very surface*: `Flight/Weather.cs`'s own comment — "one TUNE
calibrated to the C1/IA1 reference (A=0.25,D=1.2 → 0.80, **matching the original's deck 210→169**
and terrain →~57)" — and `PLAN-M2-polish-2` recorded the result as "deck/sky **168 vs 169**". The
deck left that match when the `lighting`/`fog` flags became shader variants. So C22 restores a
calibrated number; it does not invent one.

**Three independent confirmations, none of them the same measurement:**

1. **Arithmetic.** Ours unfogged at CAP-12's box **212.74** → × 0.802 = **170.6**; original
   **168.1** (same box) / 169.7 / 168.7 (the two wide side bands).
2. **The fogged still fits it as a free parameter.** The `f·h` fit below (`fitfog.py`) leaves the
   ceiling's own unfogged colour `L0` free and recovers **168.1–169.2** from the original river
   still's near-horizon ramp alone — a number arrived at from the *fog*, with the texture never
   consulted.
3. **C4 is the cross-chapter control and it passes.** C4's `WorldLight` is
   `clamp(0.5 + 1.5×0.46) = 1.19 → **1.0**`, so the mechanism predicts **no change to C4's deck**
   — and C4's deck already matches: original 192.0/191.9 (`t100`) and 192.8/192.1 (`t85`) against
   ours 191.6. A fixed "underside is 0.8× darker" rule would have broken C4 by −45.

**The three candidates in the brief are all dead, by data read before any probe:**

| candidate | how it died |
|---|---|
| authored per-face/vertex data we ignore (B18's restated-colour shape) | **All 144 C1 deck polygons carry vertex colours (255,255,255)** and a **`Textured`** material (`cloudlayer.tif`), so `VertexColorsRestateMaterialColor` cannot apply and there is nothing for a modulate to mishandle. (C4's 144 *do* carry 192,192,192 — and we already apply it correctly: the same flat override reads back **95** = 128 × 192/255, which is also the empirical proof that our vertex modulate lands in **gamma** space.) |
| a directional / ambient-only term on a down-facing sheet | ambient-only is `0.25 / 0.802 = 0.31` of the lit value ⇒ **≈52**, not 168. And the mechanism must not be face-dependent: it is a per-mission scalar, which is what makes C4 come out right. |
| a texture difference | none: our own unfogged render (212.7) reproduces the extracted texture's mean (210.5) to 2.2 units, so the texture we ship *is* the one the original draws. |

**It must be DECK-LOCAL, and the two able-to-fail controls say why.** The **dome** is also
`lighting: false` and is *correctly* undimmed — B12/B17 measure its apex at 74.3 original vs 75.5
ours. The **`cloudsprite` field** is also `lighting: false` and also approximately undimmed:
`cloudsprite1`/`cloudsprite2` are one `SphericalY` facade each, vertex colours **240,240,240**,
textures `cloud1`/`cloud2` (alpha-weighted mean **236.65**) ⇒ `236.65 × 240/255 = 222.7`, and we
render **222.3**; the original's near tops read 209–213, which a ×0.802 would have put at 178.6.
So the fix is a `forceLit` on the deck path beside the `forceDoubleSided` that is already there
(`WorldBuilder.Add`) — **never** a change to the `lighting` gate itself, and never to
`csky_world_light`.

### Predicted CAP-12 boxes under the mechanism — written before C22 exists

Populations were separated first, because three of the four boxes turn out not to contain the
deck mesh at all. `cloudlayer.tif` skins the **mesh alone** (SHOT-21's texture-sharing trap is
`cloud1`/`cloud2` = `fvol` clutter + `cloudparent`, which the mesh does not use), so a flat-red
override gives an exact per-pixel mesh mask (`.scratch/c21/population.py`):

| CAP-12 pose | mesh % of the box | what the box actually measures |
|---|---|---|
| 670 m (`t20`, 2200 ft) | **79.8 %** (tight box) / 41.9 % (wide) | the deck **mesh** |
| 1040 m (`t30.5`, 3430 ft) | **0 %** | the whiteout overlay, flat **242.34**, sd 0.00 |
| 1160 m (`t33`/`t124`) | **0.0 %** | 100 % `cloudsprite` field |
| 1700 m (`t97`, 5572 ft) | **0.0 %** | 100 % `cloudsprite` field |

⚠ **That resolves `BL-118`'s tops caveat outright: the "tops from above" box was always a pure
SPRITE measurement**, at every above-band altitude in the ladder. C22 cannot move it, and must
not be tuned as if it could.

| box | original | ours now | **predicted after C22** | Δ vs original |
|---|---|---|---|---|
| **underside** 670 m (`under-L`/`under-R`) | **169.7 / 168.7** | 201.2 / 195.9 (fogged) · 212.7 / 207.2 (`--no-fog`) | **171 ± 3** (deck's own colour 170.6, fog mixes it *up* toward 176 by ≤ +2 at this pose) | **+1 … +4** ✓ |
| underside, CAP-12's own tight box | 168.1 | 211.2 (fogged) · 212.7 (`--no-fog`) | **171 ± 2** | +3 ✓ |
| **interior** 1040 m | 249.8 | 242.3 | **242.3 — unchanged** (0 % mesh) | −7.5 ✓ |
| **tops** 1160 m | 204.9–213.3 | 220.9–222.6 | **220.9–222.6 — unchanged** (0 % mesh) | +9 … +18 ✗ → **C23** |
| **tops** 1700 m | 182.7–201.2 | 205.8–219.6 | **205.8–219.6 — unchanged** (0 % mesh) | +18 … +23 ✗ → **C23** |

Boxes are `.scratch/c21/capboxes.py`'s, fractional and HUD/plane-free on the original side
(gauges x 0.13–0.21 / 0.80–0.87, aircraft x 0.28–0.72, compass band top-centre), so 1250×713 and
1280×720 read the same. Per-chapter: **C4 is a no-op** (WorldLight clamps to 1.0);
**C1C/C2B** both compute `0.6 + 0.4×0.46 = 0.784` and their decks would go 212 → **166.5** — no
original below-deck footage exists for either (C1C is unreachable in Instant Action, C2B's
CAP-11 still is above the deck), so those two are mechanism-consistent and unverifiable, and
should be recorded as such rather than tuned.

### The ceiling's fade mechanism — for `C25`. The fog-exempt/baked-fade lead is REFUTED

**The original's below-band ceiling is fogged, normally, with the flown zone's own authored
ranges — there is no exemption and no baked fade.** The deck tiles author **`fog: true`** (all
144, both C1 and C4; the `cloudsprite` cards next to them author `fog: **false**`, so the data
distinguishes the two and puts the deck on the fogged side). What makes it *read* as continuous
to the horizon needs no new mechanism at all:

1. its own colour (168.9) is **7 units** from `FOG_COLOR` (175), so the entire fade is a 7-unit
   ramp — invisible as a fade; and
2. past saturation the **dome's skirt is painted that same `FOG_COLOR`** (`B18`), so ceiling, fog
   wall and dome are one flat tone with no seam and no sky stripe to close.

**Measured, on `OriginalScreenshots/C1 IA1 Fog river.png`** (level, true horizon row 356;
`measure.py rows`, row-mean over B15's two HUD-free column bands):

| elevation above the true horizon | row-mean luminance | horizontal high-pass |
|---|---|---|
| 346 → 29 px | **168–172, flat** (no trend over 300 rows) | 0.55–0.93 — full mottling throughout |
| 29 → 17 px | monotone **172 → 175.0** | 0.42 → 0.03 |
| **17 px → the horizon → −37 px** | **175.00, DEAD FLAT** | **exactly 0.000** |
| below −37…−41 px | terrain returns | rises sharply |

The −41 px terrain onset reproduces `B15`'s own "terrain 41 px below the true horizon", which
is what validates the horizon row this whole reading rests on.

**Fit** (`.scratch/c21/fitfog.py`): `L(e) = mix(L0, FOG, φ)` with `φ = clamp((f·h/e − 1000)/3000)`
— the **authored** `FOG_RANGES` 1000→4000 and the **linear** ramp `B15` landed — returns
`f·h = 80,000–91,500 px·m`, `L0 = 168.1–169.2`, residual **1.0–1.3 units rms** over ~200 rows.
The able-to-fail control is our own fogged render at the same pose with `K = 400 m` **known**: it
returns 273,500–290,000 against the true 240,000 (+14…+21 % bias), and its saturation elevation
is **exactly 60 px** = `f·400/4000` (rows 300–320 read 176.00, sd 0). The instrument works and it
is honest about its own bias.

**So `B15`'s "the original's deck texture is readable to ~12.6 km" is retired.** The 19-px reach
is not where a long fade ends — it is where the **authored 4000 m far range saturates**. 12.6 km
came from dividing that elevation by A7's `f·h = 2.4e5`, which this item's fit puts ~3× too
large. There is nothing for C25 to invent.

**Numbers C25 needs**, all at C1/IA1 zone2 (`FOG_RANGES` 1000–4000, `FOG_COLOR` 175/176):

- fade **onset** at `d = 1000 m` ⇒ elevation `f·K/1000`; **completion** at `d = 4000 m` ⇒
  `f·K/4000`. Original: onset ≈ 70 px, completion **≈ 17.5 px**. Ours today (K = 400): onset 240,
  completion **60 px**. At K = 135 ours becomes onset 80, completion **20 px**.
- the sheet's **rim** sits at `f·K/6144` (half of the 144-tile, 12,288 m span, camera-followed).
  Ours measures **exactly** where that predicts — the deck ends at row 321 = **39 px**, against
  `599.1 × 400 / 6144 = 39.0`. **At K = 135 the rim moves to 13 px, i.e. *inside* the
  fog-saturated zone, and can never be seen.** The original's own frame is only self-consistent
  that way: at K = 400 its rim would sit at 39 px, where the original still plainly shows deck
  (row 318: luminance 170.5, high-pass 0.69).
- **the sky stripe is real and it is ours alone**: in `.scratch/c21/underside-nofog.png` /
  `-fog.png` the rows between our rim (e ≈ 38) and the horizon read **143–174** — the dome wall,
  which `B16` correctly un-fogged — while the original reads a flat 175.00 there. So the stripe is
  a **K** artifact, not a missing extension: correct K and it closes itself.

⇒ **C25's likely shape changes: no ceiling extension, no authored fade, no `fog: false` on the
deck. Correct `K`, then verify.** If the user rejects the K change (below), C25's extension
becomes necessary again — that is the fork.

### K re-estimate — 135 m (bracket 110–155), superseding A7's 400 m, but it needs a user verdict

| estimator | value | what it rests on |
|---|---|---|
| **fog-ramp fit (this item)** | **f·h 80–91.5 k ⇒ K = 135–154 m**; bias-corrected against the control, **115–132 m** | authored `FOG_RANGES` + the linear ramp + `FOG_COLOR`; **no texture and no cross-engine comparison**; control passes |
| A7's apparent mottling scale, re-run on a properly matched **level** pair | 580–746 /u original vs 539–833 /u ours ⇒ K 279–618 m | a cross-*engine* comparison of texture appearance |
| B15's arithmetic hint | ≈128 m | the same saturation reading, done by hand |

**The two genuinely disagree by 3×, and this item does not average them — it says why the
mottling estimator is the weaker one.** Two reasons, both new:

1. **A7's calibration was a coincidence.** It reported "the recovered world period comes back
   1062 m against the authored 1024 m tile (3.7 %)" as proof the method reproduces a tile it was
   never told about. But the deck tiles' **authored UVs span 0.5 × 0.5 per 1024 m tile** (four
   quadrant origins in a 2×2 checker — read from `models.json`, 144/144 in both C1 and C4), so
   `cloudlayer.tif` repeats every **2048 m**, not 1024. The estimator locked onto the second
   harmonic; matching "the tile" meant nothing.
2. **Our own render is heavily mip-blurred at grazing angles and the original is not.**
   `.scratch/c21/mottling-AB.png` (contrast-stretched, same rows, same pose) shows the original's
   ceiling crisp and multi-scale and ours smoothed into broad bands. A zero-crossing rate on a
   blurred profile under-counts, which biases the *ratio* — and therefore K — upward. The
   fog-ramp fit reads a row **mean**, which no filter can move.

**Recommendation: `DeckCeilingHeight = 135 m` for C22/C25 — with an A/B at the controls before it
lands.** Confidence: high on the arithmetic, **medium on the decision**, because A7's `K = 400`
carries a user "it looks a lot better. approved." at the controls, and 135 m makes the apparent
mottling ~3× coarser. That is a visible change to an approved look, and it is the one thing in
this item a measurement cannot settle. C25 should shoot 135 / 400 at the river pose beside the
original and put it to the user.

### The per-population measurement protocol for C22/C23

1. **Mesh vs everything else — by texture, and it is legitimate.** `SHOT-21` bans
   `--tex-override` for `fvol`-vs-`cloudparent` because both wear `cloud1`/`cloud2`; the deck
   mesh wears **`cloudlayer.tif`** and neither other population does. Shoot each pose twice
   (natural + `--tex-override=cloudlayer.tif=ff0000`), build the mesh mask from the flat frame
   and take per-population means through it on the natural one — `.scratch/c21/population.py`
   does exactly this and printed the table above. ⚠ **C4 is the exception**: its deck wears
   `Sky1.tif`, which its **skydome also wears**, so on C4 separate by pose (from below the band
   the deck fills the sky) rather than trusting the mask.
2. **`fvol` clutter vs `cloudparent` — by altitude/position only** (`SHOT-21`, `A6`). C1's
   `cloudparent` geometry spans 1069.7–1875.6 m on a 1024 m X/Z grid at Y = 1107.2; the `fvol`
   slab is 970–1090.5. Below the band `A7`'s gate culls both, so every below-band box is pure
   mesh by construction. Keep both frames on the same side of the map edge (CAP-12's own caveat).
3. **Boxes**: `.scratch/c21/capboxes.py` — `under-L`/`under-R` (0.05–0.40 / 0.60–0.95 × 0.07–0.45),
   `tops-L`/`tops-R`/`tops-M`, `inside`. Fractional, HUD- and plane-free on the original side.
4. **Ladder**: `--freecam --chapter=C1 --det --mute --pos=-4974,{670,1040,1160,1700},-3861
   --direction=-1,0,0`, against `t20` / `t30.5` / `t33`+`t124` / `t97`. Add `--no-fog` to read a
   surface's own colour before the fog mix — that is what makes 212.7 → 170.6 a checkable step.

### Two things that are NOT this item's, recorded so they are not re-found

- **C4's deck renders untextured-white × its 192 vertex colour.** `--tex-override=Sky1.tif=808080`
  reads back 95 (= 128 × 192/255, so the vertex colour is applied) while the natural render is a
  flat **192** (= 255 × 192/255), i.e. `base_col` is white — and C4 ships `texture/sky1.png` as a
  16×16 **195**-gray while every `rtexture*/sky1.png` is pure **255** white. The product lands on
  192, which is exactly what the original measures, so **do not "fix" the texture without
  re-measuring** — a 195-gray base would put C4's deck at 147 and miss the original by −45.
- **The reach residual at the river pose is `K`, not brightness and not fog.** Fog saturation sits
  at `f·K/4000` whatever the deck's colour is, so C22 alone cannot move 64 px toward 19 px; only
  `K` can. `B15`/`B17` attributed the *luminance* half to `BL-118` correctly; the *elevation* half
  is this item's K.

### Files

`.scratch/c21/` — `shoot.ps1` + `shoot2.ps1` (the probe set, every pose documented in-file),
`measure.py` (flat/box/rows), `capboxes.py` (the matched HUD-free boxes), `population.py` (the
mesh-mask split), `fitfog.py` (the `f·h` fit **and its able-to-fail control**), `ceiling_h.py`
(a horizontal-ACF estimator that did **not** work — kept with its own negative result:
the original's high-frequency content has a screen-fixed ~40 px correlation length at every
elevation, so it is not perspective-scaled and cannot measure `h`), `mottling-AB.png`, and 20
renders with their per-shot `weather` log lines (METHOD-15). `docs/PLAN-overcast-match.md` (this section, the checklist line, and a pointer line in
`C25`). No engine code, no shader, no `weather.md`, no `PROJECT_CONTEXT.md`, no `backlog.md`.

### Original brief (kept for reference)

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

## C22 ☑ Implement the underside darkening

**Landed.** (2026-08-08) Exactly C21's mechanism, deck-local: `WorldBuilder.Add` now builds every
deck tile with `forceLit: isDeck` beside its existing `forceDoubleSided: isDeck`. `SceneBuilder`
threads `forceLit` through `BuildSubtree`/`GetMesh`/`BuildMesh` the same way as
`forceDoubleSided` (inherited by descendants, joins the mesh-cache key), and the one substantive
line is `bool lit = mesh.Lighting || forceLit;` — the deck's authored `lighting: false` no longer
gates off `ALBEDO *= csky_world_light`. This selects an existing lit+fogged shader **variant**
(the ordering-contract note in `SceneBuilder.cs`: `lighting`/`fog` pick shader text, not a
uniform), so it cannot perturb any surface that doesn't ask for it — confirmed below by the C4
control (byte-identical) and the dome/sprite controls (untouched code paths, unaffected).

### The four CAP-12 boxes — predicted vs measured vs original

`--freecam --chapter=C1 --det --mute --pos=-4974,{670,1040,1160,1700},-3861 --direction=-1,0,0`,
`.scratch/c21/capboxes.py` + `population.py`, before shot first on the pre-C22 build
(`.scratch/c21/`), after on this build (`.scratch/c22/`):

| box | original | before (ours) | **predicted (C21)** | **measured (C22)** | verdict |
|---|---|---|---|---|---|
| underside 670 m, `under-L`/`under-R` (fogged) | 169.7 / 168.7 | 201.2 / 195.9 | 171 ± 3 | **172.6 / 168.4** | ✓ Δ +2.9 / −0.3 |
| underside 670 m, deck's own colour (`--no-fog`) | — (arithmetic ref 168.9) | 212.7 / 207.2 | 170.6 | **170.4 / 165.9** | ✓ matches the arithmetic almost exactly |
| underside, CAP-12's own tight box (fogged, 79.8 % mesh) | 168.1 | 211.2 | 171 ± 2 | **170.6** (mesh-only 171.7) | ✓ Δ +2.5 |
| interior 1040 m, `inside` (0 % mesh — the whiteout overlay) | 249.8 | 242.3 | 242.3 — unchanged | **242.34 — byte-identical** | ✓ inert by construction |
| tops 1160 m, `tops-L`/`tops-R`/`tops-M` (0 % mesh) | 204.9–213.3 | 222.3 / 220.9 / 222.6 | unchanged | **219.3 / 210.5 / 222.2** | still open → `C23` (see note below) |
| tops 1700 m, `tops-L`/`tops-R`/`tops-M` (0 % mesh) | 182.7–201.2 | 205.8 / 217.7 / 219.6 | unchanged | **202.8 / 213.8 / 212.8** | still open → `C23` |

The underside rows are the item's own target and land exactly where C21 predicted, including the
`--no-fog` deck-own-colour control landing almost exactly on `210.54 × 0.802 = 168.9` — the
clearest confirmation that the mechanism is what C21 traced it to, not a coincidence of the fogged
mix. The interior box is byte-identical (0 % mesh, the whiteout overlay fully occludes it — inert
by construction, exactly as A2/A3's controls establish that pattern).

**⚠ The two tops boxes moved a little, where C21 predicted flat-zero.** `population.py`'s
flat-red mesh mask still reads **0.0 % mesh** in both boxes at both altitudes on this build (the
override's own math survives dimming: `255 × 0.802 ≈ 204 > 200`, the mask's own threshold, so a
darkened deck tile would still be caught if it were there) — so no *pure* deck-mesh pixel entered
either box. The few-unit movement (up to 10 on `tops-R` at 1160 m) is most likely translucent-edge
bleed: at 1160/1700 m the camera is above the `CLOUD_COVER` band centre, where `A7`'s regime puts
the deck as a world-fixed floor sitting *below* the camera, and these boxes sit in the lower 35–40
% of a level frame — close enough to the horizon to catch the floor's darkened colour showing
through a `cloudsprite` card's own soft alpha edge, a blend the flat-mask can't see (it only
classifies *fully*-red pixels). This does not change the box's own verdict — both were already
predicted to miss the bar and wait on `C23` — and it is recorded here rather than chased, per this
item's own scope (`C23` owns the mesh↔sprite blend and should fold this in).

### Controls — C4, the dome, the sprite field

- **C4 (`Sky1.tif` deck, `WorldLight` clamps to `1.0`): byte-identical**, no special-casing. Same
  pose, same box, before vs after: below-band `-4974,900,-3861` **190.63 → 190.63** (identical
  RGB triple), above-band `-4974,1300,-3861` **212.49 → 212.49** (identical RGB triple). The
  `c4-snow` golden did not move either (see below) — three independent readings, none moved,
  because the clamp does the work exactly as C21 said it would.
- **Dome (`horizon`, untouched code path): unaffected.** Pinned above-deck pose
  `-7323,1192,-3829` / `0,0,-1`, apex box (rows 0–150, full width): **75.43** fog-on and
  `--no-fog` alike (byte-identical to each other, matching B12/B16's own "above the fog altitude
  ceiling" finding) — consistent with the on-record **74.3** original / **~75.3–75.5** ours.
  `BuildHorizon` never passes `forceDoubleSided`/`forceLit`, so this is expected, not merely
  measured.
- **`cloudsprite` field (`Effects/FogVolumeClutter`, a wholly separate builder): unaffected.**
  It never calls `WorldBuilder.Add` or `SceneBuilder.BuildSubtree` with `forceLit`, so nothing in
  its own code path changed; the tops-box movement above is read-through from the deck floor, not
  a change to the sprite population itself (unchanged sprite counts, below).

### River pose — the fogged ceiling moves toward the original's band, the K residual does not

`-7323,192,-3829` / `-0.997,-0.1,0.070`, `.scratch/c21/fitfog.py` (horizon row 301, this render's
own pitch — `SHOT-23`), before on `.scratch/c21/river-fog.png`, after on `.scratch/c22/`:

| reading | before | after | original |
|---|---|---|---|
| `f·h` fit's recovered `L0` (the ceiling's own unfogged colour, read from the fog ramp alone) | **210.63** | **170.00** | 168.1–169.2 (C21's own fit on the original still) |
| row-mean, elevation 71–81 px (B15's HUD-free column bands) | 178.6 / 183.5 | **175.4 / 173.5** | 166–175 flat band (C21) |
| row-mean, elevation ≤ 41 px (the rim/dome-stripe region) | 158.7 / 144.4 / 164.0 / 165.2 / 161.4 | **158.7 / 144.4 / 164.0 / 165.2 / 161.4 — byte-identical** | — (this is `K`, `C25`) |

`L0` lands almost exactly on the predicted 168.9, and the upper part of the ceiling gradient moves
visibly toward the original's flat 166–175 band. **Rows below elevation ≈ 41 px are byte-identical
before and after** — that region is the dome-wall sky stripe C21 attributed to `K`
(`DeckCeilingHeight`, still 400 m, still `C25`'s), not to the deck's own brightness, and this item
correctly does not move it. Stated, not chased, exactly as the brief asked.

### Tests, goldens and the 8-chapter regression

`.\RunTests.ps1` — build PASS (0 warnings), **units 682/682**, **engine 26/26, errors clean**,
goldens **5 moved, 0 broken of 13**. Exit 1 is the golden stage alone. Goldens were clean (0
moved) going into this item — `B17` re-pinned Wave B's movers and `C21` touched no code — so this
list is this item's own, not a `GOLD-8` conflation:

| golden | moved? | why |
|---|---|---|
| `c1-waterfall`, `c1-flight`, `c1-destroy-effects` | **moved** | C1, deck in frame, `WorldLight` 0.802 ⇒ deck visibly darkens |
| `c1c-rain` | **moved** | C1C, same deck signature, `WorldLight` 0.784 |
| `c2b-rain` | **moved** | C2B, same deck signature, `WorldLight` 0.784 |
| `c4-snow` | **ok** | C4's deck is present but `WorldLight` clamps to `1.0` — a no-op by construction, confirmed above at the pixel level too |
| `c1b-night-sea`, `c2-city`, `c3-island`, `c5-city-night`, `c1-crash`, `viewer-bhawk`, `empty-stage` | **ok** | no deck (`C1B`/`C2`/`C3`/`C5`), no chapter world (`viewer-bhawk`/`empty-stage`), or no deck in frame (`c1-crash`'s straight-down frame, per `A2`) |

**Not re-pinned — `C24` re-pins once**, per the plan's own convention (`GOLD-1`/`GOLD-8`).

8-chapter `--freecam --chapter=<X> --frames=20` regression: **zero errors in all eight**, every
census identical to the on-record numbers (decks C1/C1C/C2B 144 tiles @ y=960, C4 144 @ y=1050;
clusters C1 28 · C1B 70 · C1C 30 · C4 45; sprites C1/C2B/C4 22,201 · C1C 22,748 · C5 16,170;
gamez-node/mesh-instance counts per chapter unchanged) — a material/lighting change touches no
geometry or placement, so an unchanged census is the expected result, not a coincidence.

### Files

`CSVM/src/Mech3/SceneBuilder.cs` (`forceLit` threaded through `BuildSubtree` (public + private
overloads), `GetMesh`, `BuildMesh`; `_meshCache` keyed on `(Model, Force, ForceLit)`),
`CSVM/src/Mech3/WorldBuilder.cs` (`Add` passes `forceLit: isDeck`), `docs/architecture.md` (both
module entries), `docs/PLAN-overcast-match.md` (this section, the checklist line).
`.scratch/c22/` — `shoot.ps1` (the after-build probe set, mirroring `.scratch/c21/shoot.ps1` +
`shoot2.ps1` pose-for-pose), `regress.ps1` (the 8-chapter census sweep), `repro.ps1` (the
determinism check), plus every PNG/`.weather.txt` the tables above cite.

**⚠ Traps, addressed.** The deck-follow behaviour (`GameSession`/`WeatherRig.Tick`) needed no
special handling: `forceLit` is baked into the built material at world-build time, not read per
frame, so it survives the deck's X/Z/regime follow the same way `forceDoubleSided` already does.
Nothing here is keyed to face normals.

## C23 ☑ Re-measure the tops per population; blend the mesh↔sprite cut

**Landed.** (2026-08-09) **No engine code, no shader, no `fogvol.md` rule change.** This item is a
measurement, four disproofs and a fork. The tops caveat is resolved — with a *correction* to how
`C21`/`C22` read it — and both halves of the cut are now traced to an exact mechanism and
quantified. What is NOT landed is the fix, because the two mechanisms that survive the data fit it
equally well and produce visibly different renders (ground rules; the item's own brief says stop
rather than pick by taste).

### ⚠ BLOCKED — the fork, and what `C24` must not do

**→ Taken by the user on 2026-08-09: `M-a`, landed the same day. The analysis below is left
exactly as it was written — it is the record the choice was made on — and the landing, with its
own verification tables, is the `Fork resolved` block at the end of this section.**

The from-above world reads **~210 and flat** in every original above-band frame; ours reads
**222.7 cards over a 168.9 floor**. Two mechanisms close that, both consistent with everything
measured below, and **they differ visibly**:

| | what it says | what it looks like |
|---|---|---|
| **M-a** | the card's own rendered value is **~209**, not 222.7 (the original's saturated plateau measures 208.88 / 209.16 in two independent frames) — **and** the above-band floor is un-dimmed to 210.5 | the field stays: a cloud carpet with ragged tops above the band, one flat tone with the floor |
| **M-b** | the `fvol` field is **not drawn above the band at all** — the mirror of `A7`'s below-band gate — and the from-above sheet is the un-dimmed deck mesh at 210.5 | the carpet and the ragged tops above the band go away entirely; a smooth sheet, which is what `t97` shows |

Both need the **same second half**: the above-band deck floor must not carry `C22`'s `WorldLight`
dimming (evidence below — the original's above-band frames contain **no pixel below `FOG_COLOR`
175**, and ours contain 168.9 ones). Neither half is landable alone: un-dimming the floor by itself
moves every tops box **1–15 units further from** the original, because the sprite half is still
wrong.

⇒ **`C24` cannot close `BL-118`'s `PT-42`(a) symptom.** Either the user picks M-a/M-b at the
controls (the A/B pair is `.scratch/c23/AB-1700-nearfield.png`, and shooting M-b is a one-line
probe: invert `WeatherRig.DeckRegime`'s `CloudsVisible`), or `PT-42`(a) and the above-band deck
brightness are minted as a fresh item and `BL-118` closes on its underside half only.

### The instrument the tops caveat actually needed — and the correction it forces

`C21`/`C22` read the tops boxes as **0.0 % deck mesh** and concluded they were a pure `cloudsprite`
measurement. That reading is an artifact of the instrument: `population.py`'s flat-red mask
classifies only **fully** red pixels, and above the band the deck is behind alpha-blended cards
almost everywhere, so a blended deck pixel counts as "not mesh". Shooting the same pose twice with
the deck texture flattened to **black** and to **white** (`--no-fog`, so the only difference is the
deck's own colour) gives, per pixel, `T = (L_white − L_black) / (255 · WorldLight)` — the exact
fraction of that pixel the deck contributes through whatever alpha sits in front of it
(`.scratch/c23/bleed.ps1` + `bleed.py`):

| pose | `tops-L` | `tops-R` | `tops-M` | whole lower 45 % |
|---|---|---|---|---|
| above-deck 1192 m | **13.6 %** | **14.4 %** | **6.2 %** | 7.3 % |
| CAP-12 1160 m | 3.4 % | 13.3 % | 0.5 % | 5.4 % |
| CAP-12 1700 m | **35.2 %** | 14.1 % | 11.7 % | 16.6 % |

⇒ **`BL-118`'s caveat resolves as "65–97 % sprite, 3–35 % deck", not "100 % sprite".** `C22`'s own
"the two tops boxes moved a little where C21 predicted flat-zero … most likely translucent-edge
bleed" is confirmed and now has a number; the movement is exactly this weight times `C22`'s own
−42 on the deck. Keep the vocabulary: every row below is labelled with which population it is.

### Per-population tables

**The pinned above-deck pose**, `--pos=-7323,1192,-3829 --direction=0,0,-1`, natural vs
`--tex-override=cloudlayer.tif=ff0000`, boxes `.scratch/c21/capboxes.py`:

| box | ours (all) | deck weight | original | Δ |
|---|---|---|---|---|
| `tops-L` | 210.29 | 13.6 % | 169.00 | +41.3 ⚠ |
| `tops-R` | 207.60 | 14.4 % | 169.42 | +38.2 ⚠ |
| `tops-M` | **215.45** | 6.2 % | **207.75** | **+7.7** |

⚠ **`tops-L`/`tops-R` are NOT comparable at this pose and the +41 is not brightness** (`SHOT-23`).
The original still's pitch differs: its sky stays 72–80 down to row 0.53 and its cloud-top line
sits at rows 0.60–0.73, so those two boxes sample *cloud-tops-against-dark-sky* on the original
side and solid sheet on ours. Only `tops-M` (the near, bottom-centre box) samples the same thing in
both, and it reads **+7.7**. The comparable ladder is CAP-12's, below.

**CAP-12's own level ladder**, `--pos=-4974,{1160,1700},-3861 --direction=-1,0,0`, against the
stills at the matching altitudes:

| box | ours 1160 | orig `t33` 1133 m | orig `t124` 1208 m | ours 1700 | orig `t97` 1698 m |
|---|---|---|---|---|---|
| `tops-L` | 219.31 | 204.88 | 213.01 | 202.82 | 182.68 |
| `tops-R` | 210.48 | 194.57 | 213.33 | 213.78 | 183.37 |
| `tops-M` | 222.23 | 213.33 | 209.07 | 212.79 | 201.16 |

**The 1160 m rung is essentially at the bar** against the altitude-matched `t124` (+6.3 / −2.8 /
+13.1); **the 1700 m rung fails everywhere** (+20.1 / +30.4 / +11.6). The deficit grows with
altitude, i.e. with how much *distance* is in the frame — which is the shape of the finding.

### The plateau: ours is 222.7, the original's is 209 — measured five ways

Our saturated card value is **222.7 exactly** — `p90 = 222.7` at every above-band pose, matching
`C21`'s prediction `236.65 × 240/255` to the decimal, so our render is faithful to the naive
reading of the data. The original never reaches it. In the *near-field* patch
(x 0.30–0.45, y 0.90–0.99 — closest surface, HUD-free, fog ≈ 0):

| frame | mean | sd | min | max |
|---|---|---|---|---|
| orig `t124` 1208 m | **208.88** | 0.54 | 207 | 210 |
| orig `t59` 1219 m | **209.16** | 1.77 | 202 | 213 |
| orig `t97` 1698 m | 201.42 | 2.12 | 195 | 207 |
| **ours 1160 m** | **222.18** | 0.38 | 221 | 223 |
| **ours 1700 m** | 211.49 | **14.37** | **168** | 223 |
| ours 1192 m | 216.15 | 1.68 | 211 | 219 |

Whole-frame `p99`, HUD/plane excluded: original **213–216** at `t33`/`t59`/`t124` and **max 215**
on the pinned above-deck still; ours 224–229. The original's cloud pixels are also perfectly
neutral (`RGB(214,214,214)`, `(215,215,215)`, `(216,216,216)` are the three most common values in
its near box) while ours carry the texture's own `(239,235,239)` tint. **Nothing in five
independent original above-band frames renders at 222.7.**

### Why our sprite tops read too bright — the four candidates, all refuted on data

1. **A `cloudsprite` opacity like `cloudparent`'s 0.6 — does not exist.**
   `extracted/C1/zrdr/clouds.zrd.json`, read in full, is ONE `ANIMATION_DEFINITION`: `NAME`
   `cloudparent#`, `ACTIVATION` `ON_STARTUP`, `EXECUTION_BY_RANGE` 1900, `RESET_TIME` −1, and both
   its `RESET_STATE` and its `LOOP{-1}` sequence are `OBJECT_OPACITY_STATE` `NAME cloudparent`
   `STATE ON 0.6`. It never names `cloudsprite`, and **C1 is the only chapter that ships a
   `clouds.zrd` at all**. A sweep of every `zrdr/*.json` in all eight chapters finds `cloudsprite`
   named ONLY in the eight `fogvol.zrd` files (plus `interp.json`'s `LoadGameGen
   c1\horizon\cloudsprite1.flt` load lines). The `cloudparent` 0.6 is already applied and
   range-gated in our build — the probe log prints `anim: EXECUTION_BY_RANGE reached - starting
   cloudparent# at 1864 m (range 1900 m)` (`METHOD-15`).
2. **`WorldLight` on the sprites — refuted, and the data varies the flag per chapter.**
   C1/C4 cards author `lighting: false`; **C1C/C2B author `lighting: true`** (`fogvol.md`), and we
   already honour both. Applying 0.802 to C1's cards puts them at **178.6**, *below* the
   original's own 204.9–213.3 at the 1160 m rung — it would overshoot by more than the error it
   fixes. The able-to-fail control passes as authored: C1C at the same above-deck pose measures
   `tops-L`/`tops-R`/`tops-M` = **168.62 / 196.04 / 165.79** against its predicted field value
   `222.7 × 0.784 = 174.6` — our C1C field is dark exactly where its data says it should be.
   (This also disposes of the B15 night worry the brief raised: no sprite change is proposed, so
   `C1B`'s moonlit numbers cannot move, and `c1b-night-sea` is byte-identical below.)
3. **Fogging the sprites — refuted three ways, and it was the tempting one.**
   The *shape* fits beautifully (the original's from-above sheet ramps smoothly from `FOG_COLOR`
   175 at range to ~209 near; ours steps +35 in 30 rows at the `far_fade` edge). The data says no:
   (a) the same clutter reader's **tree** templates — `firtree1`, `firtree2`, `dougfirtree1`,
   `bush1`, `bush2` — all author **`fog: true`**; (b) the world's own **placed** cloud facades
   author `fog: true` with vertex colour 255 (C1's 52 `cloudparent`/`o7xx` children, C2B's 108);
   (c) `B16` verified the flag is honoured for the dome. `fog: false` on the `fvol` card is a
   deliberate per-model authored distinction inside the very system that authors `fog: true` next
   to it — **do not re-chase this.**
4. **Carrying the field up with the relocated deck — refuted by CAP-12's own altimetry.**
   The authored invariant (`fogvol.md`: deck tiles 10 m *below* the `fvol1`–`fvol9` slab floor,
   all four deck chapters) suggests moving the field by the same `bandCentre − authoredDeck` that
   `A7` moves the deck: +87 m for C1. That puts card tops at **1177–1277 m** against CAP-12's
   measured "clear above by ~3700 ft (**1128 m**)". Dead.

### ⚠ The C1C frame the user reported holds THREE cloud tones, and the widest gap is not the deck

Re-shot at C1C's own above-band pose (`--chapter=C1C --pos=-7323,1192,-3829 --direction=0,0,-1`,
110 m over its 1082.5 band centre — `.scratch/c23/c1c-abovedeck-nat.png`). C1C carries **106**
placed cloud facade models wearing `cloud1`/`cloud2` beside its **2** `fvol` template cards, and
the gamez gives the two populations *opposite* `lighting` flags:

| population in C1C | authored | our rendered value | measured in the frame |
|---|---|---|---|
| placed cloud facades (`cloudparent`) | vcol **255**, `lighting: false`, `fog: true` | 236.65 | **235.25** (bright right column) |
| `fvol` `cloudsprite1/2` cards | vcol **240**, `lighting: **true**`, `fog: false` | **174.6** (`× WorldLight` 0.784) | — |
| deck floor (`C22`) | `cloudlayer.tif` 197.42 `× 0.784` | 154.8 | 168.2 mean / 151.0 min (lower sheet) |

**That is an 80-unit spread across three populations in one frame, and 62 of it is between the two
CLOUD populations, not between cloud and deck.** C1 does not have it (both of its cloud
populations author `lighting: false`; 236.65 vs 222.7 = 14 units); C1C and C2B both do, because
their `fvol` card is the only cloud model in the chapter that authors `lighting: true`.

⇒ **The user's C1C report is most likely this split, not the mesh↔sprite boundary at all.** It is
a *fifth* candidate for the tops question and it is same-chapter and data-level: if the `lighting`
flag on a `Facade` cloud card is not a `WorldLight` gate, C1C's field is 222.7 and sits 14 units
from its own placed clouds — the identical relationship C1 has. Not tested here (it would move
C1C/C2B/C5 and nothing about C1's two reference stills), but it belongs in whatever item inherits
`PT-42`(a), and it must be settled before anyone reads a C1C frame as evidence about the deck.

### The cut — two independent causes, both quantified

**(a) Geometric: `A7`'s above-band floor sits INSIDE the card band, so it slices every near card.**
`A3` top-anchors each card's *centre* at the slab top **1090.5 m** (+ `perp_dist_range` −5…+10)
with half-height `66.14 × scale`, `scale ∈ [0.95, 1.5]` ⇒ **62.8–99.2 m** ⇒ **card bottoms
991–1028 m**. `A7`'s above-band floor is the `CLOUD_COVER` centre, **1047 m**. The opaque floor
therefore cuts **19–56 m** off the bottom of every card, and each cut is a straight silhouette
across a soft sprite — *the* "many hard lines". Visible directly in
`.scratch/c23/abovedeck-mesh.png` (the flat-red deck shows through in straight-edged wedges) and
in `AB-cut-lines.png`. This is `A7`'s own finding 1 handed forward — "if those plates read badly
at the controls, the item is the deck-mesh↔sprite intersection (C23)".

**(b) Brightness: `C22` did not soften C1's cut — it CREATED it, and it barely moved C1C's.**

| chapter | deck floor before `C22` | after `C22` | field | gap before → after |
|---|---|---|---|---|
| **C1** (cards `lighting: false`) | 210.5 | **168.9** | **222.7** | 12 → **54** |
| **C1C** (cards `lighting: true`) | 197.4 | **154.8** | **174.6** (`222.7 × 0.784`) | 23 → **20** |
| **C2B** (cards `lighting: true`) | 186.3 | **146.0** | 174.6 | 12 → 29 |
| **C4** (`WorldLight` clamps to 1.0) | — | unchanged | 222.7 | unchanged |

The user's report was at **C1C** and pre-`C22`; its own gap is now the smallest of the four. C1's
is the one that is 54 units wide today. ⚠ Per-chapter `cloudlayer.tif` means differ and were not
on record: **C1 210.54, C1C 197.42, C2B 186.27** (C4 wears `Sky1.tif`) — `C21`'s "C1C/C2B would go
212 → 166.5" used C1's texture for all three.

**The evidence that the above-band floor should not be dimmed at all.** Fog can only pull a
surface *toward* `FOG_COLOR`, so a surface whose own colour is 168.9 can never render above 175.
In the outboard HUD-free column (x 0.00–0.13, y 0.65–0.99):

| frame | p1 | p5 | median | % below 180 |
|---|---|---|---|---|
| orig `t97` 1698 m | **175.0** | 175.0 | 186.7 | 32.7 % |
| **ours 1700 m** | **166.2** | 176.0 | 217.7 | 17.1 % |
| orig `t124` 1208 m | **208.0** | 209.0 | 213.0 | 0.0 % |
| ours 1160 m | 209.9 | 211.8 | 219.7 | 0.0 % |

**The original's above-band frames bottom out exactly at `FOG_COLOR` and never below it. Ours go
to 166** — that tail is `C22`'s dimmed floor showing between the cards, and it is also what makes
our 1700 m near-field `sd` **14.37** against the original's **2.12**. `C22`'s mechanism is
verified from BELOW (the original's underside 167.7, and `C21`'s free-parameter fog fit recovering
168.1–169.2 from the river still) and is contradicted from ABOVE. Under `A7`'s own regime model
those are two different objects — below the band a ceiling carried at `camera.y + K`, above it a
world-fixed floor — so "dimmed below, authored `lighting: false` (i.e. raw 210.5) above" is a
regime rule, not face-dependent lighting. `rig.Deck` is already per rig, so it is implementable
per camera; that is the second half of the fork above, not landed here.

### Verification

- **`.\RunTests.ps1`** — build PASS (0 warnings), **units 682/682**, **engine 26/26, errors
  clean**, goldens **6 moved, 0 broken of 13**; exit 1 is the golden stage alone. **This item moved
  none of them** — it changes no code (`git diff` empty over `CSVM/`). The six are the standing
  un-repinned set: `C22`'s five (`c1-waterfall`, `c1-flight`, `c1-destroy-effects`, `c1c-rain`,
  `c2b-rain`) plus `C25`'s `c4-snow` (`K` 400 → 135; C4's golden camera sits at y = 958, below its
  1050 band centre, so it is in the *ceiling* regime `C25` changed) — `GOLD-8`, still `C24`'s to
  re-pin. `c1b-night-sea` **ok**, which is the B15 night control: no sprite rule changed.
- **The baseline replays byte-for-byte.** `.scratch/c23/cap12-{1160,1700}-nat.png` reproduce
  `C22`'s own numbers to the decimal (219.31 / 210.48 / 222.23 and 202.82 / 213.78 / 212.79),
  which is the `METHOD-3` check that this item measured the build `C22` left behind.
- **Probes worth human eyes**: `AB-1700-nearfield.png` (orig `t97` | ours 1700 m — the original's
  sheet is smooth, ours is a lumpy carpet cut by straight lines), `AB-abovedeck.png`,
  `AB-cut-lines.png` (natural over flat-red deck, the same frame).
- Not run, deliberately: the 8-chapter freecam regression and the C4 tops spot check. Both verify
  a *change*; with zero code changed they can only reproduce `C22`/`C25`'s own results
  (`METHOD-10`).

### Files

`.scratch/c23/` — `shoot.ps1` (the pose set: the pinned above-deck pose natural + flat-deck, the
CAP-12 1160/1700 rungs, C1C's own above-deck pose), `bleed.ps1` + `bleed.py` (the deck-weight
instrument and the re-composite predictor), `AB-*.png`, `runtests-output.txt`, and every PNG the
tables cite. `docs/PLAN-overcast-match.md` (this section, the checklist line, a pointer in `C24`).
**No engine code, no shader, no `docs/formats/`, no `docs/architecture.md`, no
`PROJECT_CONTEXT.md`, no `backlog.md`** — the sprite render rule did not change, so `fogvol.md`'s
entries stand exactly as written.

### **Fork resolved (user, 2026-08-09): M-a landed.**

Both halves, exactly as mocked: the above-band deck floor stops carrying `C22`'s `WorldLight`
dimming, and the `fvol` cards darken to the original's plateau. Nothing else moved — the below-band
gate, `K`, the whiteout, the dome and `cloudparent` are untouched, and every below-band frame is
**bit-identical** to the build this landed on.

**The mechanism — `DeckRegime` gains a third column, and the deck gains a second mesh.** `C22`
baked lit-ness into the deck tile's material at build time (a shader VARIANT, not a uniform — the
ordering contract in `SceneBuilder`), so "dimmed below, undimmed above" needs a runtime switch.
`WorldBuilder.Add` now asks `SceneBuilder.SharedMesh` for the deck tile's mesh in **both**
`forceLit` variants as it builds each tile — the mesh cache already keys on that flag, so the
dimmed one it hands back IS the resource the built `MeshInstance3D` carries — and publishes the
pairs as `CloudDeckUndimmedMeshes`, keyed by the dimmed mesh's `Rid`. `WeatherRig.DeckRegime`
returns `(DeckY, CloudsVisible, DeckDimmed)` — the third column is the new one — from the same
`cameraY < bandCentre` test, and
`Tick` writes the chosen variant onto that rig's own deck instances beside the Y it already writes.
Per **instance**, never per material: a splitscreen pane's deck is its own node copy sharing these
resources, so two panes on opposite sides of the band hold different variants at the same instant —
the same requirement `A7` met for the cloud gate with a per-camera cull mask, met the same way. The
variants differ only in the shader the surface picked (same vertices, same AABB, same instance
uniforms), the swap is one property write per tile and only on a change, and it is inert in a
chapter whose `WorldLight` is 1.0 (C4) by construction. Census, printed once per session:
`deck lighting: 144 of 144 deck tile(s) carry an undimmed twin` (C1 and C4 alike) — `0 of 144` is
what a broken lookup would say, which is why it is said out loud (`DIAG-15`).

**The `fvol` half is one constant, marked TUNE.** `FogVolumeClutter.BuildCardMesh` scales the
card's authored vertex colour by **225/240** (RGB only — alpha is the card's coverage, and scaling
it would thin the overcast instead of darkening it). `236.65 × 225/255 = 208.8` against the
original's measured plateau of **208.88 / 209.16**. ⚠ **It has NO decoded mechanism** — this
section refuted all four candidates on data — so it is a *calibrated match to measured originals*,
not a decode, and it says so in the constant's own comment: if a mechanism is ever found it
REPLACES this constant rather than joining it. `fvol` cards only; the placed `cloudparent` facades
keep their own authored rules (vcol 255 + the range-gated 0.6, `A6`).

**Method.** The baseline is not `.scratch/c22`/`.scratch/c23` — `C25` moved `K` 400 → 135 in
between. Every pair below was shot minutes apart on this machine: the working tree reverted
(`git checkout -- CSVM CSVM.Tests`) and rebuilt for the `base` set, the patch re-applied and
rebuilt for the `ma` set, same script, same flags, `--det --mute` throughout (`METHOD-3`,
`METHOD-17`; `git diff --stat` is identical either side of the round trip). `.scratch/c23fork/`
holds both sets, `verify.ps1`, `report.py` and `ma.patch`.

#### The fork's own statistics — mock, landed, original

`metrics.py`'s four, at the two poses the fork was mocked at (`.scratch/c23fork/`; card `p90` and
the floor mask over the lower 45 % of frame, near-field patch x 0.30–0.45 / y 0.90–0.99, outboard
column x 0.00–0.13 / y 0.65–0.99):

| statistic | before (`C22`+`C25` build) | the M-a **mock** | **landed** | original |
|---|---|---|---|---|
| **above-deck 1192 m** — card `p90` | 222.65 | 209.24 | **209.24** | **209** (plateau 208.88 `t124` / 209.16 `t59`) |
| — deck floor (flat-red mask) | 173.62 | 213.42 | **213.35** | — |
| — **floor ↔ card gap** | **+49.03** | −4.18 | **−4.11** | — |
| — near-field mean / sd | 216.15 / 1.68 | 208.24 / 0.02 | **208.24 / 0.02** | 208.88 / 0.54 (`t124`) |
| — outboard `p1` | 178.00 | 207.41 | **207.41** | 208.0 (`t124`) |
| **1700 m** — card `p90` | 222.65 | 217.24 | **217.24** | — |
| — floor ↔ card gap | +45.35 | −1.79 | **−1.79** | — |
| — near-field mean / **sd** | 211.49 / **14.37** | 208.84 / 1.74 | **208.86 / 1.69** | 201.42 / **2.12** (`t97`) |
| — outboard `p1` / `p5` / % < 180 | **166.23** / 176.00 / 17.1 | 176.00 / 176.00 / 12.0 | **176.00 / 176.00 / 11.9** | 175.0 / 175.0 / 32.7 (`t97`) |
| **1160 m** — near-field mean / sd | 222.18 / 0.38 | — | **208.66 / 0.08** | 208.88 / 0.54 (`t124`) |

**Every target the mock set is met to ±0.1**, and the two headline defects are gone rather than
reduced: the 49-unit floor↔card step is now −4 (the floor reads a hair *brighter* than the cards,
which is what a single flat tone looks like), and the **sub-`FOG_COLOR` tail is gone** — our
outboard `p1` was 166.2, i.e. 9 units below a fog colour nothing can render under; it is now
**exactly 176.0**, the fog colour itself, which is the original's own floor. The near-field `sd`
at 1700 m falls 14.37 → 1.69 against the original's 2.12. The residual differences from the mock
(≤ 0.06 on every row) are the mock's own realtime frames against these `--det` ones, not a
different implementation: the mock was shot without `--det`, so its sky UV scroll and C4's snow
sit at a different phase.

#### Per-population tops boxes — `C23`'s own table, re-measured

`.scratch/c21/capboxes.py`, same boxes, same poses. ⚠ `tops-L`/`tops-R` at the pinned above-deck
pose remain **not comparable** to that original still (`SHOT-23`: its pitch puts cloud-tops against
dark sky in those two boxes); `tops-M` is the comparable one, and the CAP-12 ladder is the
comparable ladder.

| pose / box | before | **landed** | original | Δ before → **after** |
|---|---|---|---|---|
| above-deck `tops-M` | 215.45 | **208.23** | 207.75 | +7.7 → **+0.5** |
| above-deck `tops-L` / `tops-R` | 210.29 / 207.60 | **209.79 / 207.91** | 169.00 / 169.42 | ⚠ not comparable |
| 1160 `tops-L` | 219.31 | **209.57** | 213.01 (`t124` 1208 m) | +6.3 → **−3.4** |
| 1160 `tops-R` | 210.48 | **210.71** | 213.33 | −2.8 → **−2.6** |
| 1160 `tops-M` | 222.23 | **208.67** | 209.07 | +13.1 → **−0.4** |
| 1700 `tops-L` | 202.82 | **197.53** | 182.68 (`t97` 1698 m) | +20.1 → **+14.9** |
| 1700 `tops-R` | 213.78 | **212.33** | 183.37 | +30.4 → **+29.0** |
| 1700 `tops-M` | 212.79 | **208.42** | 201.16 | +11.6 → **+7.3** |

**The 1160 m rung is inside ±10 on all three boxes and inside ±3.5 on all three against the
altitude-matched `t124`.** The 1700 m rung improves on every box and **still fails `tops-L`/`tops-R`
(+14.9 / +29.0)** — that is the *distance* half of the finding this item recorded, unchanged by
M-a: at 1700 m the frame is mostly far field, where the original ramps from `FOG_COLOR` over
~240 rows and our `far_fade` rim steps. M-a was never the fix for that, and it is not claimed as
one; `C24` inherits it as a residual with its own numbers.

#### The invariants — what must NOT move, and did not

| check | result |
|---|---|
| below-band `underside-fog` / `underside-nofog` / `river-fog` / `cap12-670` nat + flat-red | **bit-identical** — mean abs diff 0.000, max 0, **0 px changed** on all five |
| the flip ladder 1035 / 1046 / 1048 / 1060, within the landed build | **bit-identical to each other**, frame sd **0.00** on a mean of 242.34 — a flat whiteout pane |
| the same four rungs, **base vs landed** | **bit-identical**, plus 1025 — the swap is unobservable from 1025 to 1060 |
| ladder 1075 (whiteout core ends at 1062), base vs landed | mean abs diff 2.33, 97.9 % px — the change legitimately shows once the core stops hiding it, which is the able-to-fail leg (`METHOD-9`) |
| **all 13 goldens**, base build vs landed build | **every hash identical**, movers and `ok`s alike — M-a moves no golden at all (below) |
| 8-chapter `--freecam` regression | **zero errors in all eight**; every census unchanged (decks C1/C1C/C2B 144 @ 960, C4 144 @ 1050; clusters 28 · 70 · 30 · 45; sprites C1/C2B/C4 22,201 · C1C 22,748 · C5 16,170; per-chapter gamez-node and mesh-instance counts identical) |
| `.\RunTests.ps1` | build PASS (0 warnings), **units 683/683** (682 + this item's new `DeckRegimeTests` case), **engine 26/26, errors clean**, goldens **6 moved, 0 broken of 13**; exit 1 is the golden stage alone |

**The flip is still invisible, and now it hides two jumps instead of one** — the deck's altitude
AND its lit-ness change at 1047 m, and the four rungs spanning it render the same pixels. `A7`
asserted the first; the new `DeckRegimeTests` case asserts that both consequences come off the
same predicate, so nothing can flip one of them a metre early.

**Goldens: 6 moved, and none of them by this item** (`GOLD-8`). The golden stage was run on the
reverted build too, and all thirteen hashes match the landed run's line for line — including the
six movers' `actual` values (`c1-waterfall` `c81a000a…`, `c1c-rain` `81e9dccd…`, `c2b-rain`
`57f097e1…`, `c4-snow` `421ead8f…`, `c1-flight` `9e162b5a…`, `c1-destroy-effects` `0d749511…`).
The six are the standing un-repinned set — `C22`'s five plus `C25`'s `c4-snow` — and **`C24`
re-pins**, as before. That every golden camera sits below its band is exactly why: M-a is inert
below the band by construction, which the bit-identical below-band poses measure directly.
`viewer-bhawk` reported `ok` without hanging on this run (`BL-320` did not bite).

#### Controls

- **C1B night — byte-identical, and the expected darkening does NOT happen.** `--chapter=C1B
  --pos=-5406,55,-7200 --direction=-0.391,0,-0.921` (`B15`'s own pose): `mean|d| = 0.000, max 0,
  0 px changed`; the sky box reads `p90` **158.17** on both builds and the `c1b-night-sea` golden
  is `ok`. The prediction going in was that C1B's moonlit tops would fall with everything else
  (`B15`'s 187.5 → ≈176). **They cannot: C1B ships no `fvol` volumes at all** (`FogVolumeTests`
  `C1B "0|-|206.25|bare|cloudsprite:absent"`, and its freecam census prints no `fogvol clouds`
  line), so its moonlit cloud population is the **70 placed `cloudparent` facades** — ordinary
  world geometry, which this TUNE does not touch. The night worry `C23` raised is therefore
  answered by absence rather than by margin, and `B15`'s 187.5 against the original's 155–182 band
  stands exactly where `B15` left it.
- **C1C / C2B — the cards do darken, and the three-tone split gets WORSE.** Both author their
  `fvol` cards `lighting: true`, so their field renders `222.7 × 0.784 = 174.6` and the TUNE takes
  it to **163.7**; measured, the card plateau in the outboard column reads **163.24 / 163.83** at
  C1C and **163.24** at C2B — the prediction to a tenth. Above the band their deck floor un-dims
  with C1's: C1C **154.8 → 195.8** measured (its own `cloudlayer.tif` mean 197.42, lightly fogged),
  C2B → **180.0** (mean 186.27). So C1C's one frame now holds `cloudparent` facades **235.25**, an
  undimmed floor **195.8** and cards **163.7**:

  | C1C above-band | before | **after M-a** |
  |---|---|---|
  | cloud-population spread (facades ↔ `fvol` cards) | 60.7 | **71.6 — wider** |
  | deck floor ↔ cards | −19.8 (floor darker) | **+32.1 (floor brighter)** |

  ⚠ **That is a real worsening at C1C/C2B and it is stated, not tuned around.** It is the *fifth*
  candidate this section already minted — if the `lighting` flag on a `Facade` cloud card is not a
  `WorldLight` gate, C1C's field is 222.7 → 208.8 with the TUNE and sits 26 units under its own
  placed clouds instead of 71. That question moves C1C/C2B/C5 and nothing about C1's two reference
  stills, so it is **`C24`'s to mint**, not this landing's to guess at. C1C's `tops` boxes move
  168.62 / 196.04 / 165.79 → **170.90 / 199.50 / 176.57** (the undimmed floor outweighs the darker
  cards there).
  ⚠ **Instrument, for whoever reads those floor numbers:** `population.py`'s flat-red mask
  (`r > 200`) can only see an **undimmed** deck in a 0.784-`WorldLight` chapter — `255 × 0.784 =
  200.0` is exactly the threshold — so C1C/C2B read `0.0 %` mesh before and `6.8 %` / `0.9 %`
  after. That is the mask waking up, not deck appearing. C1's `0.802` (204) clears it either way,
  which is why `C22`'s own note about the mask surviving dimming held there and only there.
- **C4 above the band — content preserved; M-a deletes nothing.** The two CAP-12 C4 tension poses
  (`t21.5` 1230 m, `t32.0` 1293 m): card `p90` **222.65 → 208.65** at both, near-field 221.95 →
  208.30 and 220.93 → 207.73, and the field is still *there* — this is the visible difference from
  `M-b`, which would have emptied both frames of cards. C4's deck is untouched by the un-dimming
  half by construction (its `WorldLight` clamps to 1.0), so these two frames isolate the card TUNE
  alone. A7's green-flattened gate shot at C4 1200 m still fills the frame (**921,600 px**, both
  builds); at C1 1192 m the same instrument reads 534,412 → 527,448 px of cloud (−1.3 %), which is
  the *threshold* moving with the cards' own 6.25 % dimming, not coverage lost — the sprite census
  is identical on both builds (22,201 = 9,025 + 13,176).

#### Probes worth human eyes (`.scratch/c23fork/`)

`AB-abovedeck-ma.png` (original | before | landed — the straight-edged wedges the floor cut into
the near sheet are gone and the sheet reads as one tone), `AB-1700-ma.png`, `AB-1160-ma.png`,
`AB-c1c-ma.png` (the three-tone C1C frame, before and after), plus `montage-*.png` from the fork
itself.

#### Files

`CSVM/src/Session/WeatherRig.cs` (`DeckRegime`'s third column; `SetDeckUndimmedMeshes`;
`SetDeckDimmed` + `CollectDeckTiles` + the private `DeckLighting` cache of one rig's tiles),
`CSVM/src/Mech3/WorldBuilder.cs` (`CloudDeckUndimmedMeshes`, `RecordDeckUndimmedMesh`),
`CSVM/src/Mech3/SceneBuilder.cs` (`SharedMesh` takes the two override flags),
`CSVM/src/Session/GameSession.cs` (hands the pairs to the weather rig beside `SetDeckCenter`),
`CSVM/src/Effects/FogVolumeClutter.cs` (`CardVertexColorTune`),
`CSVM.Tests/DeckRegimeTests.cs` (+1 case, and the existing four now assert the third column),
`docs/architecture.md` (all four module entries), `docs/formats/fogvol.md` (the card description's
TUNE note), `docs/PLAN-overcast-match.md` (this block + the checklist line).
`.scratch/c23fork/` — `verify.ps1` (the base/landed probe set), `regress.ps1`, `report.py`,
`ma.patch`, `base-*`/`ma-*` PNGs, `AB-*.png`, `runtests-output.txt`.
⚠ `.scratch/c22/regress-C*.png` were overwritten by this item's 8-chapter sweep (a mis-escaped
path in the copied script). Only the images; `C22`'s section quotes its censuses in full and none
of them changed. The frames now living there are the M-a ones, and copies are in `.scratch/c23fork/`.

**Not touched, per the fork's own scope:** the below-band gate, `K`
(`DeckCeilingHeight` 135), the whiteout band's altitudes/opacity/colour, the dome, `cloudparent`,
and every `docs/formats/` decode except `fogvol.md`'s one TUNE note. **`PROJECT_CONTEXT.md` and
`backlog.md` are untouched** — `BL-118` closes with `C24`.

### Original brief (kept for reference)

**Goal.** The CAP-12 tops caveat resolved (mesh and sprite tops measured separately), and the
from-above boundary between deck mesh and sprite field blending as the original does — no hard
colour cut.

**Evidence (confidence: direction sound).** PT-42(a): ours shows a hard cut; the original blends.
The 211-vs-214 tops match sampled an unknown mix of the two populations (BL-118's ⚠).
Re-confirmed post-Wave-A at the controls (user, 2026-08-08, C1C): "the color difference between
fogvol puffs and the cloud deck texture is jarring. many hard lines" — the scatter fixes did not
touch it, as expected; it is this item's target.
— *the C1C half is answered: that report is a pre-`C22` build, where C1C's deck was 197.4 against
its 174.6 field; `C22` narrowed it to 20 units. The "many hard lines" are the geometric cut (a)
above, and they are C1/C1C/C2B-only — C4's band centre equals its authored deck altitude, so its
floor never enters the card band.*

**Approach.** Per-population boxes via `--tex-override` isolation at the above-deck pose; whichever
population is off gets the correction (sprite vertex colours are authored 240/240/240 — data first,
per ground rules). If the cut survives matched brightnesses, the blend is geometric (sprite bases
vs sheet altitude — Wave A's anchor) — re-check A3's numbers before inventing a shader blend.
— *followed, and it needed one instrument the approach did not anticipate: `--tex-override`
isolation alone reads 0.0 % mesh at every above-band pose because the deck is behind alpha, so the
two-flat-colour bleed pair had to be built to get a per-population number at all. The blend IS
partly geometric and A3's numbers are what proves it (card bottoms 991–1028 m against a floor at
1047 m).*

**Model recommendation.** high — two interacting populations, easy to fix the wrong one.

**Verify.** Above-deck pose: per-population boxes within ±10 of the original's respective regions;
the boundary region shows no step in a horizontal luminance profile.
— *the horizontal profile was replaced by the vertical one, which is where the boundary actually
lies: the field's `far_fade` rim is a horizontal ring in world space, so it crosses ROWS. At
1700 m ours steps 181 → 216 between rows 450 and 480 where the original ramps 175 → 200 over 240
rows.*

**⚠ Traps.** A claim about one population is not evidence about the other — keep the vocabulary
(`BL-118`'s note) in every measurement label.
— *respected; and the trap bit once already, in `C21`/`C22`'s "0.0 % mesh" reading, which is
corrected above rather than repeated.*

## C24 ☑ Final match: both stills within the bar; mint PT + night-moonlit BL; close BL-118

**Landed.** (2026-08-09) Measurement and bookkeeping only — **no engine code, no shader**; every
`.cs` line in this commit is a comment, repointed off the deleted `BL-118` or off the plan's old
`docs/` path (`dotnet format --verify-no-changes` clean, all 13 goldens `ok`). Both reference views
were re-shot at the pinned poses on the close-out build (Waves A + B, `C21`–`C23` including the
`M-a` fork landing, `C25`, `C26`) and measured per decision 8: region luminance boxes, per region
and per population, HUD/plane-free on the original side. **Every comparable region is inside the
±10 bar.** Three rows are reported as ⚠ *not comparable* rather than as failures — the two frames
hold different objects there, with the sd/high-pass evidence for that in the tables — and **one
row fails with an attributing item**: the original's above-deck far field carries a dead-flat
`FOG_COLOR` band and ours carries none (`BL-327`). `.\RunTests.ps1` exits **0** with the six
standing goldens re-pinned.

### The instruments, and what each pose's populations are

`.scratch/c24/shoot.ps1` — nine shots, every one with its `weather` log line kept (`METHOD-15`).
Per pose: natural; `--tex-override=cloudlayer.tif=ff0000` (the deck-**mesh** mask — legitimate
where `SHOT-21` bans an override, because `cloudlayer.tif` skins the mesh alone);
`--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00` (**both** sprite populations at
once — `SHOT-21` again, so it is read only as "any cloud card here or not"); plus the above-deck
`--no-fog` black/white deck pair for `C23`'s bleed instrument. Measured by `.scratch/c21`'s
`capboxes.py`/`measure.py`/`population.py`, `.scratch/b15`'s `boxes.py`, `.scratch/c23fork`'s
`metrics.py`, `.scratch/c23`'s `bleed.py`, and four instruments this item adds (`elev.py`,
`popelev.py`, `sheet.py`, `fogrun.py`).

| pose | cloud cards in frame | what the boxes measure |
|---|---|---|
| **river** `-7323,192,-3829` | **0.0 %** | the `CloudDeck` **mesh** from below, `C26`'s annulus, the dome wall, bare terrain |
| **above-deck** `-7323,1192,-3829` | **29.4 %** of frame | `fvol` cards over the world-fixed deck floor, dome above |

The river zero is the population note `A3`/`B17` asserted, now measured on the final build, and the
above-deck 29.4 % is its able-to-fail control (`METHOD-9`) — the same override on the same build.

### Table 1 — river pose vs `OriginalScreenshots/C1 IA1 Fog river.png`

`-7323,192,-3829` / `-0.997,-0.1,0.070`. **(a) `B15`/`B17`'s own fractional boxes**, so the Wave-B
column is the same instrument, not a re-derivation:

| region | population | `B17` (Wave B) | **ours now** | original | Δ | verdict |
|---|---|---|---|---|---|---|
| sky (mid-sky gradient) | deck mesh | 180.6 | **172.9** | 169.4 | **+3.5** | ✓ (was +11.2) |
| near valley (river + grass) | terrain | 58.8 | **58.8** | 62.2 | **−3.3** | ✓ |
| mid terrain | ours terrain / orig fog wall | 71.5 | **71.5** | 175.0 | −103.4 | ⚠ not comparable — below |

**(b) The same frames read by ELEVATION above the true horizon** (`SHOT-23`(b): the original is
level at 1250×713, horizon row 356; ours pitches 5.71° down at 1280×720, horizon row 300 =
`360 − 599.1·tan 5.713°`, the same `f` the `C21`/`C25`/`C26` rim formula uses). Elevation is a
property of the world and the camera *position*, so it survives the pitch difference that a
fractional box does not. Columns are `B15`'s two HUD-free bands:

| band | population | ours | original | Δ | verdict |
|---|---|---|---|---|---|
| **+300…+200 px** | deck mesh 100 % | 175.85 | 169.31 | **+6.5** | ✓ |
| **+200…+100 px** | deck mesh 100 % | 175.10 | 169.52 | **+5.6** | ✓ |
| **+100…+40 px** | deck mesh 87 % + annulus | 169.68 | 169.22 | **+0.5** | ✓ |
| +30…−15 px | ours terrain (sd 28.80) / orig fog wall (sd 1.44) | 160.63 | 174.55 | −13.9 | ⚠ not comparable |
| −40…−100 px | ours terrain (sd 19.14) / orig fog+terrain edge (sd 51.75) | 72.12 | 118.68 | −46.6 | ⚠ not comparable |
| **−100…−250 px** | terrain both | 69.13 | 72.19 | **−3.1** | ✓ |

**(c) The fog/`K` reading — this is the row `C21`/`C25` were built for:**

| reading | before Wave C | **ours now** | original | verdict |
|---|---|---|---|---|
| elevation at which the ceiling goes dead flat at `FOG_COLOR` | 64 px (`B17`, `K` = 400) | **+20 px** (176.00, sd 0.06, hp 0.062) | **+16 px** (175.00, sd 0.00, hp 0.000) | ✓ 4 px |
| depth of that flat run (`fogrun.py`, ±4 with per-row sd ≤ 3) | — | **41 rows** (5.7 % of frame, rows 245–295) | **85 rows** (11.9 %, rows 293–398) | the deficit is entirely at the BOTTOM — see the framing note |
| deck rim visible? | 39 px step (`C21`) | **no** — `C26` puts it at ≈3.95 px | none | ✓ |

`C21` predicted "at `K` = 135 ours becomes onset 80, completion **20 px**" against the original's
17.5. Measured: **20 against 16.** The whole `K` chain — `C21`'s fog-ramp fit, `C25`'s 400 → 135,
`C26`'s annulus — lands on the original's own saturation elevation to within 4 px.

**⚠ The framing residual, measured and NOT minted.** The three ⚠ rows above are one fact:
**the original's terrain silhouette tops out 38 px BELOW the true horizon; ours reaches +12 px** —
a 50 px (4.8°) gap, so a band that holds the original's fog wall holds our hillside. It is not a
luminance miss and it is not new: `B15` recorded it as "the original … with its terrain 41 px
*below* the true horizon, our matched render … with terrain rising 17 px *above* it", which is
`SHOT-23`(b)'s own measured case, and the pre-plan CSVM twin
(`Z:\CSVM\Screenshots\C1 IA1 Fog river.png`, 2026-08-08) frames the terrain exactly as we do today
— so nothing in this plan moved it. A spot check at the original's own **ALT-gauge** altitude
(230 m, against the twin overlay's 192 — the discrepancy `A7` recorded) moves the onset +12 → ≈+5
and lengthens the flat run 41 → 51 rows: **~7 px of the 50 is altitude, the rest is which terrain
the derived yaw puts in frame.** Recorded rather than minted, per decision 10 and this item's own
trap: it is a pose question, not one of this plan's three mechanisms, and chasing it would reopen
`A4` inside the close-out commit.

**⚠ Instrument note (`DIAG-15`), for anyone re-reading the mesh percentages.** The flat-red mask
(`r > 200`) goes blind in fog: at elevation +60…+40 the override's own red reads **197–199** in
this frame (the fog mix pulling flat red toward 176), so the mask reports 0 % mesh below ≈55 px
where the deck is plainly still there. Same threshold trap as `C23`'s C1C/C2B note, from the other
direction. The `+100…+40` row's "87 %" is a floor, not a coverage.

### Table 2 — above-deck pose vs `OriginalScreenshots/C1 IA1 Fog above clouddeck.png`

`-7323,1192,-3829` / `0,0,-1`. Per population throughout (decision 8):

| region | population | before `M-a` | **ours now** | original | Δ | verdict |
|---|---|---|---|---|---|---|
| zenith (`B15` box) | dome | 72.8 | **72.8** | 69.4 | **+3.4** | ✓ |
| apex, rows 0–150 full width (`B12`'s box) | dome | 75.5 | **75.47** | 70.53 | **+4.9** | ✓ |
| dome band x 0.20–0.80, y 0.15–0.30 (HUD-clean) | dome | — | **77.05** | 71.61 | **+5.4** | ✓ |
| `tops-M` (near sheet, bottom centre) | 91 % card / 9 % deck | 215.45 | **208.23** | 207.75 | **+0.5** | ✓ |
| near sheet, y 0.90–0.975, clean columns | card + floor | — | **207.50** | 210.93 | **−3.4** | ✓ |
| card plateau `p90`, lower 45 % | `fvol` cards | 222.65 | **209.24** | **209** (208.88 `t124` / 209.16 `t59`) | **+0.2** | ✓ |
| near-field patch mean / sd | `fvol` cards | 216.15 / 1.68 | **208.24 / 0.02** | 208.88 / 0.54 (`t124`) | **−0.6** | ✓ |
| deck floor (flat-red mask) ↔ cards | mesh vs cards | **+49.03** | **−4.11** | one flat tone, no step | — | ✓ |
| sub-`FOG_COLOR` tail in the sheet (rows 396–720) | mesh + cards | outboard `p1` 178.00 | **min 206.05, 0.000 % below 176** | never below `FOG_COLOR` 175 | — | ✓ |
| **far field: dead-flat `FOG_COLOR` rows between dome and near sheet** | `fvol` cards | 0 | **0 (0.0 % of frame)** | **74 rows (10.4 %, rows 517–590)** | — | ✗ → **`BL-327`** |
| `deck tops` (`B15` box, y 0.62–0.90) | mixed | 221.2 | 208.7 | 157.2 (sd 42.03) | +51.5 | ⚠ not comparable |
| `tops-L` / `tops-R` | mixed | 210.29 / 207.60 | 209.79 / 207.91 | 169.00 / 169.42 | — | ⚠ not comparable |

**The two ⚠ rows are `C23`'s own `SHOT-23` finding, unchanged:** the original still's pitch puts
its cloud-top line at y 0.70–0.80 where ours sits at y ≈0.50, so those boxes sample
cloud-tops-against-dark-sky on the original side and solid sheet on ours (original sd 22.6–42.0
against our 1.0–1.7). `tops-M` and the y ≥ 0.90 rows are the ones that sample the same object on
both sides, and they are the rows above.

**The one real miss, and its item.** `fogrun.py` asks a question that needs no pose match at all —
*does this frame contain a dead-flat run at `FOG_COLOR` between the dome and the near sheet?* The
original's does: **74 rows, mean 175.0, per-row sd ≤ 3**, rows 517–590. Ours contains **none**: our
sheet steps from the dome straight to the card plateau 208.65 and never fogs, because `fvol` cards
author `fog: false` (`C23` candidate 3, refuted as a thing to change — it is deliberate authored
data) and our `far_fade` rim ends the field rather than fading it. This is residual 1 of the three
`M-a` handed forward — the 1700 m far field (`tops-L`/`tops-R` +14.9/+29.0 over `t97`) — seen at
the reference pose, and it is **`BL-327`'s** far-field half. It is a distance/fade question about
the card population, not a brightness one: every brightness row above passes.

**Per-population weight (`C23`'s bleed instrument, re-measured on this build).**
`bleed.py`'s `WORLD_LIGHT = 0.802` divisor is now wrong above the band — `M-a` un-dims the floor
there — so its raw 21.0 / 22.9 / 10.7 / 11.7 % correct to **16.8 / 18.4 / 8.6 / 9.4 %** for
`tops-L` / `tops-R` / `tops-M` / whole lower 45 % (`C23` measured 13.6 / 14.4 / 6.2 / 7.3 with a
dimmed floor). `BL-118`'s tops caveat therefore closes as **"81–91 % sprite, 9–19 % deck"**, the
same shape `C23` found and not "100 % sprite".

### `C26` at this pose — 83 pixels, and they are the right 83

The above-deck frame is **not** bit-identical to `C23`'s `M-a` landing, and the difference is
exactly what `C26` should be: `83 px (0.01 %), mean|d| 0.0000, max 1, rows 364–370` — a seven-row
band at the horizon (row 360), the annulus seen edge-on from above the band. Every statistic in
Table 2 that `C23fork` also measured reproduces its landed value **to the decimal** (card `p90`
209.24, floor 213.35, gap −4.11, near-field 208.24 / 0.02, outboard `p1` 207.41), which is the
`METHOD-3` check that this item measured the build `C26` left behind.

### Goldens — the standing six, re-pinned, and nothing else moved

`GOLD-8` first: the run before re-pinning reported **6 moved, 0 broken of 13**, and the six are the
standing un-repinned set exactly — `C22`'s five (`c1-waterfall`, `c1-flight`, `c1-destroy-effects`,
`c1c-rain`, `c2b-rain`) plus `C25`'s `c4-snow`. No unexpected mover; `viewer-bhawk` reported `ok`
without hanging (`BL-320` did not bite). The `actual` hashes also confirm `C26`'s own claim: only
`c1-waterfall` still carries `C23fork`'s recorded hash (`c81a000a…`), and the other five moved
past it — which is `C26`'s "five of the six move further, `c1-waterfall`'s pose misses the strip",
verified here rather than assumed.

Re-pinned with `-RegenGoldens`; the diff is **six `hash` fields and nothing else** (`GOLD-1`: no
re-pin prose in `exercises`). A clean full run then exits **0** — build PASS 0 warnings, units
**683/683**, engine **26/26 errors clean**, goldens **13 hash-identical**.

### `BL-118` closed — deleted from `backlog.md`

Every half of it is landed or has a named successor:

| half | outcome |
|---|---|
| the `CloudDeck` **mesh** underside, +54 too bright | **landed `C22`** — the deck was the one world surface the original dims by the mission SUNLIGHT and we did not (`lighting: false` gated it off); `170.6` against the original's `168.1`, and `C21`'s free-parameter fog fit recovers the same number from the original still's own fog ramp |
| the mesh↔sprite **cut** from above | **landed via the `C23` fork's `M-a`** — regime-conditional dimming + the card `225/240` TUNE: card `p90` 222.7 → **209.24** against the original's 209, floor↔card gap **+49 → −4.11** |
| the tops-from-above **caveat** ("re-measure per population") | **answered** — the boxes are 81–91 % sprite, 9–19 % deck (`C23`'s bleed instrument, re-measured above); they were never the 100 % sprite `C21`/`C22`'s flat mask read |
| the whiteout's hardcoded white | **landed pre-plan**, commit `9b69568` — the authored `CLOUD_COVER` colour |
| deck placement / the band / the regime | **landed** `A6`/`A7`/`C25`/`C26` |
| **night-moonlit** puffs (`CAP-11` C1B) | split to **`BL-325`** per decision 2, with the evidence pointers and the three-population vocabulary carried over |
| the `225/240` card plateau's undecoded mechanism | split to **`BL-327`**, together with the far-field residual above and the `lighting: true` gate question |
| `PT-42` | already retired 2026-08-07; its successor at the controls is **`PT-47`** |

References repointed rather than left dangling: `docs/architecture.md` (the stale "belongs to Wave
C" ⚠ deleted — it is landed), `docs/formats/weather.md` (→ `BL-325`; the C4 veil datum → `CAP-12`),
`docs/formats/fogvol.md` (→ `CAP-12` / `BL-325`), `backlog.md`'s `BL-317` vocabulary pointer
(→ `BL-325`), plus three comments in `WeatherRig.cs`, two in `Weather.cs` and one in
`analysis/video-flight-calibration/extract.py`. `docs/verification.md` never cited it.
`docs/HISTORY.md` and the archived plans keep their references — both are frozen history.

### Files

`.scratch/c24/` — `shoot.ps1` (nine shots + weather logs), `elev.py` (elevation-referenced bands
and profiles), `popelev.py` (population by elevation band), `sheet.py` (the above-deck sheet by
frame depth, through columns clean on BOTH sides), `fogrun.py` (the pose-independent
fog-plateau test), `grid.py` (used to LOCATE the original's furniture before trusting a box — it
found a dark instrument bar across the bottom ~2 % of the above-deck still that was not on
record), `diff.py`, `sxs.py`, `delete_bl118.py`, `repoint_plan.py`, every PNG the tables cite, the
two side-by-sides, and four `runtests-*.txt`. `analysis/goldens/manifest.json` (six hashes),
`backlog.md` (`BL-118` deleted; `BL-325` + `BL-327` minted; `BL-317` repointed), `playtest.md`
(`PT-47` + its new C1 flight-profile section), `PROJECT_CONTEXT.md` ("Current status"),
`docs/architecture.md`, `docs/formats/weather.md`, `docs/formats/fogvol.md`,
`docs/plans/plans.md` (this plan's row), and this file — `git mv`'d into `docs/plans/`, with its
two `docs/formats/` links re-relativised. Comment-only, no behaviour: `CSVM/src/Session/WeatherRig.cs`,
`CSVM/src/Flight/Weather.cs`, `CSVM/src/Effects/FogVolumeClutter.cs`, `CSVM/src/Mech3/FogVolumes.cs`,
`CSVM/src/Mech3/WorldBuilder.cs`, `CSVM/src/Utils/Rng.cs`, `CSVM.Tests/RngTests.cs`,
`analysis/video-flight-calibration/extract.py`, `analysis/fog-zone-survey/FINDINGS.md` — the last
seven only because the plan's path changed under them.

### The two side-by-sides, for the user

- `.scratch/c24/final-river-sxs.png` — original | ours, labelled, both scaled to a common height,
  nothing cropped.
- `.scratch/c24/final-abovedeck-sxs.png` — the same for the above-deck pose.

⚠ They are for the eye, not for a verdict (`SHOT-5`): every number above comes from the decoded
pixels, never from the composite.

### Original brief (kept for reference)

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
— *it bit once, at the river pose's terrain silhouette: closing that band needs the derived view
direction moved, i.e. `A4` reopened. Recorded above with its numbers, not chased.*

⚠ **`C23`'s fork is TAKEN and LANDED (`M-a`, user 2026-08-09)** — see that section's
`Fork resolved` block. `PT-42`(a) is therefore judgeable at the controls again rather than blocked:
the above-band deck floor is undimmed per regime and the card plateau is 209, floor↔card gap −4.
What this item inherits instead is three named residuals, each with its numbers already measured:

1. **The 1700 m far field.** `tops-L`/`tops-R` sit **+14.9 / +29.0** over `t97` (were +20.1/+30.4).
   Our `far_fade` rim steps where the original ramps over ~240 rows — a distance/fade question, not
   a brightness one, and M-a neither fixed nor worsened it.
   — *measured again at the pinned above-deck pose, framing-independently: the original carries 74
   dead-flat `FOG_COLOR` rows there and ours carries 0. `BL-327`.*
2. **C1C/C2B's three-tone split, which M-a WIDENS** (cloud-population spread 60.7 → 71.6; the deck
   floor goes from 19.8 *under* their cards to 32.1 *over* them). The candidate is the fifth one
   `C23` minted — whether `lighting: true` on a `Facade` cloud card is a `WorldLight` gate at all.
   It moves C1C/C2B/C5 and nothing about C1's two reference stills. **Mint it; do not guess it.**
   — *minted as `BL-327`, with `PT-47`(d) as its at-the-controls half.*
3. **The `225/240` card TUNE has no decoded mechanism** (`C23` refuted four candidates). It is a
   calibrated match to two measured original frames and is marked TUNE in the constant's own
   comment; anything that decodes the real mechanism REPLACES it.
   — *carried into `BL-327` as the third question, with all four refutations as its traps.*

Also inherited from `C23`: the goldens list to re-pin is **six**, not five — `C22`'s five plus
`C25`'s `c4-snow`. M-a itself moved **none** of the thirteen (verified against a reverted-build
golden run), so that list is unchanged by the landing.
— *confirmed: six moved, 0 broken of 13, no unexpected mover.*
