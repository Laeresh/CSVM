# Milestone 3 — Polish run 10: quick-win bug fixes

**ACTIVE PLAN** (written 2026-08-06). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

Ten open `[Bug]` items from `backlog.md`, selected 2026-08-06 by the criterion **quick wins / low
risk**: small, well-diagnosed fixes with a clear fix shape, favouring many landings over one deep
dig. Each was re-verified still-open on 2026-08-06 against the record (`git log --grep=BL-NNN` —
every hit is a minting/bookkeeping commit, no landings) and against `backlog.md` (which deletes on
landing). **Deliberately excluded:** flight-model bugs (`BL-092`, `BL-247` — the flight constants
are pinned, coupled measurements guarded by the `flight-envelope` suite; not low-risk), bugs
needing an original-game A/B or a new capture before any code can move (`BL-070`, `BL-034`,
`BL-284`, and everything `[Blocked: CAP-nn]`), `[Blocked: M4]` items (future milestone), and
`[Owed-playtest]` items (code done; they need the user at the controls, not a plan).

## Milestone goal

- Torn-panel flake debris fires only at the tear moment and separates from the plane in world
  space (`BL-288`).
- The fuel-leak animation and the wing-light blinker stop fighting over the same nodes (`BL-287`).
- The crash water splash sprays up and its rings lie flat on the water (`BL-292`).
- The runway light-state quads and the C1 rail patch stop z-fighting (`BL-081`, `BL-082`).
- The own-ship audio mix loses its unexplained blanket ×0.2 (`BL-268`).
- The weapon gauge maps slots to pylon numbers and sweeps the original's way (`BL-294`); the
  damage display no longer latches all-red on a crash (`BL-047`).
- Flying C1/M05, C3/MP1–2 or C5/MP1 places the interp-scripted entities where authored (`BL-249`).
- The sandbox-only free-flight exit hang is diagnosed or bounded (`BL-283`, stretch).

**This plan fixes only what is already diagnosed — no new reverse engineering, no flight-model or
TUNE changes.** An item that turns out to need an original-game capture stops and records that,
rather than growing into a research dig.

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

### Wave A — Damage & crash effects

1. ☐ `BL-288` — `gimmeflakes` fires only at a panel tear, and the flakes separate in world space
2. ☐ `BL-287` — the fuel leak stops fighting `WingLightBlinker` over `wing_flare2`/`winglight2`; confirm panels 4/6 burn flakes-only as authored
3. ☐ `BL-292` — the crash water splash orients to the struck surface: spray up, rings flat

### Wave B — HUD & world rendering

11. ☐ `BL-294` — weapon-gauge slots map to pylon numbers (1,5,2,6,3,7,4,8 fill) and the sweep direction matches the original
12. ☐ `BL-047` — the damage display no longer latches fully red on a crash
13. ☐ `BL-081` — runway `lite*`/`ltout*` state-variant quads pick one light state instead of z-tying
14. ☐ `BL-082` — the C1 rail-over-transition patch draws above its transition

### Wave C — Audio & missions

21. ☐ `BL-268` — remove/replace the blanket ×0.2 over authored sound volumes in `FlightAudio`
22. ☐ `BL-249` — `MissionSetup` applies `Object3DTranslate`/`Object3DRotate`, with a per-script angle-unit decision

### Wave D — Stretch

31. ☐ `BL-283` — diagnose the sandbox-only free-flight `--screenshot` exit hang (timeboxed)

## Dependency and parallelism notes

All ten items are independent — no item blocks another; waves are theme groups, not dependency
chains, and run in any order. File contention: A1 and A2 both touch the damage-visuals wiring
(`DamageVisuals`/`FlightRigAssembler` neighbourhood) — land them sequentially, not in parallel
worktrees. B12 touches `GaugeCluster.cs`, as does B11 — same rule. C21's ×0.2 sits on the paths
`BL-285`'s owed listen A/B will judge; note in the landing message that the mix changed so that
playtest is re-based. D31 is diagnosis-only and can run any time in the background; it must not
block the plan's completion — if timeboxed out, it stays in `backlog.md` unchanged.

---

# Wave A — Damage & crash effects

## A1 ☐ `BL-288` — `gimmeflakes` fires once at the tear and separates in world space

**Goal.** Taking panel damage in flight throws a single flake burst at the moment a panel tears;
the flakes separate from the plane, decelerate in world space and fall behind. A fuel-leak-only
stage throws none.

