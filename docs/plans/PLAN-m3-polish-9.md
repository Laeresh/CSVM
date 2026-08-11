# Milestone 3 — Polish 9: the 2026-08-05 playtest batch

**COMPLETE** (2026-08-06). Archived under `docs/plans/`; all ten items landed.

Ten items drawn from `backlog.md` under the criteria the user picked for this run: **unblocked and
visible** — no owed `CAP-nn` capture, no M4/menu-hub dependency, no pending user decision, a
diagnosed (or trivially diagnosable) cause, and a defect you can see at the controls. Eight are the
2026-08-05 at-the-controls triage batch (`BL-273`–`BL-280`, minus the CAP-gated `BL-281`); two are
the diagnosed effect defects filed the same day (`BL-257`, `BL-262`). Each was re-verified
still-open on 2026-08-05 against both `docs/HISTORY.md` (all ten are recorded there as
"banked"/"filed rather than fixed"; `BL-262` explicitly "stays open and untouched") and the backlog
entries themselves, which were written the same day.

Deliberately excluded: everything CAP-blocked (`BL-281`, `BL-250`, `BL-251`, `BL-110`, …),
everything M4- or menu-hub-blocked (`BL-222`, `BL-233`, `BL-181`, …), the DONE-code-wise
DONE-code-wise (`BL-254`), the TUNE-at-the-controls list (judged by the user, not
planned), the census-heavy `BL-258` (research-scale, fails the visible-per-effort test), and
`PLAN-m3-polish-8`'s Wave D (`BL-259`, `BL-270`), which runs in its own session.

## Milestone goal

When this plan is done, the defects the 2026-08-05 playtest actually saw are gone:

- Freecam and the labs behave: Space fires only one action, the orbital camera's distance sits on
  numpad `+`/`−` with `shift` decoupled, and the health slider zeroes armor.
- The crash scene's one remaining visual defect is fixed (the splash anchors at impact), the
  30-second fire burns 30 seconds again, and both plumes climb to the original's height.
- Two authored effects that today play nothing (`ballflare.flt`, `apassengers`) play, and the
  zeppelin destruction model's eight parts fly instead of vanishing.
- C1B/C2/C3 get a real skydome and the right fog, and the ambient cloud field is the authored
  `fogvol.zrd` data instead of our invented one.

**This plan fixes only what was seen and diagnosed — it does not widen any solve beyond its census,
re-open any settled decode, or touch anything gated on a capture or a user call.** Every one of
those boundaries is a named trap in some backlog entry; respect them per item.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen 2026-08-06) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — controls and lab one-liners

1. ☑ `BL-279` Unbind the freecam action from Space so guns and freecam stop sharing the key
2. ☑ `BL-280` Orbital camera: distance on numpad `+`/`−`, `shift` no longer drives camera and target together
3. ☑ `BL-278` The damage lab's health slider takes armor to 0 with it

### Wave B — the crash and fire scene

4. ☑ `BL-274` Anchor the crash ground splash at the impact point instead of the sliding wreck
5. ☑ `BL-276` `large_30sec_fire` burns 30 s again — find and fix the suspected `BL-212` regression
6. ☑ `BL-275` Fire plumes climb to the original's height (rise speed or per-puff lifetime)

### Wave C — authored effects and world data

7. ☑ `BL-262` Stage `ballflare.flt` and `apassengers`; re-pin what moves
8. ☑ `BL-257` Census the no-`RUN_TIME`/no-`BOUNCE_SEQUENCE` launch shape; make the zeppelin's eight parts fly
9. ☑ `BL-277` Select the sky/fog zone per chapter instead of hard-coding `zone2`
10. ☑ `BL-273` Read `fogvol.zrd` and render the authored cloud field, deleting `CloudPuffs`' invented one

## Dependency and parallelism notes

Wave A items are independent one-liners and can run in any order; A2 must check `BL-150`'s claim on
the numpad bindings before choosing keys. In Wave B, **B5 before B6** — a fire that quits at 5 s
cannot be judged for height — and B5/B6 both touch the puffer/fire path, so they are one session,
never parallel. B4 is independent of both.

