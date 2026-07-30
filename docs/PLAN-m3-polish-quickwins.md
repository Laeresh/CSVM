# M3 polish — quick wins, code-verifiable

**ACTIVE PLAN** (written 2026-07-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans.md), when every item lands.

Ten items drawn from `backlog.md` under two user-chosen criteria (2026-07-30): **quick wins** (small,
root cause already pinned or an explicit fix shape written down) and **code-verifiable only**
(acceptance checkable via tests, logs, deterministic screenshots, or doc diffs — no judge-by-eye
against the original, no TUNE-by-feel). That excludes the whole muzzle-flash/tracer/explosion *look*
family (`BL-011`/`BL-012`/`BL-015`/`BL-016`/`BL-018`), everything blocked on future milestones
(menu hub, cutscene player, M4 configurator), the high-diagnosis-risk `det==0` (`BL-007`), and
`BL-047` (its fix waits on an original-game crash capture). Still-open status: the six `m3-polishing`
fixes (`BL-001`–`BL-006`) were confirmed landed on `main` (code present) and are NOT in this plan;
`BL-026` and `BL-159` were re-verified open against the code directly; the rest were verified
against `backlog.md`/`docs/HISTORY.md` — per Ground rules, confirm each trace before implementing.
Item A3 (`BL-025`) was added by the user on 2026-07-30 — an M-size exception to the quick-win
criterion, kept because the user settled its open fidelity question by decision (per-pylon cycling
must work even with uniform ammo types). Whether the gauge arrow *animates* to the new slot is
`BL-184`, blocked on capture `CAP-18` (`playtest.md`) and deliberately not in this plan.

## Milestone goal

- The cockpit's ammo gauges and rocket cues behave per the confirmed original behaviour (gun-only
  yellow tier; a dry rocket pull always sounds its cue).
- Zero known spurious engine errors in sound-enabled runs, and no flag combination that hangs a
  scripted run forever.
- The inspection labs (node lab, anim-lab) reflect true state and can reach the player plane.
- Weapon-sprite orientation and flight-audio tuning are mechanically correct/wired; their *look*
  and *magnitude* stay TUNE items in `backlog.md`.
- `docs/cli.md` and `docs/formats/weapon-effects.md` match what the code/data actually says.

**No item in this plan is accepted by eye against the original.** Anything whose acceptance is
aesthetic stays in `backlog.md` for a criteria-appropriate future run — this plan deliberately
lands only what a script, log grep, or doc diff can prove.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
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

### Wave A — cockpit gauges & cues

1. ☑ `BL-024` Split gauge colouring: yellow tier for guns only, hardpoints green→red
2. ☑ `BL-026` Rocket empty-clip cue swallowed by the fire-rate cooldown
3. ☑ `BL-025` H selects individual hardpoints, even when all carry the same ammo type

### Wave B — spurious errors, hangs, and lab correctness

11. ☐ `BL-040` `!is_inside_tree()` error on every sound-enabled world bind
12. ☑ `BL-049` `--headless` + `--screenshot` NREs forever instead of failing loudly
13. ☑ `BL-044` Node lab: hidden subtree's tree row doesn't reflect live `Visible` state
14. ☑ `BL-043` Player plane unreachable by selection in `--anim-lab`

### Wave C — mechanically-wrong weapon/audio plumbing

21. ☐ `BL-139` Muzzle-flash quad sits in a fixed world plane, not the aircraft's basis
22. ☐ `BL-159` Wire `WhineMixGain` through `Config` like every other TUNE constant

### Wave D — doc drift

31. ☐ `BL-031` `docs/cli.md` documents 89 flags, the parser accepts 92
32. ☐ `BL-140` `weapon-effects.md` "confirmed in C1" overclaims meshless `gunshell`/`muzzle_burst`

## Dependency and parallelism notes