**Evidence (confidence: traced for the symptom, lead for the over-fire mechanism).** Seen at the
controls 2026-08-05 (backlog `BL-288`). (a) Flakes fly on EVERY damage animation, but the authored
menu calls `gimmeflakes` only from the `pdpanelN` defs — `player_fuelleak` calls none. The
over-fire may be a re-CALL of the whole `pdpanelN` def rather than the flakes — check what our
wiring invokes before fixing. (b) The flakes stay plane-local — the third bite of the
plane-parented-effect trap (`BL-229`, the smoke-trail `TopLevel` fix of 2026-08-03 and its
`trail-world-anchor` suite are the family).

**Approach.** First trace what invokes `gimmeflakes` (log the dispatch per damage stage in the F5
lab). Fix (a) at the call site so only a panel-tear transition fires it. Fix (b) with the proven
family fix: world-stage the emission (`TopLevel` at the site), never per-frame reparenting.

**Model recommendation.** medium — mechanical once traced; the trap family is documented.

**Verify.** F5 damage lab in `--fly` at a real mission spawn and heading (the identity pose hides
the parenting bug — `docs/verification.md`); step damage stages: burst only on a tear, flakes fall
behind. Then `.\RunTests.ps1` incl. the `trail-world-anchor` suite and goldens.

**⚠ Traps.** Do not mute `gimmeflakes` to fix (a) — the tear moment must still throw its burst.
Verify off-origin; the identity pose masks plane-local anchoring.

## A2 ☐ `BL-287` — fuel leak vs wing lights; panels 4/6 flakes-only check

**Goal.** `player_fuelleak`'s deactivation of `wing_flare2`/`winglight2` and `WingLightBlinker`'s
1.5 s re-assert stop fighting — one owner wins deliberately. Secondarily, confirm (not change)
that `pdpanel4`/`pdpanel6` burn flakes-only as authored.

**Evidence (confidence: traced).** Filed at `BL-259`'s landing 2026-08-05 (backlog `BL-287`).
(a) The ELSE branch of `player_fuelleak` deactivates the two nodes; the blinker re-asserts every
blink cycle. (b) Panels 4/6's `short_firetrail` calls sit in an `ON_CALL` sequence (`view_result`)
nothing calls — as authored they play flakes + skin flip with no burn.

**Approach.** For (a): make `WingLightBlinker` respect an externally-deactivated node (or suspend
the blinker while the leak def is active) — pick the reading that matches the authored intent of
the def, not a z-order hack. For (b): no code — record the as-authored verdict in the landing
message; an original-game check is only owed if footage ever shows those panels burning.

**Model recommendation.** medium — small arbitration fix, but the "who owns the node" call needs
judgement against the def.

**Verify.** F5 lab: drive to the fuel-leak stage, watch the wing lights hold their commanded state
across several blink cycles; `.\RunTests.ps1`.

**⚠ Traps.** Don't "fix" (b) by wiring `view_result` — nothing calls it in the data; that would be
inventing content.

## A3 ☐ `BL-292` — crash water splash orients to the surface

**Goal.** A dive into open water plays `plane_big_splash` with the spray column firing straight up
and the flat rings lying in the water plane; fire and steam stay correct.

**Evidence (confidence: direction sound; the mechanism is a stated hypothesis).** PT-37 sea dive,
2026-08-06 (backlog `BL-292`). Hypothesis, not observation: the effect root inherits the crashed
plane's attitude — would be the fourth bite of the plane-parented-effect trap. **Verify the actual
transform at spawn before assuming the angle.**

**Approach.** Instrument the crash-effect spawn (log the root basis), confirm or refute the
inherited-attitude hypothesis, then orient the splash sub-effects to the struck surface normal
(straight up on water). Re-check the ground/dirt crash variant in the same landing — same spawn
path, and a tipped effect is harder to spot against terrain.

**Model recommendation.** medium.

**Verify.** Scripted dive into C1B water (`--pos`/`--direction` toward open sea), `--screenshot`
series through the splash; then a dirt-crash capture for the shared-path regression; goldens
(`c1-crash` is pinned — expect and re-pin only if the dirt path legitimately changes).

**⚠ Traps.** `BL-293` (rocket impact rings) is a different spawn path — template meshes, not crash
choreography — and stays a separate item; do not fold it in. The upper HE ring's fixed axis is
CORRECT per the original — don't "fix" ring orientation globally.

# Wave B — HUD & world rendering

## B11 ☐ `BL-294` — weapon-gauge pylon mapping and sweep direction