Wave C is where the golden hashes move: C7 explicitly re-pins (`c1-destroy-effects`, `c1-crash`,
the census tallies), C8 may move effect censuses, and C10 changes every chapter's sky. **Land C7,
C8 and C10 sequentially, one re-pin each** — two golden-moving items in flight at once makes the
re-pin unattributable. C9 and C10 both touch the weather/sky path (`SessionSpec`/`Weather` vs. the
cloud field) — sequential, C9 first, since C10's playtest reads the sky behind the clouds.
`PLAN-m3-polish-8` Wave D (`BL-259`) runs in a concurrent session and owns the
`CALL_ANIMATION`/damage-effect machinery — do not land C7/C8 in the same window as D11 without
checking which goldens each moved.

---

# Wave A — controls and lab one-liners

## A1 ☑ `BL-279` Space is double-bound in freecam

**Goal.** In `--freecam` with weapons available, pressing Space triggers exactly one action — the
guns and the freecam action no longer fire together.

**Evidence (confidence: traced).** Found in the weapon lab (`PT-30` (d), 2026-08-05). The `V`
round-trip to the impact point and back was judged **correct** in the same pass, so the toggle
itself is not at fault; only the binding overlap is.

**Approach.** Find the freecam action bound to Space and move it to an unused key (check
`docs/controls.md` for the current map and update it in the same change — the project rule is that
player-input changes edit that file).

**Model recommendation.** Medium, low effort — a binding move plus a doc edit.

**Verify.** Launch `--freecam` with a weaponized plane spawnable, press Space: one action. Then the
in-engine suites via `.\RunTests.ps1` for regressions.

**⚠ Traps.** Don't "fix" it by disabling gun fire in freecam wholesale — the weapon lab fires from
freecam deliberately.

## A2 ☑ `BL-280` Orbital camera distance controls and `shift` coupling

**Goal.** The orbital camera's distance controls sit on numpad `+` / numpad `−` like the original,
and `shift` no longer drives the camera and the target point together.

**Evidence (confidence: traced — a user request, not a bug hunt).** From the weapon-lab sitting
(`PT-30` (e), 2026-08-05), and **about the orbital camera, not the lab's own controls** — the lab's
stand-off slider and re-parking were judged to work "really good".

**Approach.** Rebind the orbital camera's zoom to numpad `+`/`−`; split whatever `shift` currently
modifies so camera movement and target-point movement are separate inputs. Update
`docs/controls.md`.

**Model recommendation.** Medium, low effort.

**Verify.** At the controls in `--viewer`/orbit: numpad `+`/`−` changes distance, `shift` moves only
the one thing it should. This is interactive-feel work, so the user's next sitting is the real
sign-off.

**⚠ Traps.** `BL-150` (the numpad camera rebuild) owns the numpad bindings for the *flight* views —
check its entry before picking keys so this doesn't collide with the layout it will impose.

## A3 ☑ `BL-278` The damage lab's health slider does not zero armor

**Goal.** Dragging the health slider to 0 takes armor to 0 with it.

**Evidence (confidence: lead-only — no cause recorded).** At the controls (`PT-29`, 2026-08-05).
Everything else in the in-flight lab passed the same sitting: live HUD/dial/rattle response, smooth
dragging, scenery grazes dropping the slider on their own, **R** restoring 100 %, and "repair all"
clearing panels and trail without a stutter. So this is the one behaviour left, and the fix is
almost certainly local to the slider's write path.

**Approach.** Find the health slider's handler in the damage lab, see what it writes (HP but not
armor, presumably), and mirror whatever relationship the game's own damage path maintains between
the two — read how ordinary damage drives armor before writing anything.

**Model recommendation.** Medium — small, but it needs the judgement to copy the real damage
relationship rather than invent one.

**Verify.** F5 lab in `--fly`: drag health to 0, watch armor read 0; **R** still restores both.

**⚠ Traps.** None recorded; don't let the fix leak into the real damage path — the lab drives it,
not the other way around.

# Wave B — the crash and fire scene

## B4 ☑ `BL-274` The crash ground splash tracks the moving wreck

**Goal.** On a belly-slide crash, the ground splash plays where the plane hit and stays there while
the wreck slides on.

