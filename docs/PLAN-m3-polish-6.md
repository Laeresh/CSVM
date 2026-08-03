# M3 polish 6 — open bugs: surfaces, the animation runtime, and the instruments

**ACTIVE PLAN** (written 2026-08-03). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

Ten items selected from `backlog.md` under the user's criteria (2026-08-03): draw from the **"Open
bugs" section and its neighbours** — test infrastructure, surfaces/colliders, HUD & audio — and
**exclude everything blocked on an owed capture of the original** (`CAP-nn`), because the user is
working the capture-blocked items themselves. Also excluded by the user: the newly-unblocked
flight-model pair `BL-092`/`BL-247` (deserves its own focused plan — both touch the guarded
`flight-envelope` suite). Excluded during selection: `BL-047` (ruled capture-blocked 2026-07-30,
`docs/HISTORY.md`), `BL-160` (blocked on `CAP-09`), `BL-161` (out of scope until a cockpit-audio
feature), `BL-048` (a future Hi-Def-mode invention, not a bug fix).

**Each item was re-verified still-open against both `docs/HISTORY.md` and the code on 2026-08-03**:
none of the ten IDs has a landed HISTORY entry; `zone_set` has zero hits in `CSVM/src`;
`FromToMotion.cs:109-111` still reads the delta channels through `Channel()` (which returns
`(null, null)` on the bare-vector shape they ship in); `TextureArchive.KnownAbsentFromGameData`
does not contain `cloud1`/`cloud2`; `GameZ.cs` parses no node-level `active` flag. Two blockers
have cleared since the items were filed: `BL-051` was excluded from polish run 4 (2026-08-01) only
because `BL-099` was open — `BL-099` was **answered 2026-08-02** (the persist-log decode); and
`BL-058`'s precondition, the subface fix, **landed** (user-confirmed at the controls,
`docs/HISTORY.md` 2026-08-01 "M3 polish-5 A2" cites it as landed).

## Milestone goal

- The world builders stop silently dropping shipped gamez data: the node `active` flag is honoured,
  the per-polygon second material pass renders, and `zone_set` is at least parsed and documented.
- The two known-wrong-order rendering defects shrink: cross-node depth-bias conflicts resolve by a
  dense conflict rank, and the C5 doubled-buildings question left behind by the subface fix is
  answered.
- The animation runtime's two known timing/data drops are settled: the 26 dead `FROM_TO` delta
  channels get real semantics (or a documented disproof), and the one-frame `CallSequence` dispatch
  lag is either fixed with a bounded drain or explicitly re-deferred with a measured reason.
- The instruments improve: the silent golden-run exit-1 becomes capturable if it recurs, and the
  collider probe exists again and explains its off-by-6/11.
- The C3 `cloud1`/`cloud2` magenta question gets its user decision, whichever way it goes.

**No capture-blocked item enters this plan, and no TUNE is judged inside it.** Anything that turns
out to need the original at the controls gets written back to `backlog.md`/`playtest.md` with its
`CAP-nn`, not absorbed here.

## Decisions (2026-08-03)

| # | Question | Decision |
|---|---|---|
| 1 | Selection criteria for the ten items | **Open bugs / test infra / HUD & audio / surfaces** — the user's call, replacing the proposed default (M3-scope + code-verifiable) |
| 2 | Include the CAP-unblocked flight-model items (`BL-092`/`BL-247`)? | **No** — the user is working the capture side themselves; the drag+lift pair gets its own plan |
| 3 | `BL-047` (crash damage display) | **Stays out** — its fix was ruled to wait on an original-game crash capture (2026-07-30); effectively capture-blocked |
| 4 | `BL-133` (C3 missing textures) | **In, as a decision item** — the backlog reserves the magenta-vs-gray call for the user; the plan's job is to put the decision in front of them and land whichever they pick |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
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

### Wave A — surfaces: what the builders drop

1. ☑ A1 `BL-051` — honour the gamez node `active` flag
2. ☐ A2 `BL-056` — render the per-polygon second material pass
3. ☐ A3 `BL-058` — answer the C5 doubled-buildings question (post-subface check)
4. ☐ A4 `BL-053` — dense cross-node conflict rank for the depth bias
5. ☐ A5 `BL-057` — parse + census `zone_set`, document it

