# M3 polish 7 — HUD sweep/warning fidelity, flight ceiling, effects and gates

**COMPLETE** (written 2026-08-04, completed 2026-08-05). Archived under `docs/plans/`; all ten items
landed — three of them in part as disproofs rather than code. `C6`/`BL-221` settled the `AT_NODE`
axis order as reading **A verbatim** by a 6,728-position census, so the proposed flip is disproven
and **no code changed**; `A1`'s measured ~97 ms end-cap ease is deliberately left unimplemented and
recorded rather than guessed; and `B4`'s clamp matches `CAP-03`'s 2003 m resting cap to 0.3 ft while
the same capture's speed-bleed half is an **open, disclosed gap** — the existing stall model does not
produce it for free, as the Approach had hoped.

**Owed at the controls:** `PT-31`–`PT-37` in [`playtest.md`](../../playtest.md) — the gauge-arrow
A/B against `CAP-18`, the stall-blink A/B against `CAP-06`, the gun-belt colour judgement (no
original clip exists to A/B against), the sea-dive splash and its splash-then-steam ordering (no
headless water crash exists — `--crash` resolves `Ground`), the rocket rings' template meshes, and
splash falloff onto a real large neighbour. `B5`'s engine-detune listen A/B is owed too and carries
no `PT-` of its own. **Found on the way and filed rather than fixed:** `BL-257` (the zeppelin model's
eight parts hidden on the tick they launch) and `BL-258` (the `unknown_seq` block — a third `Initial`
sequence on 1,544 defs our reader never loads, holding 879 of the install's flagged calls).

Ten items drawn from `backlog.md` under the criteria agreed 2026-08-04: player-visible M3-era
fidelity (weapons/destruction/HUD plus cheap flight/audio wins), **unblocked** — no owed `CAP-nn`
capture, no M4/menu-hub/cutscene dependency — mixing quick wins with a couple of larger items.
Each item was re-verified still open against **both** `docs/HISTORY.md` and the code before
scheduling (a backlog entry is not proof the work is undone): `BL-024`/`BL-025`, which gate A1 and
A3, landed 2026-07-30 (quickwins A1/A3), so both are clear. Deliberately excluded: everything
already landed and owing only an in-cockpit playtest (`BL-001`–`BL-005`, `BL-253`, `BL-254`);
every future-milestone item (M4 AI, menu hub, cutscenes, cockpit view); and `BL-047`, which was
shortlisted and then dropped — `docs/HISTORY.md` records it capture-blocked per the 2026-07-30
ruling ("check what the original shows on a crash before wiring anything"). `BL-088` was
shortlisted next and then found **already landed** (2026-08-01, "M3 Wave C C9" in
`docs/HISTORY.md`; its backlog entry was stale and has been deleted) — caught by the user
2026-08-04. `BL-239` took the slot: diagnosed in code, unblocked, no capture needed.

## Milestone goal

- The weapon gauges read like the original's: the ammo/hardpoint arrow sweeps to the new slot at
  the measured rate instead of snapping, and the gun low-ammo colour threshold is judged on a real
  gun belt rather than inherited from the rocket-pylon coincidence.
- The stall warning behaves like the original's: a blink-*rate* ramp with the warn threshold
  (0.30 fd) split from the nose-drop threshold (0.25 fd).
- The measured hard altitude clamp (2003 m, C1B) exists, and the engine loop is the original's
  detuned pair.
- Three effect-fidelity gaps close: the `AT_NODE` axis-order question is settled by census, the
  sea-dive splash emitter survives its same-tick deactivation, and the world-effects template
  *meshes* (gunhit bits, `he_ring`, splash/zeppelin models) render at the call site.
- `WAIT_FOR_COMPLETION` is implemented with measured scope, and rocket splash damage scores a
  neighbour by the nearest point on its collision shape instead of its transform origin.

**Nothing blocked on a capture of the original, an M4 capability, or the menu hub enters this
plan.** That is the criteria line the item selection was made against; if an item turns out
mid-work to need one of those, it goes back to `backlog.md` with the discovery recorded, it does
not wait here.

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

### Wave A — Gauges and warnings (`GaugeCluster.cs` cluster — sequential, same file)

1. ☑ `BL-184` Tween the ammo/hardpoint gauge arrow at 168.7 °/sim-s with shortest-way wrap
2. ☑ `BL-148` Stall warning: blink-rate ramp + split the 0.30 warn / 0.25 nose-drop thresholds
3. ☑ `BL-142` Re-tune `IndicatorLowFrac` for guns on its own merits

### Wave B — Flight and audio quick wins

4. ☑ `BL-094` Hard altitude clamp at the measured 2003 m (absorbs the `BL-073` stub)
5. ☑ `BL-078` Engine loop as the original's ~5%-detuned pair

### Wave C — Effects and destruction

6. ☑ `BL-221` Settle the `AT_NODE` position axis order by census (graze sparks 8 m too high)
7. ☑ `BL-229` Splash emitter killed on its start tick by its own caller
8. ☑ `BL-061` World-effects template MESH half renders at the call site

### Wave D — Larger mechanisms

9. ☑ `BL-228` Implement `WAIT_FOR_COMPLETION` (scoped and measured — it moves goldens)
10. ☑ `BL-239` Blast falloff measures to a body's transform origin, not its geometry

## Dependency and parallelism notes

A1–A3 all edit `GaugeCluster.cs` — run them sequentially, never in parallel worktrees. A2 also
changes `FlightModel.isStalled()`'s signature and its `FlightController.cs` call sites, so A2 and
B4 (which touches the same flight modules) must not run concurrently either. A3 is an in-flight
eyeball tune — do it after A1 so the gauge draw path has stopped moving. B5, C6, C7, C8 and D10
are mutually independent and independent of Wave A. C6 should land **before** C8 if its verdict
flips the axis read — the template meshes' `OBJECT_MOTION` placements go through the same
convention. C7 and D9 share the sea-dive repro but are **different mechanisms** (`BL-229`'s events
carry no `WAIT_FOR_COMPLETION` flag) — do C7 first and re-check whether the symptom survives
before starting D9's scoping. D9 is the highest-blast-radius item in the plan; land it last, on a
clean tree, with a golden baseline taken first.

---

# Wave A — Gauges and warnings

## A1 ☑ `BL-184` Tween the ammo/hardpoint gauge arrow

**Goal.** The gun and hardpoint gauge pointers visibly sweep to the newly selected slot at the
original's constant rate, routed the shortest way round; the numeric readout still snaps.

**Evidence (confidence: traced — measured from `CAP-18`, decoded 2026-08-04).** Our gauge draws
the pointer rotated to the selected slot instantly — `DrawWeaponGauge` recomputes
`-(360°/Positions) * Selected` per frame with no tween (`GaugeCluster.cs:613-614`). `CAP-18`
(`analysis/video-flight-calibration/ammoarrow.py`) measured: one shared constant rate for both
gauges, **168.7 ± 1.6 °/sim-s** (234.5 °/wall-s), **linear** interior (residual 1.1° over 90–180°
traverses) with ~2 frames (~97 ms sim) of ease at each end — not a smoothstep. Routing is the
standard shortest-path wrap `delta = ((target − current + 180) mod 360) − 180`, which also
reproduces the observed counterclockwise choice at the exact 180° antipode. The readout
(`40 SLUG`/`2400` → `30 SLUG`) flips on a single frame at the *start* of the sweep. Gun gauge
4 slots at 90°, hardpoint ring fixed 8 at 45° — the ring size and static slot mapping are already
right in our code (`geom.Positions = geom.Indicators.Count`, `GaugeCluster.cs:571`); **the whole
delta is the animation**. End-to-end A/B durations: 90° = 633 ms sim, 135° = 881 ms, 180° = 1190 ms.

**Approach.** Add a per-gauge animated angle state advanced at the constant sim rate toward the
target slot angle via the wrap expression above; leave the readout path untouched. Sim clock, not
wall clock.

**Model recommendation.** Medium — mechanical implementation of a fully measured spec, one file.

**Verify.** `--hold` script cycling weapons; capture consecutive frames (`--shots=N`) and check
intermediate angles exist and the 90° step spans ~633 ms sim. A/B against `CAP-18`. Full
`.\RunTests.ps1`.

**⚠ Traps.** Rate is quoted in **sim** seconds (k = 1.390) — implementing 234 °/s runs the sweep
39% fast. Do not ease as smoothstep. Do not derive the hardpoint ring size from the loadout — 8 is
fixed (user-confirmed 2026-08-04; `docs/formats/markers.md`). Acceptance is a capture A/B, which
is why this item was deliberately kept out of the code-verifiable-only quickwins plan.

**Landed 2026-08-04.** Constant-rate tween implemented per Approach (no ease — the measured ~97 ms
end-cap ease has no positive shape beyond "not a smoothstep", so it stays unimplemented and
recorded, not guessed). `gauge-arrow-tween` suite added; full `.\RunTests.ps1` green, goldens
unmoved. Capture A/B against `CAP-18` still owed at the controls — `PT-31`. Details:
`docs/HISTORY.md` 2026-08-04, `docs/formats/hud.md`.

## A2 ☑ `BL-148` Stall warning blink-rate ramp + threshold split

**Goal.** The `STALL` plate blinks at a speed-dependent rate (slow at onset, fast deep in the
stall), lighting at 0.30 fd while the nose-drop keeps its own 0.25 fd threshold.

**Evidence (confidence: traced — measured 2026-08-04 from `CAP-06` + the two `CAP-05` clips, four
clips, gating rigid).** Ours: `GaugeCluster.cs:247` gates on `Stalled && WarnPhaseOn`; `Stalled`
is a single hard boolean from `FlightModel.isStalled()` (`FlightModel.cs:328-332`), `WarnPhaseOn`
a fixed 400 ms 50%-duty blink (`GaugeCluster.cs:51,130`). Original: brightness is **binary**
(lit R 211.0 ± 0.2 / unlit 41.7 ± 0.2, identical in every speed bin — no opacity ramp), duty 0.50
throughout; the **rate** ramps monotonically with stall depth from **1285 ms sim** full period at
the threshold to **592 ms** at 40–50 mph, toggling on an integer game-frame counter (half-period
≈ 5.9 × V(mph) − 62 ms wall). It tracks speed, not time-since-onset, with **no hysteresis**. The
lamp lights at exactly **0.30 fd** (0.2989–0.2996 across four clips) — our `StallSpeedFrac` is
right *for the warning* — while the nose-drop is at **0.25 fd**, verified inside one clip (lamp
leads the break by 2.64 sim s / 14.9 mph).

**Approach.** Split `StallSpeedFrac` into a warn threshold (0.30) and a stall/nose-drop threshold
(0.25); replace `isStalled()`'s boolean with (or supplement it by) a continuous stall-proximity
fraction, updating its call sites (`FlightController.cs:734,757`); replace `WarnBlinkPeriod` with
the speed-dependent period law in sim ms.

**Model recommendation.** High — a signature change rippling across flight modules plus a
measured rate law; wrong-clock and wrong-abstraction traps are live.

**Verify.** Scripted decelerating flight; log blink dwells and check the period ramps ~1285 → ~590
ms sim and onset sits at 0.30 fd both decelerating and accelerating. A/B against `CAP-06` after
landing. Full `.\RunTests.ps1`.

**⚠ Traps.** (a) A code change before a magnitude — not a retune of `WarnBlinkPeriod` alone.
(b) Don't bolt a second parallel margin calculation beside `isStalled()` — change the signature.
(c) Don't generalise into a shared ramping-warning abstraction — the low-alt cue (`LowAltAglM`)
is legitimately binary. (d) The measured periods are **sim ms**; wall figures are 1/1.390 of them
— implementing wall numbers makes our blink 39% quick. (e) The lit plate carries an orange bezel
glow — if reproduced by texture swap, that glow is part of the lit art.

**Landed 2026-08-04.** All three halves in: the threshold split (`StallSpeedFrac` 0.25 nose-drop /
`StallWarnFrac` 0.30 lamp, both over the one `FlightModel.StallFraction` margin), the proportional
blink-rate law in sim ms, and the phase integrator that carries it (`GaugeCluster.AdvanceStallLamp`).
The graded-opacity shape the item was opened on is **disproved and unbuilt**; the original's integer
game-frame lattice is recorded as its frame rate, not part of the law. `stall-warning` suite added;
two new `flight` debug log lines make the cue measurable from a scripted run — a zero-throttle
deceleration reads onset 0.299 fd / 90.3 mph, exit 0.302 / 91.1 with no hysteresis. Full
`.\RunTests.ps1` green, goldens unmoved. Capture A/B against `CAP-06` still owed at the controls —
`PT-32`. Details: `docs/HISTORY.md` 2026-08-04, `docs/formats/hud.md`.

## A3 ☑ `BL-142` Re-tune `IndicatorLowFrac` for guns

**Goal.** The gun group's low-ammo colour step reads well against a real gun belt draining from
full to empty — judged on its own merits, not inherited from the 3-round rocket-pylon coincidence.

**Evidence (confidence: direction-sound, magnitude TUNE).** The 0.34 threshold
(`GaugeCluster.cs:76`) was picked so a 3-round pylon steps green→yellow→red — exactly the case
that must now show no yellow after `BL-024` (landed 2026-07-30) split the code path. Its only
remaining justification, "a gun group only warns near empty" (`GaugeCluster.cs:75`), was never
verified against a gun belt's hundreds-of-rounds curve.

**Approach.** Fly a gun group from full to empty (`--fly --fire` or manual), watch the colour
step, adjust `IndicatorLowFrac` by eye. Record the chosen value as a TUNE judgement in the landing
entry.

**Model recommendation.** Medium, low effort — a one-constant eyeball tune plus its documentation
trail; the judgement is the user's at the controls.

**Verify.** The in-flight observation itself, plus `.\RunTests.ps1` (the constant feeds the
gauge draw path — confirm no golden moves, or that a move is the intended colour step).

**⚠ Traps.** Don't skip because `BL-024` "already tunes it" — `BL-024` only split the code path;
nobody has watched a gun belt drain past 0.34 with intent to judge the colour step.

**Landed 2026-08-04.** No original capture exists for this cue, so the eyeball tune was done via a
`RunProbe.ps1 --ammo=200 --gun-select=0 --fire --frames=<N> --screenshot=<path>` sweep (a 1/14-scale
stand-in for the real 2800-round `CLUSTER_SIZE`) across seven remaining fractions, full to 5%. At
the old 0.34 the light already read yellow with two-thirds of the belt still left (~119 sim s of
sustained fire at real scale); retuned to **0.15** (~53 sim s left), which reads as genuinely low
without doubling as an empty-warning. `GunIndicatorColor`'s literal-zero-only red tier is unchanged
— out of this item's scope. Full `.\RunTests.ps1` green, goldens unmoved (no `--det` capture flies a
gun group into either threshold's band). An eyes-on judgement at the controls is still owed —
`PT-33` — since there is no original clip to A/B against. Details: `docs/HISTORY.md` 2026-08-04,
`docs/formats/hud.md`.

# Wave B — Flight and audio quick wins

## B4 ☑ `BL-094` Hard altitude clamp at 2003 m

**Goal.** The aircraft cannot climb past the original's resting cap (~2003 m true altitude, with
~140 ft of ballistic overshoot allowed), while aerodynamics below the cap are untouched. Absorbs
the `BL-073` duplicate stub.