No cross-wave dependencies; waves are thematic, not ordered. File contention: A1 and A3 both touch
`GaugeCluster.cs` (A1 the colour functions, A3 the missile gauge's `Selected` state) and A2 and A3
both touch `FlightController.cs`'s rocket path — land Wave A sequentially, A1 → A2 → A3, not in
parallel worktrees. B13 and B14 may both touch the lab/selection code — also sequential. C21 (`Projectile.cs`) is
independent of everything else. D31/D32 are doc-only and can run any time. B12 should land early —
it hardens the scripted-run harness every other item's verification leans on.

---

# Wave A — cockpit gauges & cues

## A1 ☑ `BL-024` Split gauge colouring: yellow tier for guns only

**Goal.** Gun ammo belts keep the green→yellow→red steps; hardpoint/ordnance pylon indicators step
green→red with no yellow, ever — matching the user's A/B against the original (playtest pass 2,
finding 9a).

**Evidence (confidence: traced).** `DrawWeaponGauge` (`GaugeCluster.cs:596-615`) calls one shared
`IndicatorColor` (`:408`) for both `_gunGaugeGeom` and `_missileGaugeGeom` (call sites `:274-283`).
`IndicatorLowFrac` (`:76`) is load-bearing **for guns**; its code comment justifies it by the
3-round-pylon case — exactly the case that must now show no yellow.

**Approach.** Split into a 3-state gun colour function (keeps `IndicatorLowFrac`) and a 2-state
hardpoint function (green above 0, red at empty). Rewrite the `IndicatorLowFrac` comment to its
surviving gun-only justification.

**Model recommendation.** sonnet — the fix shape is fully specified; mechanical split.

**Verify.** `--run-tests` green; a deterministic assertion (in-engine suite or targeted screenshot
with pinned ammo state) that the hardpoint colour function never returns the yellow constant at any
fraction, and the gun function still does below `IndicatorLowFrac`.

**⚠ Traps.** (a) Do not delete `IndicatorLowFrac`. (b) Do not reuse it (even retuned) for
hardpoints — the original has no intermediate colour there at all. (c) `docs/architecture.md`'s
`GaugeCluster.cs` entry is at its 3-⚠ cap — merge into the existing per-GROUP/per-PYLON
digit-readout bullet, don't add a 4th. (d) Re-tuning 0.34 for guns is `BL-142`, a separate
by-eye TUNE — out of this plan's scope; leave its backlog entry in place.

## A2 ☑ `BL-026` Rocket empty-clip cue swallowed by the cooldown

**Goal.** A dry rocket-trigger pull sounds the empty cue (once, via `_rocketDryWarned`) even when
pulled within the fire-rate cooldown window of the last shot.

**Evidence (confidence: traced, re-verified in code 2026-07-30).** The pull handler returns early at
`FlightController.cs:1206` (`if (!pull || _rocketCooldown > 0f) return;`) before the dry branch can
run, so a pull within `1/FireRate` s of the last shot is silent.

**Approach.** Evaluate the all-pylons-empty case before (or independently of) the cooldown gate so a
dry pull reaches the cue; live rounds keep the cooldown exactly as-is.

**Model recommendation.** sonnet — one-gate reorder with a pinned line.

**Verify.** Scripted run: drain rockets (12 max launch counter helps), then pull within 1 s of the
last shot and grep the run's log for the cue event firing exactly once per dry state. Full
`--run-tests` for regression; confirm firing cadence with live ammo is byte-identical in the log.

**⚠ Traps.** Don't let the dry check fire the cue on every held frame — `_rocketDryWarned` is the
existing latch; keep it. The backlog's original line numbers (`:782-793`) predate refactors — the
live gate is at `:1197-1209`.

## A3 ☑ `BL-025` H selects individual hardpoints, even with uniform ammo

**Goal.** H cycles through the plane's individual hardpoints (each pylon counts for itself, and its
slot shows on the missile gauge's arrow), regardless of whether the pylons carry different ammo
types; the selected pylon drains, auto-advancing only when it empties. User decision 2026-07-30 —
this supersedes both `BL-010`'s old "nothing to fix in M3" verdict and the backlog's
confirm-the-original caveat.

**Evidence (confidence: traced for the gate; design settled by user).** The input is wired and
mutates state (`FlightController.RocketSelectPressed` → `CycleWeaponSelectors`) but is gated
`_ordnanceTypes.Length > 1`; all 11 stock loadouts carry one hardpoint type (`wep_06`), so after
dedup there is never anything to cycle. The gauge side already supports display:
`WeaponGaugeState.Selected` rotates the `mgarrow` pointer (`GaugeCluster.cs:613-614,668`) — it just
needs to be fed the selected pylon index instead of the ordnance-type index.