**Evidence (confidence: traced-by-family).** Seen at the controls (`PT-04`, 2026-08-05): the splash
drags along with the sliding plane. Same defect family as `BL-229`'s "puff at the plane's last
position" and the `TopLevel` anchor fix that produced the `trail-world-anchor` suite
(`docs/HISTORY.md` 2026-08-03) — the effect is parented to the aircraft rather than world-staged.
Everything else about the crash was judged **right** in the same sitting (sound, piece tumble
rate), so this is the one visual defect left in that scene.

**Approach.** Find where the crash ground splash is spawned/parented and give it the same
world-anchoring the trail fix used (`TopLevel` / world-staged at the impact point). Read the
`trail-world-anchor` suite and its HISTORY entry first — the pattern exists, reuse it.

**Model recommendation.** Medium, low effort — pattern reuse with a known reference fix.

**Verify.** `./RunGame.ps1` and belly-slide a crash: the splash stays put. The `trail-world-anchor`
suite and `c1-crash` golden guard the regression surface; run `.\RunTests.ps1`.

**⚠ Traps.** Anchor at the **impact point**, not the plane centre — `BL-060`'s trap about anchoring
at `pose.Origin` applies to the *breakup*, not this splash.

## B5 ☑ `BL-276` `large_30sec_fire` stops at ~5 s instead of 30

**Goal.** The fire behind ~1,035 death call sites burns its authored 30 seconds again.

**Evidence (confidence: direction traced, mechanism unconfirmed).** Confirmed at the controls
(`PT-22` (a) and (b), 2026-08-05): the fire visibly quits after ~5 s, and `PT-22`'s explicit check
"the fire still **ends at 30 s** (the `BL-212` halt must not have regressed)" came back **no**.
`BL-212` is the landed `STOP_SEQUENCE` halt and is the prime suspect — but it is a *suspect*, not a
finding. Diagnose before fixing.

