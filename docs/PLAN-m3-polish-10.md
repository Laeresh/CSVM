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

All ten entries moved out of `backlog.md` into this plan on 2026-08-06 (scheduled items live in
their plan); the deep evidence and traps are in the per-item sections below. Same day, the user's
review closed two before work started: `BL-047` (B12) and `BL-283` (D31).

## Milestone goal

- Torn-panel flake debris fires only at the tear moment and separates from the plane in world
  space (`BL-288`).
- The fuel-leak animation and the wing-light blinker stop fighting over the same nodes (`BL-287`).
- The crash water splash sprays up and its rings lie flat on the water (`BL-292`).
- The zeppelin hookup-light on/off quads (`lite*`/`ltout*`) stop z-fighting (`BL-081`).
- The own-ship audio mix loses its unexplained blanket ×0.2 (`BL-268`).
- The weapon gauge maps slots to pylon numbers and sweeps the original's way (`BL-294`).
- Flying C1/M05, C3/MP1–2 or C5/MP1 places the interp-scripted entities where authored (`BL-249`).
- ~~`BL-047` damage display~~, ~~`BL-082` rail z-nit~~ and ~~`BL-283` sandbox hang~~ closed
  2026-08-06 by user call/A/B — see B12/B14/D31.

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

1. ☑ `BL-288` — `gimmeflakes` fires only at a panel tear, and the flakes separate in world space
2. ☑ `BL-287` — the fuel leak stops fighting `WingLightBlinker` over `wing_flare2`/`winglight2`; confirm panels 4/6 burn flakes-only as authored
3. ☑ `BL-292` — the crash water splash orients to the struck surface: spray up, rings flat

### Wave B — HUD & world rendering

11. ☐ `BL-294` — weapon-gauge slots map to pylon numbers (1,5,2,6,3,7,4,8 fill) and the sweep direction matches the original
12. ❌ `BL-047` — closed 2026-08-06: the original disables the HUD on crash, as do we
13. ☐ `BL-081` — the zeppelin hookup-light `lite*`/`ltout*` state pairs play their authored state instead of both drawing
14. ❌ `BL-082` — closed 2026-08-06: the original z-fights there too (user A/B at the spot) — faithful as-is

### Wave C — Audio & missions

21. ☐ `BL-268` — remove/replace the blanket ×0.2 over authored sound volumes in `FlightAudio`
22. ☐ `BL-249` — `MissionSetup` applies `Object3DTranslate`/`Object3DRotate`, with a per-script angle-unit decision

### Wave D — Stretch

31. ❌ `BL-283` — closed 2026-08-06: sandbox existed only to test the installer, which passed

## Dependency and parallelism notes

The open items are independent — no item blocks another; waves are theme groups, not dependency
chains, and run in any order. File contention: A1 and A2 both touch the damage-visuals wiring
(`DamageVisuals`/`FlightRigAssembler` neighbourhood) — land them sequentially, not in parallel
worktrees. C21's ×0.2 sits on the paths `BL-285`'s owed listen A/B will judge; note in the
landing message that the mix changed so that playtest is re-based. B12, B14 and D31 are closed
(2026-08-06).

---

# Wave A — Damage & crash effects

## A1 ☑ `BL-288` — `gimmeflakes` fires once at the tear and separates in world space

**LANDED 2026-08-06 (second pass).** The diagnosed unpooled-template theft below was fixed by
pooling the crash rig's templates (slot containers per `effect_pools.json`'s new crash section,
sized to the authored distinct call-anchor counts) plus a new per-(root, anchor) sticky
caller-slot assignment in `AnimRuntime` — the crash rig's calls anchor on plane nodes outside any
slot, so the world pool's ancestry-based slot choice alone could not work here. The TopLevel half
stayed. Verified on `--plane=player_bhawk` with staggered two-part damage: each of five torn
panels claimed its own `planeflakes` copy (log `caller slot N … claimed by 'pdpX'`, no wrap), the
screenshot series shows both wings' debris fields receding at their own sites, nothing at the
nose; new `damage-template-pool` suite; full battery green (515 units / 32 suites / 13 goldens
byte-identical). Record in the landing commit. The section below is kept as the diagnosis record.