**Goal.** The weapon gauge shows slots at their PYLON numbers — empty pylons leave gaps, fill
order alternates per wing (1,5,2,6,3,7,4,8) — and cycling sweeps the same direction as the
original (clockwise on the Balmoral's full 8).

**Evidence (confidence: traced on the symptom).** PT-31, 2026-08-06 (backlog `BL-294`): ours
compacts used hardpoints into indices 1..N and sweeps counterclockwise. Analysis order is given:
check the dial's index winding first — a mirrored slot→angle map explains the direction flip
without touching `BL-184`'s landed tween rate.

**Approach.** In the gauge (`GaugeCluster.cs` neighbourhood): slot index = pylon number, un-mirror
the slot→angle map. Reconcile with `CAP-18`'s measured CCW antipode step before changing anything —
on a full 8-slot dial, 1→5 in alternating order IS the 180° antipode case, so that measurement may
describe exactly those steps rather than a general CCW rule.

**Model recommendation.** medium.

**Verify.** Balmoral full stock loadout: cycle through all 8, screenshot each — clockwise sweep,
gauge index equals pylon. A partial-loadout plane (gaps visible) as the second case.
`.\RunTests.ps1`.

**⚠ Traps.** Do not retune the `BL-184` tween rate. `CAP-18`'s measurement is not automatically
wrong — re-read it under the antipode framing first.

## B12 ☐ `BL-047` — crash damage display no longer latches all-red

**Goal.** After a crash, the damage display shows whatever the original shows — not the ordinary
damage path left latched fully red.

**Evidence (confidence: traced).** Backlog `BL-047`: `GaugeCluster.cs` blinks a zone on
`OnPartDamage` and picks red at `frac <= RedAt`, but `FlightController.Crash()` never calls into
`Gauges` — the all-red is the ordinary damage path latched, not a crash behaviour mis-firing.

**Approach.** First establish what the original shows on a crash — `CAP-16`/`CAP-14` crash clips
in `playtest/` and `OriginalScreenshots/` may already answer it (the original cuts to menu ~0.35 s
after a fatal crash, so "nothing" is a plausible answer); ask the user only if the footage doesn't
settle it. Then wire `Crash()` to the gauge accordingly (freeze, clear, or all-red-blink —
whichever is observed).

**Model recommendation.** medium, low effort — tiny wiring once the observed behaviour is settled.

**Verify.** Scripted crash + `--shots` through the impact frames; compare the gauge region against
the chosen reference. `.\RunTests.ps1`.

**⚠ Traps.** Don't invent a "crash display state" the original doesn't have — the footage decides.

## B13 ☐ `BL-081` — runway light-state quads stop z-tying

**Goal.** The runway `lite*`/`ltout*` lights-on/lights-off state-variant quads no longer z-fight:
the engine picks one state's variant and suppresses the other.

**Evidence (confidence: traced on the cause).** Backlog `BL-081`: the two variants are authored
state alternatives; we draw both, so they z-tie. Needs an engine-side light-state toggle to pick
one variant.

**Approach.** Classify the `lite*`/`ltout*` pairing at build time (`SceneBuilder`/`WorldBuilder`
neighbourhood — read the architecture entries first) and show exactly one variant, keyed off the
mission's light state (day missions: lights-off unless data says otherwise — check how the state
is authored before choosing a default).

**Model recommendation.** medium.

**Verify.** Before/after screenshots at a known z-tying runway; the 8-chapter `--freecam`
regression — node counts will change (variants suppressed), so record the expected delta rather
than asserting unchanged counts. Golden hashes will move if a golden frames a runway — inspect and
re-pin deliberately.

**⚠ Traps.** Do not fix by a depth-bias nudge — the variants are semantic alternatives, not a
draw-order tie to break.

## B14 ☐ `BL-082` — the C1 rail-over-transition z-nit

**Goal.** The one 6-poly rail patch NE of the C1 bridges draws above its ground transition.

**Evidence (confidence: traced to a location, lead on the fix).** Backlog `BL-082`: the patch sits
below the draw-order tie-break's resolution.

**Approach.** Read the draw-order tie-break's entry in `docs/architecture.md` and the depth-bias
findings (`analysis/item9-depth-bias/`) first; then either extend the tie-break's resolution for
this class or accept a targeted bias for the patch's class. Smallest change that doesn't disturb
the global tie-break wins.

**Model recommendation.** medium, low effort — a one-location nit with a documented instrument.

**Verify.** Screenshot at the exact patch (freecam `--pos` at the NE-of-bridges location),
before/after; 8-chapter regression + goldens unchanged elsewhere.

**⚠ Traps.** A global tie-break change to fix a 6-poly nit is the failure mode — blast radius must
stay local.

# Wave C — Audio & missions

## C21 ☐ `BL-268` — remove the blanket ×0.2 mix override