### Wave B — animation runtime: dropped data and late dispatch

11. ☐ B11 `BL-050` — the 26 dead `FROM_TO` `*_delta` channels: census, decide, implement
12. ☐ B12 `BL-135` — the one-frame `CallSequence` dispatch lag: bounded same-pass drain or measured re-deferral

### Wave C — instruments

21. ☐ C21 `BL-039` — make the silent golden-run exit-1 capturable
22. ☐ C22 `BL-070` — rewrite the static collider probe; explain the off-by-6/11

### Wave D — decisions

31. ☐ D31 `BL-133` — the C3 `cloud1`/`cloud2` magenta call (user decision + one-liner)

## Dependency and parallelism notes

A3's verdict must be taken **after A1 and A2 land** — both change what C5 draws, and a
doubled-buildings check taken before them would have to be re-taken. A1, A2 and A4 all touch the
`GameZ`/`SceneBuilder`/`WorldBuilder` build path — run them serially, never in parallel worktrees.
A4 and A1 both move golden hashes (expect rebaselines); land and rebaseline one before starting the
other so a moved hash has exactly one owner. B11 and B12 both live in the anim runtime
(`FromToMotion.cs` / `AnimRuntime.cs`) — serial. Wave C and D31 are independent of everything and
can interleave anywhere. Within a wave, listed order is the intended order.

---

# Wave A — surfaces: what the builders drop

## A1 ☐ `BL-051` — honour the gamez node `active` flag

**Goal.** A node the gamez ships `active: false` is built hidden (or not built), matching the
original's own record of the build script's `NodeSetActive off` — instead of today's
everything-visible.

**Evidence (confidence: traced).** `GameZ` parses no node flags at all (`flags` touched only for
*polygon* flags, `GameZ.cs:278`); measured population is small — C1 13, C1B 2, C1C 2, C2 3, C2B 2,
C3 6, C4 6, C5 2 inactive nodes, of which only **four are world-build roots**: C1
`fuel_truck01`/`fuel_truck02`, C2 `piratezep`, C3 `barracuda`. All four are currently masked by
something else, coincidentally. **The blocker is gone:** the backlog trap said "do not fix while
the C1 IA1 oil-tank fidelity question is open" — that question was `BL-099`, answered 2026-08-02
(the persist-log decode; CSVM's clean IA1 is correct, and `ia1.gw` deactivates the fuel trucks
anyway). Full evidence: `backlog.md` `BL-051`.