**User re-test verdict, same day:** the pooling fix stands, but the FEATURE semantics are
disputed — nose damage spraying wing panels, tears while armor absorbs, repeated identical
bursts. The data reading says those are authored (`random_gun_impact`'s random `pdp1/2/4`, the
vehicle-level `player_fuelleak`'s random `pdp1–3`, `pdpanel7`'s 4-burst call, and our
combined-armor+HP threshold scale) — whether the ORIGINAL behaves that way is now
`BL-297` `[Blocked: CAP-29]` (backlog.md / playtest.md), per the ground rule that an item needing
a capture stops and records it. No further code here.

**Goal (unchanged).** Taking panel damage in flight throws a single flake burst at the moment a
panel tears; the flakes separate from the plane, decelerate in world space and fall behind. A
fuel-leak-only stage throws none.

**What the first pass got right — keep it.** `AnimRuntime.PlaceTemplateOn` (~line 3130) now sets
`root.TopLevel = true` before writing the placed `GlobalTransform`. This is real and necessary:
without it, ANY template `PlaceTemplateAt` relocates onto a still-flying (not frozen-at-crash)
anchor stays parented under the controller and gets dragged/re-yawed every later frame — confirmed
by tracing the parent chain (`WorldEffectsFactory.BuildFlightCrashRuntime`'s `crashRoot` sits under
`controller`, which is the flying `FlightController`) and matches the already-tested `BL-229`/
`trail-world-anchor` family exactly. `.\RunTests.ps1` is clean with it in (513 unit + 31 engine
suites incl. `trail-world-anchor`/`effects-census`/`damage-hd` + 13 goldens byte-identical) — this
part regresses nothing and should stay regardless of what else changes.

