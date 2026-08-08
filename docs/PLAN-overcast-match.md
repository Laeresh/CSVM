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
  `x -7323 y 1192 z -3829`; river `x ≈ -7325 y 934 z -3829`. View direction is NOT in the overlay —
  re-derive it by matching terrain features before the first A/B and record it here.
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

1. ☐ A1 — Decide the scatter mechanism from the evidence (research)
2. ☐ A2 — Kill the lattice and the field edge (horizontal mechanism)
3. ☐ A3 — Top-anchor the vertical placement
4. ☐ A4 — Scatter A/B vs CAP-12 + the river twin; close `BL-312`

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
`FogVolumeClutter.cs` — sequence them, never parallel worktrees. Inside Wave B: B11, B12, B13 are
independent research and may interleave (B13 may park on a [USER] capture — proceed with the
others); B14 needs all three; B15 and B16 follow B14 and both touch the weather/fog path
(`Weather.cs`/`WeatherRig.cs`/`SceneBuilder.cs`) — sequence them; B17 last. Inside Wave C: C21
blocks C22; C22 before C23 (the cut can only be judged with the underside fixed); C24 last.

---

# Wave A — the scatter

## A1 ☐ Decide the scatter mechanism from the evidence

**Goal.** A written verdict (in this section + `fogvol.md`) on how the original places the
`cloudsprite` field: randomised at the authored mean spacing inside the `fvol` boxes, tiled
unboundedly around the camera on the `distance` period, or a hybrid per volume shape — plus how the
vertical placement anchors. No code.

**Evidence (confidence: traced for the constraints, lead-only for the mechanism).** BL-312's two
candidates and the argument that the density corroboration only ever supported the spacing;
PT-42(b): world-locked, tiled, no volume edge anywhere over the base map; CAP-12 grazing stills
(t=44/59/97/124, t=29.2): soft continuous structure, no lattice; `fogvol.md`'s census: C1/C1C/C2B/C4
map-spanning slabs, C1C's twelve stacked build-ups, C5's seventeen street-level strips. The
`templates.zrd` shared grammar tiles ground clutter unboundedly.

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

## A2 ☐ Kill the lattice and the field edge

**Goal.** At any grazing angle the sheet shows soft continuous structure — no 130 m comb — and
flying over the base map shows no field edge, per A1's mechanism.

**Evidence (confidence: traced).** `FogVolumeClutter.cs` scatters on a world-origin X/Z grid of the
`distance` period with `perturb_dist_range` 10–20 m jitter — ±15 % on 130 m, which is the comb.

**Approach.** Implement A1's horizontal mechanism in `FogVolumeClutter.cs`. Keep the authored mean
spacing (density is the one thing the old reading got right), the per-kind weights,
`far_fade_range` consumption, and the built-once/world-anchored/splitscreen-shared structure
(architecture entry). Update `fogvol.md`'s "What is decoded and what is inferred" in the same turn.

**Model recommendation.** high — placement math with a determinism constraint.

**Verify.** `--screenshot` grazing passes along tops and base at CAP-12's angles: no periodic
structure (eyeball + a column-autocorrelation check on the sheet region); sprite count logged per
chapter stays within ~5 % of the census (9,025 C1) unless A1 decided tiling — then state the new
number and why. `.\RunTests.ps1` green; goldens re-pinned only at A4.

**⚠ Traps.** `--det` must keep the field stable frame-to-frame and run-to-run — a camera-tiled
field re-seeded per cell must hash cell coordinates, not accumulate RNG state, or determinism and
world-lock both break. Do not touch `cloudparent`.

## A3 ☐ Top-anchor the vertical placement

**Goal.** Card bottoms sit at/above the deck sheet: C1's underside view shows the flat deck mesh
(no cauliflower bottoms — the river twin's top edge today), C4 reproduces the measured 30–75 m
clear band at 1135 m.

**Evidence (confidence: traced).** The disproof of uniform fill (wrong-claim #1). Top-anchoring at
C1's 1090 predicts bottoms ~991–1027 vs measured whiteout onset 1003 / wisps 982; C4 predicts the
clear band the clip shows.

**Approach.** Replace the uniform-Y placement in `FogVolumeClutter.cs` with centres near the volume
top plus `perp_dist_range`; correct `fogvol.md`'s inference note (it already carries the
under-challenge banner). Keep C5 in mind: its strips are −463…183 m ground haze — verify the
anchor rule doesn't hollow them out (if it does, the anchor is per-volume-shape; say so in the doc).

**Model recommendation.** medium — a contained placement change with a written prediction to hit.

**Verify.** C1: `--pos` shots at 934 m (river pose) — no sprite bottoms below the deck sheet; C4:
`--pos` at 1135 m in clear air (CAP-12's `csvm-c4-1135m.png` pose) — sky gap present. C5 freecam
street pass unchanged in character.

**⚠ Traps.** The whiteout band is a separate system (`WeatherState.WhiteoutAmount`) — climbing
through 970–1124 must still white out on schedule; don't "fix" a whiteout symptom with sprite
placement or vice versa.

## A4 ☐ Scatter A/B vs CAP-12 + the river twin; close BL-312

**Goal.** The wave's acceptance criteria measured and recorded; `BL-312` deleted from the backlog.

**Evidence (confidence: n/a — this is the instrument).** CAP-12 stills + the pinned river pose;
PT-42(c)'s "a lot denser" as the density reference.

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

# Wave B — fog semantics and zones

## B11 ☐ All-chapter zone-table survey; test H1/H2/H3 on paper

**Goal.** One table: every chapter × zone's `FOG_RANGES`/`FOG_ALTITUDE`/`FOG_COLOR`/`CLIP_RANGES` +
sunlight, and against it each hypothesis' predictions for (a) the settled zone verdicts
(C1B/C2/C3/C5 = zone1), (b) BL-303's three broken scenes, (c) the CAP-11-confirmed healthy scenes,
(d) the two C1 stills. Hypotheses: **H1** `FOG_ALTITUDE` fades the whole fog effect by *camera*
altitude; **H2** per-fragment fade (current shader); **H3** altitude-triggered zone *switching*
(user hypothesis, decision 5; candidate switch point = zone1's band top, 1047 in C1 — note C5's
identical bands make a switch unreachable there, which is consistent with its zone1 verdict).

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