**Evidence (confidence: traced — settled by `CAP-03`, four clips decoded 2026-08-03, all gating
rigid).** There is **no performance fade below the clamp**: level full-throttle equilibrium is
flat to ±0.3 mph from 1674 to 1988 m and equal to the low-altitude value. At the cap the aircraft
holds altitude to sub-foot at a 22° nose-up attitude while airspeed bleeds at 13.0 mph/sim-s to a
173.7 mph equilibrium — a **clamp on altitude**, not an energy limit. Resting cap
**6571.6 ft = 2003 m**; transient ballistic overshoot to ~6712 ft. The data's `flight_ceiling`
2500 is parsed (`PlaneStats.FlightCeiling`) and read by nothing — the cap is 80% of it.

**Approach.** Clamp altitude (or climb rate with enough lag to allow the measured ~140 ft
overshoot) in `FlightModel`/`FlightController`, leaving thrust/lift/drag untouched below it. The
existing stall model should then produce the observed bleed-to-173-mph symptom for free.

**Model recommendation.** Medium — a well-specified localized mechanism; the trap is the shape,
which the evidence already settles.

**Verify.** Scripted full-throttle climb: altitude settles at ~2003 m (allowing the overshoot),
level speed at 1988 m unchanged from baseline (take the baseline first). Full `.\RunTests.ps1`.