**Approach.** Parse the node-level `active` flag in `GameZ`, honour it where world-build roots are
selected/built (`WorldBuilder`), matching the semantics of the build scripts' `NodeSetActive`.
Survey first: confirm the per-chapter counts above against `nodes.json` before wiring, and record
where the flag is applied (build-time skip vs built-hidden — prefer built-hidden if anything can
later activate the node; C5's `piratezep` ships `active: true` and must be unaffected).

**Model recommendation.** medium — mechanical parse + a scoped visibility change, but with a
regression surface that needs care.

**Verify.** 8-chapter `--freecam` regression: node/mesh counts change by exactly the measured
inactive-root population per chapter and nothing else errors. Targeted: C2 `--freecam --node=`
checks that `piratezep` (shipped inactive in C2) no longer builds visible, while C5's does.
Goldens: expect movement only in chapters whose build roots change — take a baseline first.

**⚠ Traps.** (a) Do **not** extend this to other node flags without a survey — `terrain` is not a
visibility flag (it marks world-space map geometry; C3 `zepbridge1/2`, C4/C5 `zepdock`). (b) The
flag is **not** a fix for the polish-4 unplaced-sweep symptom — C5's `piratezep` ships
`active: true`. (c) C2/M01–M03 do not name `piratezep` in their setup scripts, so C2 is where a
behaviour difference would first be visible — look there, not only at C1.

**Landed 2026-08-04.** `GameZ.Active` + `WorldBuilder.Add`'s build-time skip, exactly as scoped.
`RunTests.ps1` clean (13/13 goldens hash-identical); `destructible-census` moved exactly as
predicted and was rebaselined (C1 214→210/145→143, C3 228→221/151→147). A git-stash A/B on C2
isolated the whole effect to `piratezep` (383 mesh instances, matching its own subtree size);
C5's `piratezep` (`active: true`) confirmed unaffected. Full record: `docs/HISTORY.md` 2026-08-04.

## A2 ☐ `BL-056` — render the per-polygon second material pass

**Goal.** The 352 C5 polygons that ship a second textured material with its own UVs — fog
gradients (`z3_foggrad`/`foggrad8x64`, 142), `buildingspotlighted` (34), `fadedsign01-03`, `nypd`,
`clock`, plane logos — render both passes instead of pass 0 only.

**Evidence (confidence: traced, measured 2026-07-23).** `materials` is a per-polygon LIST and
`SceneBuilder` reads only element 0; the census and texture names are in `backlog.md` `BL-056`
(same report as the depth-bias analysis, §3). Re-locate the exact read in `SceneBuilder` before
changing it (the literal `materials[0]` spelling was not found by grep on 2026-08-03 — find the
actual access pattern first).

**Approach.** Survey the payload first: how many entries beyond `[1]` exist (any polygon with 3+
materials?), what blend the second pass implies, and whether the second pass's UVs ship alongside.
Then build the second pass as a second surface/material on the same mesh — reusing the existing
shader includes — with the draw-order machinery aware of it (a second pass on the same polygon
must not z-fight its own base; the depth-bias scheme in A4's territory is adjacent, which is why A2
lands before A4's rebaseline or after, never interleaved).

**Model recommendation.** high — a real rendering feature with blend/order semantics to get right,
visible across C5.

**Verify.** Targeted C5 captures at a fog-gradient building and a `nypd`/`clock` sign
(`--freecam --chapter=C5`, `--tex-override=z3_foggrad` to prove the pass is drawing at all —
`docs/cli.md`). 8-chapter regression for errors/counts; goldens will move in C5 — baseline first.

**⚠ Traps.** (a) `--tex-census` counts are lower bounds and need `--no-fog` (`docs/cli.md`). (b) Do
not conflate this with A4's bias ordering — a second pass that only looks right because a bias
happens to order it is the masked-effect failure `docs/verification.md` warns about; verify the
pass renders with A4 unlanded or explicitly note the interaction.

## A3 ☐ `BL-058` — answer the C5 doubled-buildings question

**Goal.** A measured yes/no: with the subface fix landed (base ground layer hidden), does C5 draw
doubled clutter buildings — the base layer's own disjoint clutter templates (`cb12a`–`cb24a` on
`cblock4/5/6`) spawning on top of `cblock1/2/3`'s (`cb00a`–`cb11a`)?

**Evidence (confidence: lead-only — the source report flags it as an open question, not a
finding).** `backlog.md` `BL-058`. The precondition is met: the subface fix landed and the user
confirmed the C5 z-fight gone at the controls (`docs/HISTORY.md`, cited by the 2026-08-01
"polish-5 A2" entry).

**Approach.** Run **after A1 and A2**. Census first, capture second: from `nodes.json`/the clutter
build path, count what clutter each of `cblock1..6` actually spawns today; if both template sets
spawn at overlapping sites, frame one such site in `--freecam --chapter=C5` and look. The outcome
is a verdict written to `backlog.md`/`docs/`: either "no doubling, question closed" or a new
backlog item with the mechanism.

**Model recommendation.** medium, low effort — a bounded measurement with a written verdict; no
code lands from this item by design.

**Verify.** The verdict itself, with the census numbers and the capture path recorded. A disproof
closes `BL-058` per the ground rules (delete from backlog, HISTORY entry).

**⚠ Traps.** Treat it as a question, not a finding — do not pre-emptively "fix" clutter spawning.
If doubling exists, the fix is its own future item with its own regression, not a rider here.

## A4 ☐ `BL-053` — dense cross-node conflict rank for the depth bias

**Goal.** Cross-node conflicting surface pairs stop resolving the wrong way round: replace the
sparse `node_bias` term (which already spans 1.22–2.86 priority levels per chapter, letting
within-mesh surface rank out-bid the cross-node term on 2–16% of conflicting pairs) with a dense
conflict rank.

**Evidence (confidence: traced + measured, 2026-07-22).** `analysis/item9-depth-bias/` holds the
measurement; per-chapter spans C1 1.77, C1B 1.40, C1C 1.41, C2 1.24, C2B 1.22, C3 1.35, C4 2.07,
C5 2.86. The proposed magnitude is in the backlog entry: a dense rank at 28 × 5e-6 = 1.4e-4 = 0.7
levels fixes the inversion as a side effect. `docs/architecture.md`'s "accepted corner case" note
is what this item retires.

**Evidence caveat:** the 2026-08-01 "polish-5 A2" HISTORY entry re-measured `SurfaceRankCap` and
re-scoped `BL-052` after the subface fix — re-run the `analysis/item9-depth-bias/` instrument
against today's grouping before implementing, so the 2–16% figure is current, not inherited.

**Approach.** Re-run the instrument; then implement the dense conflict rank in the depth-bias
computation (`SceneBuilder` region that owns `node_bias`/surface rank — read its
`docs/architecture.md` entry first). Keep the instrument's before/after diff as the acceptance
number.

**Model recommendation.** high — global blast radius: every chapter's draw order moves; the
failure mode (a new z-fight somewhere unexamined) is exactly what the goldens exist for.

**Verify.** The instrument's wrong-way-pair count drops to ~0. Full `RunTests.ps1` including
goldens — **expect hash movement in every chapter; baseline first and rebaseline deliberately**
("an unchanged number is not evidence unless you've seen it able to fail"). Targeted captures at
the known conflict sites the analysis names.

**⚠ Traps.** (a) `BL-052`'s 36 genuine cap-collapsed pairs are a *different* defect (rank cap, not
cross-node span) — do not fold it in. (b) The C5 ground z-fight is already fixed by the subface
flag — do not re-attribute it here (`BL-037`'s correction). (c) Golden movement makes every other
in-flight change ambiguous — land this alone, nothing else in the same commit window.

## A5 ☐ `BL-057` — parse + census `zone_set`, document it

**Goal.** `zone_set` — the per-polygon weather-zone membership list nothing parses (zero hits in
`CSVM/src`, re-verified 2026-08-03) — is parsed into the reader model, censused across chapters,
and documented in `docs/formats/`; rendering consequences (if any) become their own backlog item.

**Evidence (confidence: traced for the gap; lead-only for meaning).** `backlog.md` `BL-057`: C5
uses values 1 and 3; it is not a ground selector (checked and ruled out); per-polygon granularity
is notable because weather zones are otherwise per-chapter. The zone-activation unknown (`BL-036`,
`BL-100`) is **not** in scope — that is the guess-what-to-hide trap.

**Approach.** Docs-first: parse the field (`GameZ`), census values per chapter into the formats
page (`docs/formats/world-structure.md` or `weather.md`, wherever the sibling fields live), and
stop. No rendering change lands from this item.

**Model recommendation.** medium, low effort — mechanical parse + census + docs page.

**Verify.** Round-trip safety: parsing an extra field must not change any built world — 8-chapter
regression with identical counts and unchanged goldens. The census table in the docs page is the
deliverable.

**⚠ Traps.** Do not act on the parsed values — `BL-036`'s rule stands: which zone a mission
activates is in no file in the install, and a wrong guess deletes visible content.

# Wave B — animation runtime: dropped data and late dispatch

## B11 ☐ `BL-050` — the 26 dead `FROM_TO` `*_delta` channels

**Goal.** The 26 delta channels (15 `translate_delta`, 6 `rotate_delta`, 5 `scale_delta`
install-wide) that ship as bare `{x, y, z}` vectors — and are therefore silently dropped by
`FromToMotion.Channel`, which only reads `{from, to}` pairs — get decided semantics and a live
implementation, or a documented disproof.

**Evidence (confidence: traced for the drop; lead-only for the semantics).**
`FromToMotion.cs:109-111` still routes all three delta channels through `Channel()` (re-verified
2026-08-03); the reader front-end (`AnimDefs.AddFromTo`) emits no delta channel at all. The
handler's delta arithmetic has **never executed**. Full evidence: `backlog.md` `BL-050`.

**Approach.** Census first, semantics second, code third: read `docs/formats/anim-definitions.md`
and all 26 actual payloads (which defs, which anchors, what values, what the surrounding events
do) before deciding what a bare vector means — a `{from,to}` pair is a tween; a bare vector is
*plausibly* the `to` with implied zero `from`, but that is a guess until the payloads argue it.
Write the census to `analysis/`. Then implement the decided reading in the reader front-end + 
`FromToMotion`, with a regression that actually reaches a live delta event (name the def and the
mission that fires it).

**Model recommendation.** high — semantics-deciding work where inventing meaning is the project's
most-repeated trap; the census must carry the decision.

**Verify.** A targeted run that dispatches at least one revived delta event, verified by
`--debug-anim` + capture at its site; 8-chapter regression for everything else (the 26 events are
the only behaviour allowed to change). Note: a regression that never reaches a delta event proves
nothing about `Seek`'s `rot *=` / `scale *=` lines — the targeted run is the real check.

**⚠ Traps.** (a) Do not make `Channel` fall back to `Vec3` before the semantics are decided — the
backlog's explicit rejected fix. (b) The `FromToMotion` docstring's "deltas compose on the HELD
pose" has **never been observed** — whoever revives them owns confirming or correcting it.
(c) If the census is ambiguous, the honest outcome is a documented open question + this item
closed as ❌-for-now, not an invented reading.

## B12 ☐ `BL-135` — the one-frame `CallSequence` dispatch lag

**Goal.** Every called sequence's first event currently fires one frame late (`CallSequence`
appends to `AnimInstance.Runners` at `AnimRuntime.cs:998` while `Advance` walks descending at
`:1485`). Either a bounded same-pass drain lands and the lag is gone engine-wide, or the item is
re-deferred with a fresh measured reason — not left ambiguous.