**Approach.** Reproduce headlessly (destroy a building, watch the fire's runner in the logs), then
bisect the halt path: does `STOP_SEQUENCE` fire early, or does something else kill the runner?
`docs/HISTORY.md`'s `BL-212` entry records how the halt was verified when it landed — start from
what that verification would have caught and what it couldn't.

**Model recommendation.** High — a regression hunt in the animation runtime, where the instruments
are known to mislead; read `docs/verification.md` first.

**Verify.** Destroy a building and watch the full 30 s at the controls; headlessly, the runner's
lifetime in `.scratch/logs/`. Take a baseline of the failing behaviour first so the fix has
something to be measured against.

**⚠ Traps.** This is judged **separate** from the aircraft damage-stage puffers also stopping
early (user's call, 2026-08-05) — those are `PLAN-m3-polish-8` Wave D's authored-data rework. Do
not merge the two; a shared "puffers stop early" item would hide a regression inside planned work.

## B6 ☑ `BL-275` Fire plumes do not rise high enough

**Goal.** The crash fire and the destruction fire both climb to the original's height.

**Evidence (confidence: direction sound, magnitude TUNE).** Two independent sightings the same
sitting, judged as one defect (user, 2026-08-05): the crash fire "burns higher" in the original
(`PT-04`), and the destruction fire's flames "climb but not as high as the original" with the dark
plume "right but not high enough" (`PT-22` a/c). The suspect is the puffer's rise speed or per-puff
lifetime, not its size or count.

**Approach.** After B5 lands (a fire that dies at 5 s can't be judged for height), raise the
puffer's rise velocity and/or per-puff lifetime and A/B against the original's footage. The *what*
is settled; the *how much* is TUNE — record the chosen numbers in `backlog.md`'s TUNE list if the
user hasn't signed them off at the controls by landing time.

**Model recommendation.** Medium — parameter work with an A/B judgement.

**Verify.** Destroy a building and crash a plane in one flight; both plumes climb to the original's
height. Judge at the authored 1× sizes — `BL-282` landed 2026-08-05, so height reads are no longer
inflated by the 4× scale.

**⚠ Traps.** This supersedes `PT-22`'s original question, which asked whether the plume read too
*thin* (the `NUMBER` default, `BL-218`) — the answer at the controls was about **height**, so do
not fold a density re-tune into this.

# Wave C — authored effects and world data

## C7 ☑ `BL-262` Two anchor roots the bound effect defs ask for are staged by nothing

**Goal.** The torpedo explosion's flare (`ballflare.flt`, via `torpedo_ground_effect` /
`torpedo_water_effect`) and the crash's `apassengers` (`rem_pas`) actually play.

**Evidence (confidence: traced).** Found by `PLAN-effect-catalogue` B2's derivation
(`EffectCatalogue.StageRootsFor`), not by any census — the `effects-census` rows for both still
read as resolved, which is why B2 exists. Since B3 the hand tables are **gone**: both binds stage
`EffectCatalogue.WorldStageRoots`/`CrashStageRoots`, i.e. the closure MINUS the
`WorldStageRootGaps`/`CrashTemplateRootGaps` lists — **deleting the name from the gap list is the
whole fix: the root stages itself.**

**Approach.** Delete `ballflare.flt` from `WorldStageRootGaps` and `apassengers` from
`CrashTemplateRootGaps` in the same commit that re-pins whatever moves (the census tallies,
`c1-destroy-effects`, `c1-crash`). Also fix
`analysis/effect-anchor-roots/anchor_roots.py`, which keys defs by `ANIMATION_NAME` and misses
`ballflare.flt` (it declares only a `NAME`) — otherwise the next re-run repeats the miss.

**Model recommendation.** Medium — the edit is small; the care is in the re-pin and the offline
instrument fix.

**Verify.** `effects-census` fails in both directions if a listed gap stops being asked for or
turns up staged, so the marker cannot rot — after the delete, the census and the two goldens move;
re-pin them deliberately and record the before/after. Fire a torpedo and crash a plane to see both
effects exist.

**⚠ Traps.** Staging either is a **behaviour change**, not a table typo — B2 deliberately did not
make it so the goldens stayed hash-identical. Do this item alone in its landing window (see
dependency notes) so the golden movement is attributable.

## C8 ☑ `BL-257` The zeppelin destruction model's eight parts vanish on launch

**Goal.** `dblcannon_flying_parts`' eight pieces fly their authored arcs instead of being hidden on
the tick they are thrown — and every other event of the same shape install-wide gets the same fix,
scoped by a census.

**Evidence (confidence: traced for the repro; census owed for the scope).** The geometry and
visibility paths are fine (`--effects-test` reads `zep_ng_dstry1.flt` as 8/8 self-visible, root
revealed at the call site), yet 0 of 8 draw. The launches carry **no `RUN_TIME` and no
`BOUNCE_SEQUENCE`**; `MotionRuntime`'s solved-flight path (`BL-240`) is gated on a bounce being
named, so the event reports duration 0 and the null-start deactivation lands the same instant. A
third launch shape beside `BL-240`'s solved bounce and `BL-245`'s falls. Full record:
`analysis/bl-061-template-mesh/FINDINGS.md`.

**Approach.** Census first, exactly as `BL-240` had: how many events omit both fields, do they all
launch upward (the repro does: elevation 10–60°, speed 17–25 m/s, so an apex exists), and is the
following event the piece's own deactivation in every one. Only then extend the solved-flight gate
to cover the censused shape.

**Model recommendation.** High — widening a motion-solve gate is exactly where a plausible
over-generalization breaks `BL-245`'s falls; the census is the guard.

**Verify.** `--effects-test` on `zep_ng_dstry1.flt` (any chapter): the eight parts draw and fly.
Then the full suite — `BL-245`'s 379 falls must still *not* enter the solved path.

**⚠ Traps.** Do **not** widen the solve on the one repro case without the census — the backlog
entry is explicit. `BL-245`'s shapes (zero-gravity `chuteman`, downward throws) must stay out of
any apex-based solve; a parabola solve divides by zero on `chuteman`.

## C9 ☑ `BL-277` `SkyZone` hard-codes `zone2`, which is empty in C1B, C2 and C3

**Goal.** C1B, C2 and C3 render a real horizon dome and the fog their data actually authors for the
zone the world is built under.

**Evidence (confidence: traced, with per-chapter counts).** `SessionSpec.cs:176` sets
`SkyZone = "zone2"` chapter-wide; `Weather.ResolveZone` (`Weather.cs:165-166`) only falls back when
a zone is **absent**, and these chapters *define* `ZONE2` — but their `horizon/zone2` node is a
bare marker (`model_index: -1`, no children), so `WorldBuilder.BuildHorizon("zone2")`
(`WorldBuilder.cs:385-413`, called from `GameSession.cs:782`) builds a dome with zero meshes.
Horizon children per chapter: C1 1/4, **C1B 4/0**, C1C 1/4, **C2 3/0**, C2B 1/2, **C3 3/0**,
C4 1/4. The fog is wrong the same way: C3's `ZONE2` fog is night-blue on a daylight-lit mission
while `ZONE1` carries the matching grey; C1B's `ZONE2` fogs only 1128–1256 m. `BL-036`'s node
counts agree — C1B and C3 author their worlds under zone 1.

**Approach.** Select the zone per chapter by what the data itself shows — pick the zone whose
`horizon` subtree actually has children — rather than any lookup table. This also answers `BL-100`
for these three chapters.

**Model recommendation.** High — the selection rule is an inference about engine-side behaviour;
the wrong rule silently changes every chapter's sky.

**Verify.** Fly C1B and C3: a real dome appears, C3's haze reads as daylight grey, C1B stays fogged
above 1.2 km. Baseline screenshots of C1/C1C/C4 first — chapters whose zone2 is populated must not
change. `CAP-11` then judges the brightness (that capture stays owed; it gates nothing here).

**⚠ Traps.** Nothing on disk selects the zone — not the mission zrdr, not the 53 `.gw` scripts,
not the DLL strings (`docs/formats/weather.md:86-96`), and **not** `fogvol.zrd` (checked
2026-08-05). The rule is engine-side; wire it from the horizon subtree's own contents, don't invent
a lookup. `BL-036`'s guess-what-to-hide prohibition is about *geometry* — picking a sky/fog zone
whose dome is demonstrably empty is not that, but don't let this item grow into hiding zone
geometry.

## C10 ☑ `BL-273` `fogvol.zrd` is never read — the ambient cloud field is invented

**Goal.** The ambient cloud field renders the authored `fogvol.zrd` clutter — per-chapter sprite
templates, band, scatter, fades and sizes — and `CloudPuffs.cs`'s hand-tuned field is deleted, not
re-tuned.

**Evidence (confidence: traced).** Every chapter ships `extracted/<ch>/zrdr/fogvol.zrd.json` — a
fog-volume *clutter* spec scattering the gamez `cloudsprite`/`cloudsprite1`/`cloudsprite2`
template nodes, carrying `distance`, `perp_dist_range`/`perturb_dist_range`, `far_fade_range` and
`scale_range` — every knob `BL-118` currently guesses at. It is **entirely unconsumed**: nothing in
`CSVM/src`, `docs/`, or `analysis/` references it. `Clutter.cs` is driven by `interp.json`'s
`AddClutterTemplates` (`docs/formats/clutter.md:7-22`), a different path that never reads a zrdr
clutter block. The per-chapter differences match what the playtest saw: C1/C4/C5 name two sprite
templates, C2/C3 one — "single puffs at all heights, but not on every map" is authored behaviour.
*No capture owed* — the data carries the numbers (user's call, 2026-08-05).

**Approach.** Write the `fogvol.zrd` reader (with its `docs/formats/` page in the same change —
new decodes land with their docs), render the authored clutter, and delete `CloudPuffs.cs`'s TUNE
field (`Count 12`, `Radius 620`, `SizeMin/Max 90/200`, `BaseAlpha .06–.13`, `BandBelow/Above
120/280`, `VertFull/Fade 200/560` — all to be deleted, not re-tuned). `BL-118` then re-scopes to
the `CloudDeck` mesh brightness plus a post-landing density judgement.

**Model recommendation.** High — a new format reader replacing a live visual system; the largest
item in the plan.

**Verify.** Fly C1 and a one-sprite chapter (C2 or C3) at several altitudes and past the deck: the
singles thin with the data, and the deck carries its own dense layer. Golden hashes will move —
re-pin in this item's own landing window.

**⚠ Traps.** The original's picture is *two* things — sparse singles at any altitude **and** a
dense layer hugging the deck. One uniform field reproduces neither; do not tune our way to the
average. Sequenced after C9 — the playtest for this item reads the sky behind the clouds.