**⚠ Traps.** (a) **Not a thrust or lift fade** — the level runs rule a fade out to within 0.3 mph
up to 15 m under the cap. (b) The "auto stall" is a consequence, not the mechanism — don't build
it. (c) 6571.6 ft is measured in **one mission** (C1B IA1); whether the cap is global, per
chapter/zone, or per aircraft is untested — make the constant configurable/visible rather than a
buried world literal, and don't claim generality in docs. (d) The altimeter scale is proven
(λ = 1.000 ± 0.004) — do not re-open it.

**Landed 2026-08-04.** Implemented as a clamp on altitude, not thrust/lift/drag: once
`FlightModel.Position.Y` is at or above `AltitudeCapM` (2003 m, Config-overridable
`flightModel.altitudeCapM`), the climbing share of that frame's velocity is deleted outright
(`VelocityDir.Y` zeroed and `Speed` recomputed from the remaining horizontal vector) rather than
redirected into more horizontal speed — a no-op below the cap by construction, so thrust/lift/drag
and every earlier branch of `Step` are untouched. A `flightModel.altitudeCapOvershootM` (42.8 m ≈
140 ft) backstop bounds `Position.Y` the same way `MaxDiveSpeedFrac` bounds a runaway dive — it
catches a stray frame, it does not model the original's ballistic-overshoot *shape*, which the
Approach explicitly left optional. No "auto stall" code was added, per trap (b).