**Approach.** Replace ordnance-*type* cycling with *pylon* cycling: H moves `_nextPylon` (or a new
selected-pylon field) to the next non-empty hardpoint; firing drains only the selected pylon;
auto-advance fires only when it empties — composing with the landed `BL-001` keep-cursor drain fix.
Feed the missile gauge's `Selected` from the selected pylon.

**Model recommendation.** opus — a selection-state redesign that touches firing order; the
largest item in the plan.

**Verify.** Deterministic state assertions in a scripted run: H presses step through pylon indices
on a uniform-ammo loadout (log); rockets source only from the selected pylon until it reports
empty, then exactly one auto-advance; the gauge's `Selected` matches the pylon index each step.
`--run-tests` green; A2's dry-pull cue still fires with all pylons empty.

**⚠ Traps.** (a) Do not implement any arrow *sweep* animation — whether the original animates the
pointer is `BL-184`, blocked on capture `CAP-18`; here the arrow may keep snapping. (b) Keep
`BL-001`'s drain order as the behaviour when the player never touches H. (c) The missile gauge has
8 belt positions (`geom.Positions`) — check the mapping against planes with fewer pylons before
assuming index == slot. (d) Coordinate with A1/A2: shared files, land Wave A sequentially.

# Wave B — spurious errors, hangs, and lab correctness

## B11 ☐ `BL-040` `!is_inside_tree()` error on sound-enabled world bind

**Goal.** A sound-enabled world bind emits zero `!is_inside_tree()` errors, and bootstrap one-shot
sound emitters are positioned at their real world position, not the identity origin.

**Evidence (confidence: traced).** `AnimRuntime.Bind` → `Bootstrap` → `RunAmbientPasses` → `Start`
dispatches a `SOUND` event while the subtree is out of the tree; `OneShotSoundPosition`
(`AnimRuntime.cs:1405`) reads `GlobalTransform` → Godot logs the error and returns identity.
Measured 2026-07-25 on C3 `--freecam`: 1 error, 0 with `--mute`.

**Approach.** Either defer the bootstrap's one-shot sounds until the subtree enters the tree, or
compose the node's local transform chain as a fallback and log that it did. Read
`docs/architecture.md`'s `AnimRuntime.cs` entry first — this file is the repo's heaviest.

**Model recommendation.** opus — small change but inside `AnimRuntime`'s bootstrap ordering, where
blast radius is high.

**Verify.** Grep a **sound-enabled** (not muted) C3 `--freecam` run's full stderr for the error
string — must be absent; assert the emitter's position is non-origin in the log. 8-chapter freecam
regression, sound-enabled.

**⚠ Traps.** A muted regression run cannot see this — every earlier zero-baseline ran muted. Do not
fix by suppressing the read; the emitter really is misplaced. Same class as verification WORLD-11.

## B12 ☑ `BL-049` `--headless` + `--screenshot` hangs forever

**Goal.** The combination fails loudly and exits nonzero (or is rejected at arg-parse with a clear
message) instead of NRE-looping and never quitting.

**Evidence (confidence: traced).** The capture block's `GetViewport().GetTexture().GetImage()`
returns null under the dummy renderer; the caught-and-logged NRE means the frame counter never
advances — one run produced a 206 MB stderr log and an orphan process.

**Approach.** Prefer rejecting the combo at arg-parse time (cheapest, clearest); optionally also
null-check the capture and `Quit(1)` as a backstop for future renderer-less paths.

**Model recommendation.** sonnet, low effort — a parse-time guard.

**Verify.** Run the combo: process must exit promptly and nonzero with the message; no orphan
(check the process list after), no growing log. Normal windowed `--screenshot` still captures
(golden manifest hashes unchanged).

**⚠ Traps.** The failure is silent from the caller's side — the wrapper exits 0 while the real
child keeps running and **holds the log handle, contaminating the next run's logs** (this once
manufactured a bogus 16,576-error reading). Verify by process list, not exit code alone.

## B13 ☑ `BL-044` Node lab hide action doesn't update the tree row

**Goal.** A hidden subtree is visibly hidden in the node lab's tree row (text/colour), and a node an
animation re-shows reads visible again on its own.