**Evidence (confidence: traced; fidelity question open).** Mechanism fully traced 2026-07-22; a
drain was already implemented once and measured **behaviour-neutral** (exactly one number moved
across all 8 chapters), then deliberately not kept because it wasn't what silenced the siren.
Whether the original dispatches a called sequence in the same tick is unknown — nobody has
checked. Full evidence + traps: `backlog.md` `BL-135`.

**Approach.** Re-implement the same-pass drain **with a bound** (a self-calling sequence must not
spin within one frame — cap drain iterations per frame and log on hitting the cap). Re-measure
neutrality across 8 chapters. If the re-measurement shows real movement beyond the known one
number, stop and re-defer with the measurement written to the backlog entry instead of landing.

**Model recommendation.** high — dispatch-timing change in the engine's hottest animation loop;
the bound's design is the item.

**Verify.** 8-chapter `--freecam` regression + `--debug-anim` diff against a baseline (conditions
log only on verdict flips, so a diff is meaningful); goldens should not move (timing, not
geometry) — if one does, that is the signal to stop and measure. Do **not** use the bootstrap
emitter census as the instrument — it cannot see post-bootstrap creations (`docs/verification.md`
LOG-2, learned on this exact bug).

**⚠ Traps.** (a) The descending walk is deliberate (`AnimRuntime.cs:250`) — do not "fix" by
iterating forwards. (b) The siren is already fixed (loader lifetime) — this item must not touch
that. (c) Behaviour-neutrality last time only says *our* output didn't change — it is not evidence
about the original; say so in the HISTORY entry either way.