Measured against the new `altitude-cap` scenario (22° nose-up hold from level cruise, full
throttle): altitude settles at **6571.89 ft**, 0.3 ft from CAP-03's 6571.60 ft resting cap —
`FlightScenarios` 6 → 8, both new rows asserted and green. The Approach's hope that "the existing
stall model produces the bleed-to-173-mph symptom for free" does **not** hold in this scenario:
speed settles at 301.8 mph, barely below the unclamped baseline, because each frame's `align`
re-tilt toward the held nose is small enough that the clamp's per-frame vertical-KE loss is
second-order and never compounds into a real bleed — the plane never gets close to
`StallSpeedFrac`, so the stall block never engages. Recorded here rather than hidden: the altitude
half of `CAP-03` is traced and now matches to noise; the speed-bleed half is an open, disclosed gap,
not required by this item's Verify step (altitude settling + an unchanged 1988 m level speed,
`level-speed-near-cap`: 301.99 mph, unmoved from the 301.99 mph `level-top-speed` baseline). Full
`.\RunTests.ps1` green (8/8 flight-envelope rows ok, 24/24 engine suites, 436/436 unit tests),
goldens unmoved (no golden shot flies near the cap). Details: `docs/HISTORY.md` 2026-08-04,
`docs/architecture.md`'s `FlightModel.cs` entry.

## B5 ☑ `BL-078` Engine dual-stack chorus

**Goal.** The engine loop plays as the original's ~5%-detuned pair rather than a single loop.