**Evidence (confidence: traced-behaviour).** The world node toggles and the button text flips, but
the row's text/colour never change. Root cause is the row rendering latched state instead of live
`Visible`.

**Approach.** Make the row reflect live `Visible` state (poll or notify on the panel's existing
refresh cadence), not what the button last did.

**Model recommendation.** sonnet — contained UI-state fix in the lab panel.

**Verify.** Scripted/deterministic: hide a subtree via the lab, assert row text/colour matches
`Visible == false` programmatically (or a `--det` screenshot of the panel); trigger a def that
re-shows the node and assert the row flips back without user input.

**⚠ Traps.** A def re-showing a user-hidden node is *correct* — don't "fix" that by latching the
button state harder. The `agyrobus` framing struggle mentioned in the same entry is separate and
minor; don't scope-creep into it.

## B14 ☑ `BL-043` Player plane unselectable in `--anim-lab`

**Landed 2026-07-30.** Discrimination (temp log) confirmed cause #2: the parked `--plane=` prop hangs
on `_worldRoot` as a sibling of the world-content root, so the selection/node-lab walk rooted at the
content never visits it; its 15.9 m diagonal is far under the 350 m cap, so cause #1 never fires.
Fix extends the walk rather than special-casing: a shared `_selectionExtraRoots` list feeds
`SelectionService.ExtraRoots` (walked after the world root; each caps its own ancestor ladder) and
`NodeLab.ExtraRoots` (top-level branch + name index). Verified: a scripted click lands on
`player_bhawk` (ladder stops at the plane root, rung 6/6); node-lab `SelectByName` selects it; the
mesh lab binds all 49 of its surfaces. Freecam unaffected (empty list = byte-identical path); full
suite + 13 goldens green.