# Wave C — instruments

## C21 ☐ `BL-039` — make the silent golden-run exit-1 capturable

**Goal.** If the unreproduced one-off recurs (a `c1-flight` golden built its world, rendered its
first frame, then exited 1 before frame 120 with no PNG, no exception, nothing in any log), the
next occurrence leaves enough evidence to diagnose — instead of nothing.

**Evidence (confidence: traced for the gap, lead-only for the cause).** 2026-07-30, during B7: 3
of 4 full gates that day passed with the identical pinned hash; the only in-code `Quit(1)` cannot
fire after a successful build. Full record: `backlog.md` `BL-039`.

**Approach.** Harden the harness, not the engine: on a golden-shot nonzero exit with no PNG, have
`RunTests.ps1`/the golden runner preserve the full `.out`/`.err`/log set under a dated
`.scratch/` failure folder (instead of them being overwritten by the next run), record the exit
code + last log line in the failure report, and consider a single automatic re-run with
`--verbose` when a golden shot dies silently. No engine code changes on a hunch.

**Model recommendation.** medium — PowerShell harness work; the discipline is in *not* touching
engine code.

**Verify.** Kill a golden run mid-flight by hand (or force a nonzero exit) and confirm the
evidence folder appears with the streams intact; a normal `RunTests.ps1` pass is unchanged.