**Evidence (confidence: direction-sound — measured in the dive-video analysis, `docs/HISTORY.md`
2026-07-19; exact detune ratio and mix are the implementation's to confirm against that entry).**
The detuned-stack half of the original claim stands; the "with Doppler" half is withdrawn
(`CAP-09` measured no Doppler at all — do not build against it).

**Approach.** In `FlightAudio`, play the engine loop twice with a ~5% pitch offset (re-read the
2026-07-19 HISTORY entry for the measured comb spacing before picking the number), keeping total
gain level with today's single loop.

**Model recommendation.** Medium, low effort — a small audio-path change with a measured target.

**Verify.** `--volume=0` run logs both voices (a muted baseline is blind — use volume, not
`--mute`); listen A/B against the reference video; `.\RunTests.ps1`.

**⚠ Traps.** Do not add Doppler while in there — `BL-160` closed against it. Keep the summed
loudness constant or every engine-audio TUNE judgement shifts.

**Landed 2026-08-04.** A second `AudioStreamPlayer` (`_engine2`) plays the same
`stats.EngineSound` clip, pitch-split ±half of a new `EngineDetuneRatio` (0.05, TUNE —
the 2026-07-19 entry only bounds the comb spacing at "~5%", no exact ratio or leading voice was
resolved; Config-wired `flightAudio.engineDetuneRatio`) around the shared `EnginePitch.Eval`
centre each frame, so the pair's average pitch is unchanged from the single loop's. No Doppler
added, per the trap. Loudness: each voice held at a fixed `EngineVoiceGain` = 1/√2 (equal-power
split, not a TUNE), keeping the pair's summed RMS power at the single loop's level — the same
convention `MixGain` already uses for splitscreen. `_engine2` is driven through every lifecycle
hook the first voice already had (start, pause, crash, engine-stop, respawn re-fire) so the two
never drift out of lockstep on state changes. Verified: `RunProbe.ps1 --fly --chapter=C1
--mission=IA1 --volume=0` logs both voices building (`audio: engine=snd_bloodhawkengine (dual
voice, 5% detune) …`); `--dump-config` shows `engineDetuneRatio: 0.05` in the tuning template.
Full `.\RunTests.ps1` green (436/436 units, 24/24 engine suites, 13/13 goldens hash-identical —
the audio path touches no pixel). A listen A/B against the reference dive video is still owed at
the controls. Details: `docs/HISTORY.md` 2026-08-04, `docs/architecture.md`'s `FlightAudio.cs`
entry.

# Wave C — Effects and destruction

## C6 ☑ `BL-221` Settle the `AT_NODE` position axis order by census

**Goal.** A settled, evidenced convention for which axis order an anim-def `AT_NODE`/`PufferState`
position uses — fixing the graze sparks that render 8 m above the plane without moving the
rocket/crash effects that look right.

**Evidence (confidence: lead-with-strong-hypothesis — the census is the instrument that settles
it).** Mesh coordinates are settled right-handed Y-up, nose −Z (`docs/formats/gotchas.md`), and
the engine applies offsets in that frame. `touchdown_default`'s five `small_yellow_sparks` calls
sit at `(0, 8, −2)`, `(±1.5, 8, 0)`, `(±4, 8, 0)` — read Y-up that is a rake 8 m **above** the
plane (what the user saw, `PT-24`); read Z-up (x, y = forward, z = up) it is five sources across
the wing 8 m **ahead** — a nose/leading-edge scrape. The ±1.5/±4 spread matching a wingspan is
the strongest clue the first component is spanwise.

**Approach.** A census in `analysis/` (new dir, `FINDINGS.md`): find defs with nonzero offsets
whose correct placement is independently known (turret muzzle points, zeppelin nacelle fires) and
test both readings against them. Only then flip the read — globally or, if the census splits,
along whatever boundary it evidences.

**Model recommendation.** High — a global-convention decision where a wrong flip silently moves
effects that are currently right; the value is in the census design.

**Verify.** The census itself is the evidence; then a graze repro (sparks at the wing/nose, not
overhead) plus before/after screenshots of a rocket impact and a crash to prove the
currently-right cases didn't move. Full `.\RunTests.ps1` with golden baseline taken first.

**⚠ Traps.** (a) Not a touchdown-only fix — every `AT_NODE` position goes through the same read.
(b) `small_yellow_sparks`' `world_velocity (0, 10, 0)` reads plausibly under **both** conventions
— it cannot break the tie; don't cite it. (c) The graze staging site (`graze.siteAtContact`) is a
different question with the same symptom family — it decides where offsets are measured *from*,
not the axis order; settling one does not settle the other.

**Landed 2026-08-05 — settled as reading A (verbatim), so the flip is DISPROVEN and no code
changed.** `analysis/at-node-axis-order/` censuses all **6,728** `AT_NODE`-style positions in the
install across both front-ends and all six event kinds that carry one (trap (a) — this was never a
touchdown-only question): 5,910 are exactly zero, 48 are pure-X and blind, **770 discriminate**, in
539 distinct authored shapes. Three instruments agree. The decisive one is a **paired sign**:
`wv_turrets.zrd`'s `wvutur*`/`wvctur*` are the same definition differing only in the sign of the
middle component (`+2`/`−2`), bound to turrets at local y +42.7…+56.5 on the gasbags versus
y −81.8…−30.0 under a parent named `underneath` — the middle component tracks up/down, so it is the
vertical, and no judgement about "sane placement" enters. **Sibling spread** (the spread of a host's
own offsets against its bbox, invariant to a constant offset) runs 8 : 1 for A, and **known
placements** — a muzzle flash 1 m out of the barrel, a fireball 12 m over a 0.1 m-thick ground
ring, a crane hook below its jib, the pinned `c1-waterfall` golden — all read A. Trap (b) held:
`world_velocity` was never cited.

⚠ **One instrument had to be thrown away and is now `docs/verification.md` INSTR-9.** Scoring each
reading by how far outside the host's bbox it lands, normalised per axis, reported **63 : 101
against the right answer** — effects legitimately sit above flat hosts, and a flat host's vertical
extent is ~0, so the normalisation decides the question. Kept behind `census.py --rejected`.

Per trap (c), this leaves the graze symptom entirely with `graze.siteAtContact`:
`touchdown_default`'s 8 m is authored, the def is staged at the contact point, and in our runtime
its `MAIN_ROOT_NODE` host collapses onto the same relocated `spark_touchdown` anchor — so that flag
is now the only remaining lever, and it is a capture question. `PT-24` (c) is restated accordingly.
Verified by the census itself plus a full `.\RunTests.ps1` with no code delta — 436 units, 24/24
suites, 13/13 goldens hash-identical, `c1-waterfall`/`c1-destroy-effects`/`c1-crash` among them.
Details: `docs/HISTORY.md` 2026-08-05, `analysis/at-node-axis-order/FINDINGS.md`,
`docs/formats/anim-definitions.md`.

## C7 ☑ `BL-229` Splash emitter killed on its start tick

**Goal.** `plane_big_splash`'s `plane_puff_splash1` emitter actually emits during the sea dive
instead of being stopped by its own caller on the tick it starts.

**Evidence (confidence: traced symptom, rule undecided).** The def is three offset-less events:
`ObjectActiveState sp_1 true` → `CallAnimation hg_splasher WithNode sp_1` → `ObjectActiveState
sp_1 false`. Our runtime runs all three in one tick and logs `host 'sp_1' deactivated — emitter
stopped`, yet `hg_splasher` authors a 0.5 s run (`StopSequence` at `Animation+0.5`, `PufferState
active_state 0` at `Event+0.1`) — only reachable if the emitter survives the host's deactivation
or the events don't share a tick. The rest of the sea dive renders; this is one missing emitter.

**Approach.** First census the offset-less activate/call/deactivate triple across the extracted
defs — the rule (emitter survives host deactivation once started, vs. same-tick event ordering)
must come from how the idiom is used install-wide, not from this one def. Then implement the rule
the census supports in `AnimRuntime`/the puffer host-deactivation path.

**Model recommendation.** High — choosing between two plausible engine rules with a
census-designed discriminator; the wrong rule breaks destructible-swap emitter cleanup.

**Verify.** Sea-dive repro (the `player_crash_water` path): the splash puffer emits for its 0.5 s.
Then the destructible-swap regression: kill a destructible whose subtree swap must still stop its
emitters. Full `.\RunTests.ps1`.

**⚠ Traps.** (a) Do not drop the host-deactivation stop wholesale — it is what stops emitters on
destructible subtree swaps. (b) Census before choosing, or the fix is tuned to one def. (c) Not
`BL-228` — these events carry **no** `WAIT_FOR_COMPLETION` flag; a wait would not explain them.

**Landed 2026-08-05 — the census settled it: a host deactivation spares the emitter that started in
its own instant, and still ends every other one.** 414 activate/emit/deactivate pairs install-wide
split 32 same-instant (4 shapes, the splash family, every one authoring a 0.6 s run) against 382 a
median 3.5 s later, smallest later gap 1 ms, nothing in between — so the carve-out is keyed on the
instant, not a threshold, and costs `BL-224` nothing. `EmitterDirector` stamps emitters per
`Advance`; RESET_STATE opts out. Both able-to-fail controls run: unconditional stop fails the splash
half, deleted stop fails the debris half (`part3`'s trail leaks 3.667 s → 5.000 s). `analysis/
bl-229-emitter-host-deactivation/`, `docs/HISTORY.md` 2026-08-05. **Owed: a sea-dive capture** — no
headless water crash exists (`--crash` resolves `Ground`), so the picture is unverified. Re-check
`D9` against this: the splash's `WAIT_FOR_COMPLETION` ordering question is untouched and still open.

## C8 ☑ `BL-061` World-effects template MESH half

**Goal.** The template meshes — the `gunhit` debris bits (`bit1`/`bit2`/`chunk` + their
`OBJECT_MOTION`), the `he_ring` ground shockwave, the `huge_splash_model`/`zep_ng_dstry1.flt`
models — render at the call site; today only the puffer half of the world-effects runtime shows.

**Evidence (confidence: traced — M3 D32, 2026-07-24; item 3 of the entry was disproven polish-5
B5, item 1 landed, this is the sole survivor).** The effects stage is hidden, so puffers (drawn
independently) render while the template's meshes do not. Rendering them needs
visible-at-the-site without flashing at the stage origin — per-def visibility, or a copied
instance per call rather than a hidden shared template.

**Approach.** Reuse the machinery `BL-253` just landed: `WorldSession.ResolveLibraryRoot`'s
per-caller pooled copies with anchor-scoped indexing (`AnimRuntime.IndexPooledCopy`) is exactly
"a copied instance per call". Check whether the world-effects runtime's stage templates can ride
the same path before inventing a parallel one; pool sizes go through `effect_pools.json`
(`BL-231`'s TUNE mechanism).

**Model recommendation.** High — cross-module reuse decision in the two most trap-dense modules
(`AnimRuntime`, world-effects runtime); read both `docs/architecture.md` entries first.

**Verify.** Seeded `--effects-test` census (unseeded runs manufacture a different set each time —
`RANDOM_WEIGHT` gates several gun variants); a gunhit capture showing bits at the impact point;
the `c5` golden watched specifically (def-scoping puffer keys there moved it once). Full
`.\RunTests.ps1` with baseline.

**⚠ Traps.** Effects share puffer names across defs (`trailpuffer2`); the WORLD runtime
deliberately keeps the collapsed `(name, host)` key — do not def-scope it (stacks C5's six
`m_crane_go` spark defs, moves the c5 golden). Concurrency beyond `EffectPoolSlots` (4) collapses
by design — the size is `BL-231`, not this item.

**Landed 2026-08-05.** The Approach's premise was already spent: D31 made the stage visible with
each ROOT hidden, `BL-225` gave every root its pooled per-call copies, and the `gunhit` bits render
(`CAP-25` identified the `chunk` quad in a real capture; `bit1`–`bit3` carry 0 vertices). What was
left is one mechanism, and it is not the stage — **only `PlayEffectAt` revealed anything**, so a
template reached by `CALL_ANIMATION` was moved to the site and left dark while its puffers, which
are world-level particles, emitted there and made the effect read as working. That is exactly the
D31 ring set: `he_ground_effect`'s upper ring (`he_ring1`), `sonic_ground_effect`'s four rising
rings (`sonic_ring1`–`4`), the torpedo's `huge_splash_model`/`ripple` — staged, placed, animated and
invisible. The reveal now fires on a relocating call too. The census found the other end as well:
an ended effect left its mesh **lit** — the ap/dum/mag `dum_gunhit` chunk stayed at the impact point
for the session, because those hits author an `ACTIVE_STATE 0`, finish inside their own TTL, and
`Stop` reaches a template only through a live instance (the slug hit, with no authored stop, ran to
its TTL and looked fine). Hiding is deferred on two measured holds — anything still animating the
ROOT (a template's pieces are the CALLEE's motions on the CALLER's copy) and any sibling def live on
the same copy (`rear_flash_effect` finishes on the tick it starts). Neither trap fired: the puffer
keys were not touched and the pool is unchanged. New in-engine suite `effect-template-mesh` asserts
both halves on real gamez geometry, and both able-to-fail controls were run (reveal removed → the
called half fails; hide removed → `1 still lit`). Verified: seeded `--effects-test` on C1 and C5
(identical — the templates are chapter-invariant), full `.\RunTests.ps1` PASS with 13/13 goldens
hash-identical, `c5-city-night` and `c1-destroy-effects` among them. **Not verified: the picture** —
no headless shot fires a rocket, so `PT-35` owes the capture. Found on the way and filed, not fixed:
`BL-257`, the zeppelin model's eight parts hidden on the tick they launch by a third `OBJECT_MOTION`
shape carrying neither `RUN_TIME` nor `BOUNCE_SEQUENCE`. Details:
`analysis/bl-061-template-mesh/FINDINGS.md`, `docs/verification.md` INSTR-11.

# Wave D — Larger mechanisms

## D9 ☑ `BL-228` Implement `WAIT_FOR_COMPLETION`

**Goal.** A `CALL_ANIMATION` flagged `WAIT_FOR_COMPLETION` holds its caller's sequence until the
callee's instance completes — landing the sea dive's authored splash-then-steam ordering and
whatever else the measured scope shows.

**Evidence (confidence: decoded, scope unmeasured).** The field is decoded
(`docs/formats/anim-definitions.md`: flag `0x10` + an index into the caller's own `anim_refs`,
always naming the call's own name; **3,731 flagged events install-wide**, all
`OnCall`/`WeaponHit`) and the runtime ignores it — every `CallAnimation` returns immediately. The
clean reachable case: `player_crash_water`'s `destroy_crash` flags its `plane_big_splash` call and
then calls `large_steam_spray`; a capture shows both retargeting on the same tick where the
splash's own choreography runs 3.0 s.

**Approach.** First measure: which of the 3,731 flagged events actually dispatch in our default
missions/goldens, and what timing shift the wait would introduce (a scoped log counter before any
behaviour change). Define "completes" for a def with no terminating event **from the data** — a
census of flagged callees' terminating shapes — then implement the hold in the sequence scheduler.
Take a full golden baseline first; expect movement and justify each moved golden individually.

**Model recommendation.** High — the largest blast radius in the plan; the scheduler is shared by
every animation in the game and the item's traps are all about scope discipline.

**Verify.** Sea-dive capture: splash choreography completes before the steam spray starts. Golden
diff review — every moved golden explained against the authored data, none waved through. Full
`.\RunTests.ps1`.

**⚠ Traps.** (a) Turning the wait on globally changes timing far beyond the crash — scope and
measure before believing a screenshot. (b) `0` and `null` are **different authored states**
(3,639 vs 53,019): `0` waits on ref zero, `null` has no flag. (c) `wait_for_raw` exposes unflagged
stale values (525 events) — not a wait, must not be read as one. (d) "Completes" is defined from
the data, not from what makes the crash look right.

**Landed 2026-08-05 — and the census, not a judgement call, is what scoped it.** The new instrument
(`analysis/wait-for-completion/callee_shapes.py`) asks what each flagged call is asked to wait FOR,
per trap (d): over the compiled blocks the runtime executes, 2,852 flagged calls split
2,662 / 25 / **0** / 165 across (last-event × callee-never-terminates), and the reader front-end
reproduces the same one-sided shape independently over its own 147. **Zero, twice**: not one flagged
call with an event behind it names a callee that never finishes, and there are only four such
callees (the `sputter_*`/`gen_drop_ladder` `LOOP{-1}` idiom). So the rule is read off rather than
chosen — **the wait gates the caller's NEXT event and nothing else**; a lifetime reading would wedge
2,770 authored sequences open forever. That is also trap (a)'s answer: only **165 calls (5.8 %)**, in
29 pairs, can shift any timing, median hold 3.0 s. Traps (b) and (c) held — presence, not value, is
the authored state (`AnimData.Has`), and `wait_for_raw` is read nowhere.

`ISequenceHost` gains `PendingWait` (a predicate, not a duration — the callee's length is not
knowable at the call); `WaitCeilingS` 120 s is a logged backstop, not a model, because "completes" is
our instance lifetime rather than the authored one. All four outcomes name themselves where they
happen rather than through `Count`, whose report is the bootstrap census and could never carry a
death-time event (LOG-16).

New `wait-for-completion` suite on real gamez data: `plane_big_splash` t=0.000 s,
`large_steam_spray` t=**3.050 s** against the authored 3.0 s, with the unflagged
`call_crash_trails`/`large_10sec_fire` beside it still at t=0 as the control. **Both able-to-fail
controls run** — hold deleted → gap 0.000 s (the exact pre-change symptom) plus 3 unit-test
failures; flag test dropped → the sequence wedges on its first event. Six new `SequenceRunnerTests`
cover the interpreter half off-engine.

Full `.\RunTests.ps1` PASS (442/442 units, 29/29 suites) and **13/13 goldens hash-identical**. The
Verify step budgeted for moved goldens; none moved, and that is checkable rather than lucky —
`player_crash_water` is the only crash def in the install carrying the flag (every chapter's
`player_crash_default`/`_dirt` is `null` on all 14 calls) and `--crash` resolves `Ground`, so
`c1-crash` cannot reach it. 8-chapter `--freecam` regression: every bootstrap count identical, zero
holds armed, zero errors. **Owed: the sea-dive picture** — no headless water crash exists, the same
gap C7 left — `PT-37`. Found on the way and filed, not fixed: **`BL-258`**, the `unknown_seq` block
(a third `Initial` sequence on 1,544 defs that our reader never loads, holding 879 of the install's
3,731 flagged calls). Details: `docs/HISTORY.md` 2026-08-05,
`analysis/wait-for-completion/FINDINGS.md`, `docs/formats/anim-definitions.md`.

## D10 ☑ `BL-239` Blast falloff measures to a body's transform origin, not its geometry

**Goal.** Splash damage onto a neighbouring body is scored by the blast's distance to that body's
*collision shape*, so a large body (a zeppelin gasbag, a long building mesh) no longer soaks less
splash than a small one — or none, when its origin falls outside the sweep sphere entirely.

**Evidence (confidence: traced mechanism, unmeasured effect — step one is a repro).** Found
2026-08-02 reading `ApplyDamage` for `BL-233`. `ProjectilePool.ApplyDamage` sweeps a sphere of
`IMPACT_PROXIMITY` around the detonation point, then scores each caught body by
`BlastDamage(full, radius, zonePoint.DistanceTo(point))` where `zonePoint` is
`DamageZonePosition(body, shapeIndex)` — the shape owner's transform origin, a single point.
Right for compact zones; for a large body the origin can be tens of metres from where the rocket
detonated on its skin. Two things keep it invisible today, both load-bearing to check before
believing a fix: (a) the **directly struck** body is exempt (`body == struck` → `continue`, takes
full `HEALTH_DAMAGE`) — correct, so a plain aimed hit is unaffected; only splash onto neighbours
is wrong. (b) `MaxBlastBodies` 4096 with radii 15–100 m — nothing is capacity-dropped. Expected
to bite on zeppelin gasbags and large chapter meshes, exactly where `DAMAGES_ZEPPELIN`
(`wep_14`/`wep_28`) points. **Not yet measured against a case.**

**Approach.** Step one: build the repro — a rocket detonating near one end of a long body, with
before/after damage numbers, to demonstrate the effect exists in practice. Then score against the
nearest point on the body's collision shape (Godot's `GetRestInfo`/`CollideShape` on the blast
sphere returns contact points), or per damage **zone** where a rig has them.

**Model recommendation.** Medium — a diagnosed one-function fix, but the repro-first discipline
and the authored-data trap need judgement.

**Verify.** The repro pair (origin-scored vs shape-scored damage on the same detonation), a
zeppelin splash check, and the full `.\RunTests.ps1` with a `--damage-hd` sweep baseline — only
splash-onto-neighbour rows may move; direct-hit numbers must be byte-identical.

**⚠ Traps.** Do **not** widen `IMPACT_PROXIMITY` to compensate — it is authored data, and its
falloff *shape* is already the open TUNE `BL-227`; inflating it papers over a
distance-measurement bug and corrupts both. The comment at `DamageZonePosition` explaining why
the *detonation centre* is the ray contact is about the blast's own position and is correct — it
does not license an origin on the receiving side.

**Landed 2026-08-05.** Step one's repro came first, on controlled geometry rather than a lucky
in-mission placement: a thin wall (the directly struck body) next to one end of a 100+ m
`StaticBody3D` neighbour whose transform origin sits well past `IMPACT_PROXIMITY`, so the old code's
`zonePoint.DistanceTo(point)` read a distance beyond the radius and scored **zero** splash onto a
body only 2 m from the blast on its own skin — the exact "or none, when its origin falls outside the
sweep sphere entirely" case the Evidence named. `ApplyDamage`'s neighbour loop (every body the blast
sphere overlaps other than the one directly struck) now calls a new `NearestBlastPoint`: the same
sphere re-queried through `GetRestInfo`, with every OTHER candidate body from the original
`IntersectShape` sweep excluded, so the only shape left to resolve against is the one being scored —
its `"point"` is the contact where the sphere first touches that body's own surface, not its shape
owner's transform. Falls back to the old origin read only if that query somehow finds nothing (the
body was already known to overlap the sphere, so this is a defensive fallback, never the expected
path). The directly-struck body's branch is untouched, per trap: full `HEALTH_DAMAGE`, unscaled by
falloff, exactly as before. `IMPACT_PROXIMITY` itself was not touched, per the other trap.

The new `blast-neighbor-shape` suite is the repro turned into a permanent, able-to-fail assertion:
reverting `NearestBlastPoint` to the old `DamageZonePosition` call made it fail (the neighbour is
missing from the damage report entirely — its origin-scored falloff clamps to zero), confirming the
suite catches the bug before confirming the fix clears it. Full `.\RunTests.ps1` green: 436/436
units, 28/28 engine suites (the new one included), 13/13 goldens hash-identical — no golden fires a
weapon near a large body, so the direct-hit path's byte-identity was never in question, and
`damage-hd`'s 16 swept destructibles (destroy/reset/rekill) were unaffected, since that suite is
about direct `HEALTH_DAMAGE` kills, not neighbour splash scoring. **Owed:** an in-game picture — no
known chapter mission places a rocket-class blast near one end of a real large body (the zeppelin
gasbags are the candidate), so the fix is verified on synthetic geometry only — `PT-36`. Details:
`docs/HISTORY.md` 2026-08-05, `docs/architecture.md`'s `Projectile.cs` entry.