**Goal.** Clicking (or the node lab's `Select` path) can select the parked player plane in
`--anim-lab`, so the mesh lab's light sliders can act on it.

**Evidence (confidence: lead-only — two named candidate causes).** D31's picking skips meshes above
a 350 m world-AABB diagonal and only walks the world content root; one of the two excludes the
plane. Not yet discriminated.

**Approach.** First determine which exclusion fires (log both predicates for the plane's meshes),
then fix that path — the node lab's `Select` is the existing fallback for anything a click can't
reach; prefer extending the walk over special-casing the plane.

**Model recommendation.** opus — small but starts with a discrimination step; a wrong guess adds a
special case the entry warns against.

**Verify.** Scripted `--anim-lab` run: select the plane, assert selection lands on the aircraft
subtree (log), and that a light-slider change shades the plane's materials not the world's (probe
screenshot A/B under `--det`). World click-selection regression: freecam click-select still works.

**⚠ Traps.** Check which of the two exclusions actually fires **before** adding any special case.

# Wave C — mechanically-wrong weapon/audio plumbing

## C21 ☐ `BL-139` Muzzle flash: plane-local orientation, not world-locked

**Goal.** The muzzle-flash quad is oriented in the firing aircraft's x/y basis (rolling with the
plane), instead of one fixed world plane.

**Evidence (confidence: traced).** `RenderSprites` (`Projectile.cs:405`) builds the basis from
global axes; the `Sprite` struct (`:662-669`) has no orientation field. Billboarding was
deliberately disabled (`:135-136`) — that intent stands; the world basis that replaced it is the
defect.

**Approach.** Add a basis/orientation field to `Sprite`; feed the firing plane's basis at spawn for
muzzle flashes. Leave the flash's size/texture alone (`BL-011` is by-eye TUNE, out of scope).

**Model recommendation.** opus — one field, two consumers with different correct suppliers; easy to
wire wrong.

**Verify.** Deterministic geometric check: in a `--det` scripted banked pass, assert the spawned
sprite's stored basis equals the aircraft basis at spawn (log/probe), plus two `--det` screenshots
(level vs. banked) whose flash orientation differs — an objective content check, not an aesthetic
one. Golden-manifest run for regressions.

**⚠ Traps.** (a) Impacts share `RenderSprites` but want the **surface normal**, not the plane
basis — one field, two suppliers; do not wire them identically. (b) Fix the stale class comment at
`Projectile.cs:132-133` ("round billboards") in the same pass. (c) `Uv1Scale = (-1,1,1)` at `:429`
mirrors all three sprite types — confirm that was intended for all three before touching it.

## C22 ☐ `BL-159` Wire `WhineMixGain` through `Config`

**Goal.** `flightAudio.whineMixGain` is live-tunable via `config.json` and appears in
`--dump-config`, following `FlightModel`'s established pattern; the default (0.12) is unchanged.

**Evidence (confidence: traced, re-verified in code 2026-07-30).** `FlightAudio.cs:30` hardcodes the
const, read at `:144`; `FlightAudio` has no `Config` calls. Pattern to copy:
`Config.GetFloat("flightModel.pitchTune", PitchTune)` (`FlightModel.cs:141`).

**Approach.** `Config.GetFloat("flightAudio.whineMixGain", WhineMixGain)` at the read site in
`Update`. For `--dump-config` coverage, check whether `Config.WarmTuningRegistry`
(`Config.cs:200-215`) can register the key directly rather than constructing a throwaway
`FlightAudio` (which needs a `SoundArchive`).

**Model recommendation.** sonnet — pattern-copy with one registry wrinkle.

**Verify.** Config round-trip: set the key in `config.json`, assert the read value changes (log);
`--dump-config` emits the key. Default-path golden/audio behaviour unchanged. The *magnitude*
retune ("a bit louder") stays a TUNE in `backlog.md` — not this item.

**⚠ Traps.** (a) `WarmTuningRegistry` won't emit the key for free — verify, don't assume. (b) The
git-ignored `CSVM/config.json` silently overrules interactive runs while `--det` drops it
(verification DET-8) — **delete any whine-gain override after testing**.

# Wave D — doc drift

## D31 ☐ `BL-031` `docs/cli.md`: 89 documented flags vs 92 parsed

**Goal.** Every parser-accepted flag has exactly one description-of-record bullet in `docs/cli.md`
(or the file's own rule gets a written exception), and CLAUDE.md's "day-to-day 28 of N" number has a
defined meaning.

**Evidence (confidence: traced, found by the SessionSpec flag-count check).** Undocumented:
`--debug-colliders`, `--jitter=<deg>`, `--sky-zone=<zone>`. Indexed but bullet-less: `--direction`,
`--spawn-dir`.

**Approach.** This is authoring, not tidying: read each flag's implementation before writing its
bullet (each bullet is the description of record). Decide whether `--direction`/`--spawn-dir` get
their own bullets or a written exception to the one-bullet rule. Then settle what CLAUDE.md's count
tracks (index vs parser) and fix the number once.

**Model recommendation.** opus — the bullets are normative documentation and need the code read.

**Verify.** Re-run the flag-count check that found the drift: parser count == cli.md index count ==
bullet count (or the written exception accounts for the gap). No behaviour change to verify.

**⚠ Traps.** The "28 of 89" was silently wrong before — decide which count it means *before*
"fixing" it. Writing a bullet from the flag's name instead of its code is how the drift happened.

## D32 ☐ `BL-140` Correct `weapon-effects.md`'s meshless-root overclaim

**Goal.** `docs/formats/weapon-effects.md` distinguishes "the name resolves to a gamez node" from
"the node carries a mesh" for `gunshell`/`muzzle_burst`, verified across all 8 chapters.

**Evidence (confidence: traced/measured for C1).** C1 `nodes.json` node 204 (`gunshell`) and node
166 (`muzzle_burst`) carry `model_index: -1` and zero real children — same shape as the documented
empty template roots (`world-structure.md:6`). The doc (`weapon-effects.md:124-135`) lists both as
"confirmed in C1" alongside genuinely-meshed roots.

**Approach.** Script-check the other 7 chapters' `nodes.json` for the two node names' `model_index`/
children (an `analysis/` script if the result will be cited); then reword the doc with the footnote.
Cross-ref `BL-137` so the ejection work inherits the corrected claim.

**Model recommendation.** sonnet — a measurement sweep plus a scoped doc edit.

**Verify.** The measurement output (all 8 chapters) recorded; doc diff carries the footnote; the
CC-BY page still reads as validated reference (no speculation added).

**⚠ Traps.** Do not assume `shell1.png`/`shell2.png` fill the gap — `BL-141` traced them to the
unrelated `rabbit_blur` mesh chain. This item corrects the doc; it does not build a casing.