**⚠ Traps.** Don't attribute the original failure to the concurrent-run collision — that failure
mode is instant (0.9 s, `docs/verification.md` LOG-13); this one died seconds in, alone.

## C22 ☐ `BL-070` — rewrite the static collider probe; explain the off-by-6/11

**Goal.** The collider probe (swept from `.scratch/`, no copy anywhere) exists again as a
committed `analysis/` instrument, reproduces the runtime collider counts in all 8 chapters, and
the C4 (-6) / C5 (-11) exemption-prediction gap is explained rather than benign-by-assumption.

**Evidence (confidence: traced for the surface to match; the gap itself unexplained).** The
surface is `WorldBuilder.NoCollisionNode` (`WorldBuilder.cs:69-70`) =
`MeshUsesTexture(n, IsNonSolidSkyTexture) || IsBillboardNode(n)`; likeliest divergence source is
`IsBillboardNode` (`:99-109`) plus subtree inheritance (`SceneBuilder.cs:337` region via
`BuildSubtree` at `WorldBuilder.cs:353`). Two later changes the probe must model: clutter
collision removed outright; city-block clutter uses merged/shared shapes. Full record:
`backlog.md` `BL-070` (second bullet).

**Approach.** Rewrite as a committed `analysis/collider-probe/` script + `FINDINGS.md` (repo
convention: no game data in it). Match the runtime walk including subtree inheritance and the two
later changes; then chase the C4/C5 delta node by node until it has a name.

**Model recommendation.** medium — careful reimplementation against a documented surface; the
judgement is in the delta chase.

**Verify.** Probe count == runtime collider count in 8/8 chapters (the runtime count is printed at
build; cite the log line), and the C4/C5 divergence either reproduced-and-explained in
`FINDINGS.md` or measured gone.

**⚠ Traps.** The line numbers above are from the backlog entry and predate recent refactors
(`PLAN-planeviewer-split` moved builder code) — re-locate `NoCollisionNode`/`IsBillboardNode` by
name, not by line. `--freecam` and golden modes build **no world colliders at all**
(`SessionSpec.BuildsCollision`) — probe against a mode that builds them.

# Wave D — decisions

## D31 ☐ `BL-133` — the C3 `cloud1`/`cloud2` magenta call

**Goal.** The user decides whether C3's retail-data gap (gamez references `cloud1`/`cloud2`, which
C3's own `texture.zbd` does not ship) keeps rendering diagnostic magenta or goes neutral gray via
`TextureArchive.KnownAbsentFromGameData` — and the decision lands, either as the one-line addition
or as a documented "stays magenta on purpose" note closing the item.

**Evidence (confidence: traced, 2026-07-21).** True in both the v0.6.1 and fork extraction trees —
a retail gap, not a cutover artifact. `KnownAbsentFromGameData` at `TextureArchive.cs:679` does not
contain the pair (re-verified 2026-08-03). The convention at stake: magenta means "our bug", and
suppressing it is a judgement call the backlog explicitly reserves for the user.

**Approach.** Present the trade-off (one sentence each way), take the decision, land it: either
add the two names + a C3 capture showing gray, or close the item as
won't-fix-by-design with the rationale in HISTORY and the backlog entry deleted.

**Model recommendation.** medium, low effort — a decision relay plus a one-liner.

**Verify.** If gray: a C3 capture at the referencing geometry shows neutral gray, and no other
chapter's magenta diagnostics change (the set is name-keyed — confirm neither name exists in other
chapters' archives). If magenta: the closing HISTORY entry is the deliverable.

**⚠ Traps.** Do not generalize the mechanism — every other magenta in the project stays diagnostic;
this set is strictly for measured retail gaps.