**What was WRONG in the first pass: verification never used the plane the bug was reported on.**
Every probe in the first session used the CLI's default/random plane selection (`--fly
--damage=leftwing:0.01`, no `--plane=`) — never `--plane=player_bhawk`. `DamageVisuals.cs`'s own
class doc already flags the Bloodhawk (with the Firebrand and Brigand) as one of the three
airframes with non-standard/crossed panel-node naming. Any session picking this back up **must**
add `--plane=player_bhawk` to every repro command, matching the user's actual report.

**The real, now-evidenced root cause: `gimmeflakes`'s template (`planeflakes`) is a single,
UNPOOLED node shared across every panel and every hit, and each new `CALL_ANIMATION gimmeflakes`
steals it.** Traced 2026-08-06 with `--plane=player_bhawk --damage=leftwing:0.01 --debug-anim
--log=anim:debug` (`.scratch/logs/probe-*.out`, the `GD.Print` stream — NOT the categorized `.log`
file, which only carries `Log.*` calls):
```
damage panel: pdpanel5 on (leftwing 1%)
anim: retarget 'gimmeflakes' onto 'pdp5' (pdp5) [caller pdpanel5]
damage panel: pdpanel4 on (leftwing 1%)
anim: retarget 'gimmeflakes' onto 'pdp4' (pdp4) [caller pdpanel4]
damage panel: pdpanel3 on (leftwing 1%)
anim: retarget 'gimmeflakes' onto 'pdp3' (pdp3) [caller pdpanel3]
```
Each `AT_NODE` **resolves correctly** to its own panel (pdp5/pdp4/pdp3 in turn) — so the earlier
"(a) over-fire" framing (a bad re-CALL, or bad node resolution) is still a correct disproof; that
part of the previous session's conclusion holds. What is NOT idempotent is the template root
itself: `AnimRuntime.ForCrashRig` never sets `PooledTemplates`/`ShowPlacedTemplates` (grep
confirms — only `WorldEffectsFactory.BuildWorldEffectsRuntime:408-409` and the test suites set
them; the crash rig never does). So `TemplateRootsFor("planeflakes", ...)` always returns the
SAME single "planeflakes" node for every panel on every hit. In the `CallAnimation` dispatch
(`AnimRuntime.cs` ~2384-2461), each subsequent call — pdp4's, then pdp3's — finds `IsLive(target,
startAnchor)` false (the live instance is keyed on the PREVIOUS anchor, e.g. pdp5, not this one),
so it unconditionally `PlaceTemplateAt`s (teleports) the shared root onto the NEW site and
`Start()`s it again from the def's rest pose — discarding whatever the previous panel's flakes
were doing mid-flight and restarting the whole burst at the new location. `RemoveInstances` inside
`Start` is keyed on the NEW anchor too, so it does not even clean up the stale instance still
registered under the OLD anchor — for the ~1 s the def's `RUN_TIME` runs, two or three `AnimInstance`s
of the same def can be alive on different (one current, others stale) anchors at once, all driving
the SAME `flake1..flake7` child nodes (`MotionSet.Add`'s per-`(Target,Channel)` eviction is what
keeps only the latest one visible — read `docs/architecture.md`'s `MotionSet.cs` entry before
touching this).

This matches BOTH user-reported symptoms without further hypothesis: **"multiple times"** is the
debris repeatedly teleport-and-restarting as each new panel crosses its threshold (in the extreme
single-hit repro above all three panels fire in one frame; in a normal fight, hits land seconds
apart, so the user would SEE each teleport as a fresh burst); **"wrong site, even the nose"** is
the debris always ending up wherever the MOST RECENT panel-tear resolved to, never staying at the
panel the user is actually watching — and depending on where Bloodhawk's own pdpN nodes physically
sit (unverified — see next), the most recent one could easily read as "the nose" even though the
hit landed on a wing.

**Not yet done — pick up here.**
1. **Confirm the physical site claim.** Dump Bloodhawk's `pdp1..pdp8` node positions (no existing
   `--dump-*` covers this — extend `--dump-markers` or write a one-off probe reading
   `PlaneBuilder.DamagePanels`/`FindByName` positions) and correlate against a staggered-hit repro
   (`--damage=leftwing:0.15,rightwing:0.15` — NOT a single `:0.01`, which crosses every threshold
   in one frame and hides the teleport-over-time shape) plus the `anim: retarget 'gimmeflakes' onto
   'pdpN'` log line active at the moment a nose-looking burst is seen.
2. **Fix shape (pick one, both plausible, neither implemented):**
   (a) Pool the crash rig's damage-stage templates the way `WorldEffectsFactory.BuildWorldEffectsRuntime`
   pools the world ones — `PooledTemplates = true` + a per-root slot count (`data/effect_pools.json`
   is the existing mechanism; would need a `planeflakes` entry sized to the plane's own panel count,
   probably 8) so each panel's tear gets its OWN copy instead of stealing the one shared node. Check
   whether `short_firetrail`/`yellow_sparks_follow`/`small_fireball_follow` — the OTHER templates the
   same unpooled crash rig places (see the same `.out` log) — have the identical bug; if so this is
   one fix for the whole family, not just `gimmeflakes`.
   (b) A narrower fix scoped to `gimmeflakes` alone, if pooling the whole crash rig turns out to be
   too invasive (splitscreen-sized pools, `data/effect_pools.json` schema) — e.g. let a def opt out
   of the shared-template relocate-and-restart and instead spawn/track one instance per anchor. No
   precedent for this in the codebase yet; would be new plumbing, not a "proven family fix" the way
   (a) is — weigh carefully against the ground rules' "no new reverse engineering" bar (this is
   engine plumbing, not decode work, so it should still be in scope, but says so explicitly since
   it is more invasive than the original estimate assumed).
3. **Re-verify on `--plane=player_bhawk` explicitly**, staggered hits, `--debug-anim`, before
   calling this closed again. The `trail-world-anchor`/`effects-census`/`damage-hd` suites all still
   need to stay green, but none of them exercises the unpooled-template-theft path — check whether a
   new suite belongs alongside `trail-world-anchor` for this (multiple `CALL_ANIMATION`s onto the
   same unpooled template root from different anchors within one frame, asserting each gets its own
   copy or its own untouched flight) once the fix shape is chosen.
4. **`docs/architecture.md` needs correcting, not just extending**, once the real fix lands: both
   entries this session touched (`AnimRuntime.cs`'s `PlaceTemplateOn` bullet, `DamageVisuals.cs`'s
   "traced to NOT be a dispatch bug" bullet) currently overstate the fix as complete. Rewrite them
   from what actually landed, not what was believed to have landed.

**Model recommendation.** medium-high now — the TopLevel half was mechanical, but the pooling
question touches `data/effect_pools.json` schema and the multi-instance-per-anchor question the
crash rig has never needed before; worth thinking through the blast radius on the OTHER unpooled
crash-rig templates before picking fix shape (a) vs (b).

**Verify.** `--plane=player_bhawk --fly --damage=<staggered per-part fractions> --debug-anim
--log=anim:debug` at a real mission spawn and heading (the identity pose hides the parenting bug —
`docs/verification.md`); confirm from the `.out` log that `gimmeflakes` retargets onto each newly
torn panel WITHOUT relocating a still-live earlier burst, and confirm visually (screenshot series
across several seconds, not one frame) that each panel's debris field stays put at ITS OWN site.
Then `.\RunTests.ps1` incl. the `trail-world-anchor` suite and goldens.

**⚠ Traps.** Do not mute `gimmeflakes` to fix the repeated-burst symptom — the tear moment must
still throw its burst, for every panel, independently. Verify off-origin; the identity pose masks
plane-local anchoring. **Verify on `--plane=player_bhawk` specifically** — the default/random plane
selection used throughout the first session's probes never exercised the airframe the bug was
reported on, and Bloodhawk is a documented crossed-naming outlier (`DamageVisuals.cs` class doc).
Single extreme `--damage=part:0.01` presets cross every threshold in one frame and hide the
teleport-over-time shape — use staggered fractions or successive hits instead.

## A2 ☑ `BL-287` — fuel leak vs wing lights; panels 4/6 flakes-only check

**LANDED 2026-08-06.** `WingLightBlinker.Suspend(flareName)` hands a named lamp's index to
whatever deactivated it and skips it outright in `Advance` from then on; `FlightRigAssembler`'s
`DamageEffectSink` calls `Suspend("wing_flare2")` right after playing `player_fuelleak` through the
rig runtime (the def's own `ObjectActiveState` target name, traced in
`extracted/C1/cam_anim/player_pfighter-player_fuelleak.json`'s ELSE branch). Deterministic ownership
handoff at the one call site that knows the leak just fired, not a "did someone else touch this
node" scan after the fact — that scan was tried first and rejected: `WingLightBlinker` spends
>97% of its 1.5 s cycle already commanding the flare `Visible=false`, so a leak trigger landing
during the "off" phase (the common case) would write the SAME value the blinker already had and
never be detected as a change, leaving the very next blink window free to override it anyway.
`winglight2` (a real, distinct mesh node — not a `wing_flare*` node `WingLightBlinker` ever
manages) needed no code: nothing else in the codebase or the data touches it after
`player_fuelleak`'s deactivation, so it just stays off, as authored. Panels 4/6's `view_result`
sequence is confirmed never called by anything in the compiled program (searched every
`player_pfighter-pdpanel*.json` for a `CallSequence` and the C# source for the literal
`view_result` — zero hits either way): flakes + skin flip only, no burn, exactly as filed. No code
change for that half.
**Verify.** `--fly --plane=player_bhawk --chapter=C1 --damage=leftwing:0.9 --det --log=anim:debug
--frames=210 --screenshot=…` (`.scratch/logs/bl287-verify.log`): `player_fuelleak` fires once
(`damage stage anim=player_fuelleak started=1`), zero errors/warnings beyond the pre-existing
`pir_spinner.tif` texture-fallback notice, screenshot saved at frame 210 (3.5 s, past two 1.5 s
blink cycles since the leak fired) with no exception. `.\RunTests.ps1`: 515 units / 32 engine
suites / 13 goldens, all green, byte-identical. No 8-chapter `--freecam` regression run: this
change touches only the flight controller's per-frame flare update and the flight rig's damage
sink wiring, neither reachable outside `--fly`, so `--freecam`/world-building exercises none of it.

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

## A3 ☑ `BL-292` — crash water splash orients to the surface

**LANDED 2026-08-06.** Confirmed the hypothesis: `WorldEffectsFactory.BuildFlightCrashRuntime` sets
`crashRoot.Transform = controller.PlaneModel.Transform`, and the crash rig's effect-template pool
slots (where `plane_big_splash`/`plane_big_ripple`/`flydirt_plane` etc. get placed) sit under that
root — needed so the wreck subtree lands at the crash pose, but the same rotation leaked into every
CALL_ANIMATION-relocated template's BASIS, since `AnimRuntime.PlaceTemplateOn` only ever overwrote
the placed root's ORIGIN, never its inherited basis (the fourth bite of the plane-parented-effect
trap, confirmed against `carnage_trails-call_crash_trails.json`/`huge_splash_model-plane_big_splash.json`
et al. — no guessing). Fix: `PlaceTemplateOn` gained a `level` parameter that overwrites the basis to
`Basis.Identity`, driven by a new `AnimRuntime.LevelPlacedTemplateNames` allowlist (keyed by
`AnimName ?? Name`) rather than a runtime-wide flag — `EffectCatalogue.CrashSurfaceLevelAnimNames`
names exactly the water splash's own sub-effects (`plane_big_splash`, `plane_big_ripple`,
`hg_splasher`) and the dirt burst's ground-scorch dust plane (`flydirt_plane`), set once in
`BuildFlightCrashRuntime` beside `PooledTemplates`/`InheritedVelocityExempt`.

**First pass was too broad — caught at the controls.** A runtime-wide bool (level EVERY
CALL_ANIMATION-relocated crash template) fixed the splash but also releveled `call_crash_trails`'
flying debris chunks (`fly_trail1-5`), which author their xz/y launch spread in the template's OWN
frame on purpose so debris continues roughly along the crash's own attitude/momentum (reinforced
separately by `InheritedWorldVelocity`) — leveling it sent debris off on a fixed world heading
unrelated to the impact instead, visible on the `c1-crash` golden's later frames (past the fireball,
`--frames=85` vs the pinned `--frames=20`) as wreckage "flying away" 45° to the side instead of along
the forward vector. Diagnosed by isolating frames past the fireball on both builds side by side
(before-fix debris tracked the impact heading; blanket-fix debris diverged) — the named-allowlist
version above reproduces the before-fix debris trajectory exactly while still leveling the splash.
**Fire and steam are unaffected on purpose**: `large_fireball`/`large_10sec_fire`/
`large_black_smokeball` are pure puffers (no owned mesh, so a host's basis was never their orientation
signal) and `large_steam_spray` is excluded from the allowlist per this item's own goal.

**Verify.** Scripted C1 sea dive (`--pos=-6500,300,-1500 --direction=1,-0.85,0 --hold=0,0,0,1`,
nose-down + banked): before the fix the splash's `splash_polys`/`ripple1-3` inherited the ~40° dive
tilt, rendering as a giant screen-filling wedge (the huge authored ring scale, 7-20×, presented
nearly edge-on toward the camera); after the fix the same shot shows a round, level ring with the
spray column firing straight up. `--crash=5 --hold=0,0,0,0.6` (the `c1-crash` golden's own repro) at
frames past the fireball (`--frames=85`) confirmed debris/fireball/smoke pixel-identical to the
pre-BL-292 baseline once the allowlist was scoped down; at the golden's own pinned frame 20 the
allowlisted `flydirt_plane` still moves a thin 2.16 % halo ring (>20/255 threshold) around the
fireball — `c1-crash` re-pinned, explained in `analysis/goldens/manifest.json`. Full
`.\RunTests.ps1`: 515 units / 32 engine suites / 13 goldens, all green.

**Goal (unchanged).** A dive into open water plays `plane_big_splash` with the spray column firing
straight up and the flat rings lying in the water plane; fire and steam stay correct.

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

## B12 ❌ `BL-047` — crash damage display: closed, nothing to fix

**Closed 2026-08-06 (user call — "answered").** The original disables the HUD on a crash, and so
does our build in the meantime — the latched all-red state the entry described is no longer
player-visible on the crash path. No code change; `BL-047` deleted from `backlog.md`, record in
the closing commit (`git log --grep=BL-047`).

**Original approach (kept for reference).** The entry traced the all-red to the ordinary damage
path left latched (`GaugeCluster.cs` blinks on `OnPartDamage`; `FlightController.Crash()` never
called into `Gauges`), and the planned fix was to wire `Crash()` to whatever the original's crash
footage shows. The HUD-off-on-crash behaviour supersedes that wiring entirely.

## B13 ☐ `BL-081` — zeppelin hookup-light state pairs play their authored state

**Goal.** The `lite*`/`ltout*` lights-on/lights-off state-variant quads no longer z-fight: each
pair shows the state the data authors, and the hookup-lights animation can swap it.

**Evidence (confidence: traced — user lead confirmed in data 2026-08-06).** The `liteNN`/`ltoutNN`
pairs are not runway lights: they are the **pirate zeppelin's hookup lights**, driven by the
`pz_hookup_lights` mission animation (anchored at `pz_lites` on `piratezep`), which references
`lite01`–`lite11`, `ltout01`–`ltout11` and `flash1`–`flash11` — shipped in C1's M02, M04 and M05
(`extracted/C1/M02/mis_anim/pz_lites-pz_hookup_lights.json` and siblings). We draw both variants
of every pair at once, so they z-tie. The old entry's "runway lights / needs an engine-side
light-state toggle" framing is superseded by this.

**Approach.** Honor the anim def's RESET_STATE at build (one variant of each pair hidden — the
same start-hidden pattern `wing_lights_blink` established in `PlaneBuilder`), and let the
`pz_hookup_lights` def drive the swap through the existing anim runtime if it is dispatched in
the flown mission. Check first whether the def's node bindings resolve (the zeppelin subtree owns
the nodes — mind the compiled-symbol-table binding trap in `docs/formats/destructibles.md`).

**Model recommendation.** medium.

**Verify.** Before/after screenshots of the pirate zeppelin (`--node=piratezep --viewer
--chapter=C1` frames it; a C1/M02 flight for the live anim). The 8-chapter `--freecam`
regression — visible-variant counts change by design, so record the expected delta. Golden hashes
move only if a golden frames the zeppelin — inspect and re-pin deliberately.

**⚠ Traps.** Do not fix by a depth-bias nudge — the variants are semantic alternatives, not a
draw-order tie to break. Do not hide by name pattern globally without checking every chapter's
`lite*` users: the C1 texture list also has unrelated `lite_out.tif`/`rr_lite*` consumers.

## B14 ❌ `BL-082` — the C1 rail-over-transition z-nit: closed, the original does it too

**Closed 2026-08-06 (user A/B — "answered", faithful as-is).** The user located the patch at the
controls and confirmed **the original z-fights there too**: rails vs ground, at
`--pos="-6047.982,152.993,-5881.963" --direction="-0.67488,-0.73759,0.02216"` (freecam F11
print) — on the rail line just east of the third railroad bridge `rrbrdg3` (center
(−6311, 131, −5859)). Matching our render is the goal, so this is authored-data behaviour and
"fixing" it would be a deliberate deviation — not taken. (Probe shots referenced below are
transient `.scratch/` artifacts — the F11 params are the durable pointer.) No code change;
`BL-082` deleted from `backlog.md`, record in the closing commit (`git log --grep=BL-082`).

**Evidence note for any future revisit.** A data sweep first mis-located the patch: the only
`track_base.tif` decal polys in C1's gamez sit on the **Los Angeles station** corridor — node
`track01` at (−5226, 128, −3837)/(−5170, 128, −3836) (the latter layered over `abld_shadow`) and
`a_detail2` at (−4939, 128, −3836) — and those render **clean** (probe shots
`.scratch/bl082_track01.png` / `bl082_adetail2.png`), plausibly healed by the overlay-pass
machinery that post-dates the 2026-07-16 observation. The user's actual spot draws its rails from
terrain-tile texturing, not `track_base` decals — so a texture-based search cannot find it; the
F11 params above are the only reliable pointer. If anyone ever wants to *improve on* the original
here: smallest local change only (a targeted class bias per `analysis/item9-depth-bias/`), never
a global tie-break change.

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

## D31 ❌ `BL-283` — sandbox free-flight exit hang: closed, won't do

**Closed 2026-08-06 (user call — "won't do").** Windows Sandbox mattered only as the
clean-machine instrument for the friends-release installer test, and that test passed
(PLAN-friends-release B13, 2026-08-05). The hang never reproduced outside the sandbox, and the
affected path is the scripted auto-quit, not the UI quit a player uses — so the defect has no
player-facing surface worth a diagnosis session. `BL-283` deleted from `backlog.md`; the full
symptom characterization (3/3 sandbox repro, 0 host repro, mode-specific, run-order ruled out)
lives in the closing commit (`git log --grep=BL-283`) and the frozen `docs/HISTORY.md`
2026-08-05 B13 entry, should a real-Windows report ever revive it.