**Goal.** The own-ship path plays authored `sounds.json` volumes through a named, justified
conversion — no magic ×0.2 commented "Temporary fix".

**Evidence (confidence: traced).** Backlog `BL-268`: four call sites in `FlightAudio.cs`
(:363,:382,:178,:308) multiply `def.Volume * 0.2f`. Either the authored volumes assume a different
reference level (find it, name the conversion) or the mix is wrong; both answers remove the magic
number.

**Approach.** Audit which buses the factor touches — `WorldSounds.cs` may or may not share it —
then either derive the reference-level conversion from the data or set a single named mix constant
with the reasoning in its comment. Keep the audible outcome comparable (this is de-magicking, not
a remix): A/B `.scratch/logs/` sound-event volumes before/after.

**Model recommendation.** medium — the audit needs judgement; the edit is small.

**Verify.** `--volume=1.0` flight session listening pass by log first (`--volume=0` still counts
and logs — `docs/cli.md`); confirm world-sounds path unchanged unless deliberately included.
`.\RunTests.ps1`.

**⚠ Traps.** `BL-285`'s owed listen A/B (engine start/stop) sits on these same paths and is
explicitly gated on this item — note in the landing message that the mix base changed. Do not
touch `BL-269`'s 3D falloff curve; different item.

## C22 ☐ `BL-249` — `MissionSetup` applies the interp placement verbs

**Goal.** Flying C1/M05, C3/MP1–2 or C5/MP1 places the scripted entities (`lifesaver*` boats,
`redcross`, `workersvoyagezep`, `cargozep1`, `rearm_node_2`) at their authored positions and
rotations instead of the world corner.

**Evidence (confidence: traced, with one decoded ambiguity).** Backlog `BL-249`, measured
2026-08-04: `c1\m05.gw` ×20 with rotations unambiguously **degrees** (`0 45 0`, `0 172 0`);
`c3\mp1/mp2.gw` ×4 unambiguously **radians** (`-3.144009` ≈ π); `c5\mp1.gw` ×1 translate-only.
Build-side `load.gw` uses are baked into shipped gamez and need nothing.

**Approach.** Implement both verbs in `MissionSetup` with a magnitude-based unit heuristic per
script (values > 2π ⇒ degrees — the shipped data is cleanly separable), last-write-wins ordering
as everywhere in these scripts. Document the heuristic in `docs/formats/interp.md`.

**Model recommendation.** medium.

**Verify.** **The goldens cannot catch a wrong guess — no IA1/default mission uses either verb.**
Verify in the named missions directly: freecam screenshots of the placed entities in C1/M05 (boats
at 45°/172°) and C3/MP1 (`cargozep1` at ≈π). The final degrees-vs-radians confirmation against the
original goes on the item's *Playtest after fix* line, not into more code.

**⚠ Traps.** A single global unit guess mis-poses one chapter's set — the per-script inconsistency
is in the shipped data, not a reader bug. Cross-ref `BL-034`: the `Object3DRotate` unit question
there is this same class — do not resolve it differently in two places.

# Wave D — Stretch

## D31 ☐ `BL-283` — sandbox free-flight exit hang (diagnosis, timeboxed)

**Goal.** Know why the exported build's free-flight `--screenshot` run never exits in Windows
Sandbox — or a bounded "not reproducible outside sandbox / parked with new evidence" verdict. A
fix is welcome but not owed; this item may legitimately land as a better-characterized backlog
entry.

**Evidence (confidence: well-characterized symptom, no mechanism).** Backlog `BL-283`, B13
clean-machine tests 2026-08-05: work completes (`pixmd5` logged), process parks in normal frame
flow (39 threads, message pump responding), 3/3 repro in sandbox, 0 repro on host; mode-specific
(stunt/launchscreen/splitscreen exit cleanly 5/5). Run order ruled out.

**Approach.** Use the Windows Sandbox test-harness rig (see memory: mount race — wait for the
mapped folder; never force-kill the sandbox). Instrument the quit path in the exported build
(log each shutdown stage), rerun in sandbox, see which stage never completes. Timebox: one
session; if no mechanism, write the new evidence back to `BL-283` and close the item ❌-style as
"investigated, parked".

**Model recommendation.** high — environment-specific process-lifecycle diagnosis with no
mechanism in hand.

**Verify.** The instrumented log from a sandbox run showing the last shutdown stage reached; a
host-side run proving the instrumentation itself doesn't change exit behaviour.

**⚠ Traps.** From the backlog entry: not the timeout knob (600 s changed nothing); the
kill-the-predecessor theory is disproven — don't re-derive; a dev-machine repro needs the
*exported* build, the editor path exits fine everywhere.
