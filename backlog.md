# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. Which plan is active, if
any, is `PROJECT_CONTEXT.md`'s "Current status" — never restated here. Per-item history/diagnosis
detail is in commit messages (`git log --grep=BL-NNN`; pre-2026-08-06 in `docs/HISTORY.md`'s dated
entries, now frozen) and `docs/architecture.md` (module bullets); how to
verify a change without fooling yourself is `docs/verification.md`. **The live list of hand-tuned
constants awaiting playtest is the set of items tagged `[Tuning]`** (consolidated actionable
index: [`playtest.md`](playtest.md)). When an item gets scheduled into a plan, move it there; when
it lands, delete it here.

**Item IDs.** Every entry carries a flat `BL-NNN` tag, assigned once at minting and never
renumbered or reused, even when the item it names is deleted — so a stale cross-reference elsewhere
fails loudly instead of silently pointing at the wrong item. **Mint a new ID by running
`./New-ItemId.ps1 -Kind BL`** — never by scanning this file or taking "max + 1" by hand. The
counter lives in `.git/item-id-counters.json` (shared by every worktree, outside version
control) and the script increments it under an exclusive file lock, so two concurrent sessions
cannot be handed the same number.
⚠ **Run it for EVERY id, every time — it is not a once-per-session lookup.** Minting one id and
then deriving the next by adding 1, or reusing a number the script handed you earlier in the
session, desynchronises the counter from the file: the id you invented is not recorded, so the
next call hands it out again and the duplicate-id hook fails a later commit. Need several at
once? `-Count n` reserves a block in one call. The failure is silent at the time and surfaces
in someone else's commit, which is why the rule is absolute rather than a default.

**Structure.** Items are grouped into twelve theme sections, in this fixed order: Damage &
destruction · Weapons & combat · Flight model & collision physics · Environment & world · Effects
& animation runtime · Audio · Cameras & views · HUD & UI · Splitscreen · Missions, modes &
campaign · Tooling, platform & docs · Misc. Within a theme, items sort by ascending ID. A straddler goes to the theme
whose system you would open to fix it; Misc is the escape hatch for items with no such system —
if it grows past a handful, that is a missing theme, not a working bucket. Splitscreen is the one
cross-cutting exception: an item whose subject is the single-viewer/single-player assumption goes
there, even though the fix opens another theme's system.

Every item is one flat bullet:

    - `BL-NNN` `[Type]` `[Status?]` **One-sentence claim — the symptom or goal.** body…

`[Type]` is exactly one of: `[Bug]` (behaviour is wrong vs the original or vs intent),
`[Feature]` (something the engine does not do yet), `[Research]` (the deliverable is an answer,
not code), `[Tuning]` (a hand-tuned constant needing judgement at the controls), `[Cleanup]`
(debt with no player-visible behaviour change). The optional status tag is `[Owed-playtest]`
(the code/constant side is done; what is missing is a human at the controls) or
`[Blocked: <blocker>]` (cannot start regardless of priority — the blocker is named: a capture
`CAP-nn`, a milestone, another item, an upstream release, a user decision). No status tag means
open and unblocked.

**Body template for new entries** (existing bodies are reshaped opportunistically, when an edit
touches them anyway): after the bold title sentence, labelled run-in lines, each present only
when it has content — *Evidence:* (what was measured or checked, with `file:line`/data paths —
the one field every entry should have) · *Fix shape:* · *⚠ Traps:* (mechanisms already ruled
out, unit ambiguities, "do not fix it by X") · *Playtest after fix:* (launch command + what to
look for) · *Cross-refs:* (related `BL-nnn`/`CAP-nn`/docs, with why).

## Standing notes

- **Scheduled items live in their plan — do not re-add them here.** If one is closed without
  landing, its record goes in the closing commit's message.
- **[`playtest.md`](playtest.md) is the actionable, consolidated checklist** for everything
  tagged `[Owed-playtest]` or blocked on a `CAP-nn` — what to look for, the launch command, and
  what each blocks. The backlog keeps the deep evidence/traps; keep the two in step.
- **Reference shots are in `OriginalScreenshots/`** (gitignored — cited by filename).
- Bare code paths are relative to the Godot project's `src/`.
- **On the original pre-release design spec:** its structural claims have held up against our
  data — per-hardpoint cluster sizes, the 8-firepoint rig, the zeppelin launch-altitude gate, the
  two-volume danger zones, the armour/health damage split. Its per-item art and balance numbers
  have repeatedly failed — gun ranges, rocket speeds, zone hit points, the crash fireball's
  timing, shell ejection's calibre gate and mount position. Take the mechanism from it, never the
  magnitudes or the art direction, and prefer extracted data or an `OriginalScreenshots/` capture
  wherever either exists. Where an entry rests on the document alone, it says so and marks the
  value TUNE.
- **The flight constants are coupled and `--run-tests` guards them.** Thrust sets speed, speed
  scales the yaw `eff`, so a change in one moves others; `FlightEnvelopeTests` asserts five
  decoded scenarios and will fail if a change breaks one. There is no thrust scale left to turn:
  thrust is `EnginePower · ref_area · curve(Mach) · lever`, every number in it is the executable's,
  and the level-equilibrium curve it produces is asserted per airframe and per lever position
  ([`docs/org/flightModel.md`](docs/org/flightModel.md), "Part-throttle equilibrium"). Chasing a
  *transient* or a feel report through the force path is the forbidden move. ⚠ **`PitchTune`/`YawTune`/`RollTune` are no longer
  in that category: all three are 1, because `FUN_0048c470` carries no per-axis factor on any axis
  ([`docs/org/flightModel.md`](docs/org/flightModel.md), "The `*Tune` rates"), and
  `FlightConstantInventoryTests` now pins them there.** Every asserted target is the decoded
  plant's own value or a named exception; a footage figure that disagrees (the filmed 33.00 °/s
  pitch rate, the 28.60 s `yaw-360`, `accel-150-290`, `decel-290-150`) is discarded and kept only
  as row prose that gates nothing ([`docs/org/flightModel.md`](docs/org/flightModel.md), "Parity
  ledger"). `FlightScenarios` is 5.

## Damage & destruction

- `BL-060` `[Feature]` **Improve on the original crash — the bespoke "breaking apart" (branch `bespoke-crash-animation`).**
  User's call (2026-07-23): the retired bespoke `CrashBreakup` wreck-scatter looked *better* than the
  faithful data-driven crash, so it was preserved on that branch rather than deleted. **The A/B playtest
  passed (2026-07-23) — the faithful data-driven crash is confirmed as the default**, so this is now the
  standing follow-up: once the faithful recreation is fully settled, revisit blending the branch's nicer
  breaking-apart (free-body scatter + down-ray ground-rest) into (or over) the data-driven path — an
  explicit "improve on the original" opportunity, not a faithfulness regression.
  ⚠ **Traps (from slices 1–2, 2026-07-23).** The `blend`/`softParticles` `Puffer.Create` overrides
  exist and default to a byte-identical shader — reuse them; a MIX-blend dark puffer near the
  ground also needs `softParticles: false` or the depth-fade zeroes it. A fading additive fireball
  reads as smoke in a screenshot — isolate the emitter (suppress the others, freeze the crash with
  no `--hold`) before believing an effect is present. Anchor at the plane centre (`pose.Origin` =
  `healthy`), not the impact point. For the debris arcs, the executable decode governs now
  (`PLAN-object-motion-decode`, 2026-08-13): `translation_range` gives `dirY = elevation/90` and
  horizontal `1 − |elevation|/90` (an L1 direction, not spherical), `initial` the launch speed,
  `delta` an acceleration. The retired `DebrisTune.LaunchScale` of 0.65 was a footage fit laid over
  the earlier, wrong spherical reading and is deleted along with the whole tune class — a blend
  here starts from the authored arc, with no compensating scalar. The unscaled arc reads like the
  original at the controls, so nothing is owed on it and there is no scalar to put back. What stays
  TUNE is the `fly_trailN` anchor being invisible so that
  only the trail shows; and the DISTANCE interval hides behind an inverted flag
  (`has_interval_value` false, key off `interval_type`).

- `BL-121` `[Tuning]` `[Owed-playtest]` **Damage (Run-2 item 10)** — breakup scatter, and whether
  the 10c panel-flip and smoke-trail look right in real flight. ⚠ The invented contact constants
  this item used to name are gone: the crash speed, the stop speed, the graze friction and the
  fitted kick are retired against the decoded response and the decoded death rule
  (`git log --grep=BL-271`, `git log --grep=BL-381`), and the graze feel is judged at the controls
  as matching (`git log --grep=BL-120`), so nothing contact-side remains here. What is this item's
  own is the breakup-scatter feel judgement.
  Rendering at real spawns is verified (the `TopLevel` anchor fix, `docs/HISTORY.md`
  2026-08-03, `trail-world-anchor` suite); this item is a magnitude/feel judgement. Tree softness is retired dead code
  (`docs/HISTORY.md` 2026-07-23), not a TUNE — do not re-add it here.
  **`CAP-15` analysed 2026-08-05** (the burn-down half of `CAP-14 Graze and CAP 15 wing to
  red.mp4`, 37.45 s Bloodhawk chase clip; stills + per-frame fire counts in `playtest/CAP-15/`;
  times are wall-clock PTS, sim-s = ×1.390). The 10c look verdict, part by part:
  (a) **Panel flip matches.** The original's visible damage stage is a skin swap — the outer
  third of the right wing turns charred black at the threshold (all-red before contact), with no
  large flapping geometry readable at chase distance; our torn-`pdpN`-shown/`_h`-hidden swap is
  the same mechanism. Ignition is on the contact frame itself (t = 5.886).
  (b) **Trail staging does NOT match.** The original streams `short_firetrail`'s full
  three-puffer stack from the damaged panel — fire_f01–06 flipbook + orange-born smoke + a
  pure-black smoke puffer, deactivating at the authored 4/6/8 s `EVENT_OFFSET`s and
  sputter-looping while the panel is active. On film: fire-dominant ~12.4 wall-s (peak 9,418
  orange px at t = 6.82), then two pure-black wing plumes, then thinning, sputtering smoke still
  going ≥ 31.5 wall-s after contact at clip end. Our per-panel trails are four bare `firepuffer`s
  (`FlightRigAssembler.cs`) — fire flipbook only: no black-smoke phase, no staged burn-out, no
  sputter. **That gap is closed** — `BL-259` landed 2026-08-05: the panels play the authored
  staged burn (fire → black → sputter), census-matched to this clip's 8/6/4/2 sim-s cascade.
  (c) **Gauge timing confirmed:** the damage silhouette's right-wing segment goes RED (nose
  YELLOW) on the first lit blink ≤ 0.25 s after contact, then blinks lit/dim persistently.
  (d) The clip contains **no nose-anchored trail** even with the wing red-critical for 30 s —
  moot in code since `BL-259` landed (2026-08-05): nothing anchors at a synthetic nose offset
  any more; the heavy stage plays `player_damage_trail` at `prop1`. *When* is settled by the decode
  (`FUN_004b3800`, `docs/org/vehicleDamage.md` "Damage staging"): the whole-vehicle health fraction
  at 10%, which a graze that leaves the hull healthy never reaches — so this clip showing no
  whole-plane trail is expected, not a puzzle.

- `BL-442` `[Bug]` **The engine sputters at the slightest damage.** Reported at the controls: one
  shallow graze and the engine note drops to a sputter. `FlightAudio.UpdateEngineSlot` swaps to
  `snd_damagedengine` on any damage at all (`damageFrac > 0`, any zone below full), and
  `EngineAudioCurves.EngineDefFor` draws the pitch multiplier uniformly across the authored
  `[DamagedEnginePitchLo, Hi]` range, so a single graze can land a 0.02 pitch draw. Decode what the
  original keys the damaged-engine swap on (a health fraction, the engine zone, or a damage stage)
  and whether the pitch is drawn or derived, then port that. ⚠ Traps: do not add a threshold by
  feel; the damage-stage decode in `docs/org/vehicleDamage.md` is the place the gate probably
  lives. Nitro's engine variants (`git log --grep=BL-089`) share this slot, so check both.

- `BL-122` `[Tuning]` `[Owed-playtest]` **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)** — several playtest-gated TUNEs,
  all needing the original at the controls: the **debris-arc trajectory** (the executable decode is settled — `translation_range` gives
  `dirY = elevation/90` and horizontal `1 − |elevation|/90`, `initial` the launch speed, `delta` an
  acceleration, `PLAN-object-motion-decode`, 2026-08-13; the former `DebrisTune.LaunchScale` footage
  fit is deleted with no replacement scalar, and the arc's *look* is settled too: the unscaled arc
  reads like the original at the controls; the `fly_trailN` anchor being invisible means only the
  trail's rough scale reads); the **overall crash intensity** (the fireball, the cluster, the debris
  fire and the wreck fire are all additive, so a dirt crash can read as one big fireball — judge the
  whole against the original); and `snd_exp_ground_a` mix level + whether it should layer over
  `plane_destroy_sg` (the dirt def's only Sound is `snd_exp_ground_a`; we keep both). The retired
  bespoke crash on branch `bespoke-crash-animation` is the A/B reference for these.
  **`CAP-16` analysed 2026-08-04** (`CAP-16.mp4`, 2560×1440, 13.49 s; corroborated by
  `C1 IA1 Crash.mp4` and `CAP-14 Crash.mp4`, two further ground crashes with the same signature;
  stills in `playtest/CAP-16/`). ⚠ All times below are **wall-clock** off container PTS — multiply
  by k = 1.390 for sim-seconds before comparing against any authored `run_time`.
  - **The crash pieces barely turn, and the decode since 2026-08-13 says they do not turn at all.**
    A wing panel detaches at ignition and stays legible for 8 sampled frames, t = 6.13 → 6.60
    (0.47 s wall / 0.65 sim-s; `wing-tumble-strip-6.13-6.60.png`). Its long axis rotates only
    **~10–15° over that span** — order 20–30 °/s wall-clock, against the ~150 °/s the ÷`run_time`
    reading of the day predicted. `FORWARD_ROTATION` is now decoded (`PLAN-object-motion-decode`
    C10): the crash `pieceN` fly the vector `TRANSLATION` form, which fills none of the launch
    direction cache the tumble multiplies through, so they hold their orientation and what the strip
    shows is the piece's path plus camera motion. Recorded as agreement, not as evidence — the
    footage is one piece, near edge-on under camera motion, and no measurement off it decides a
    decode.
  - **Wreck momentum on a ground crash — there is none, and the owed judgement is the look of that.**
    No fraction is authored or applied: the `player_crash_*` defs this path plays inherit nothing
    (`player_crash_dirt` authors `impact_force` false, `player_crash_default` never arms the
    instance), and the original scales the inheritance nowhere in any case
    (`docs/org/objectMotion.md`). The pieces therefore stay where they blew rather than scattering
    along travel. ⚠ `CAP-16` shows a panel travelling down-and-forward and chunks scattered
    laterally at rest, which reads as inheritance; it is one piece near edge-on under camera motion,
    with no known impact speed, and no measurement off it decides a decode. What this item still
    owes is the whole crash judged at the controls, not a number.
  - **Overall crash intensity — "one big fireball" is correct for ground, and is surface-dependent.**
    The dirt crash genuinely reads as a single dominant fireball: granular yellow sprite cluster at
    ignition (t = 6.27), white-hot core with orange body by t = 7.40, still at full intensity at
    t = 12.50 when the clip ends. The concern that the additive stack over-reads is **not supported
    for ground crashes** — the original looks like that. ⚠ But `CAP-14 Building crash Balmoral.mp4`
    (t ≈ 5.0) shows a *building* strike as a spread of small discrete orange puffs with **no** large
    fireball and **no** dark halo. Do not tune the two surfaces to one look.
  - **The smokeball is dark red-brown, not black** (t = 8.00–12.50), in all three ground crashes.
  - **Dirt burst reads as a separate, lower, ground-coloured cluster** — three distinct pale-cream
    puffs sitting at the ground line beneath the fireball at t = 7.40, clearly not fire-tinted.
  - **Audio: the two sounds are sequential, not stacked.** Two spectrally distinct onsets — one at
    t = 6.15 (+4.5 dB, spectral centroid **958 Hz**, mid-band dominant 55.8%: the airborne breakup)
    and a deeper one at t = 7.09 (+5.3 dB, centroid **711 Hz**, low-band dominant 55.7%: the ground
    explosion, coincident with the dirt burst appearing). They are **0.94 s apart wall-clock
    (1.31 sim-s)**, with near-equal peaks (−15.1 / −15.2 dBFS). So layering both is right, but they
    should be **offset ~1.3 sim-s**, not triggered together. Mix level is modest: the crash peaks
    only **~+5 dB over the engine bed** (bed −19.1 dBFS median) and never clips. ⚠ Sound *identity*
    is inferred from timing against the visuals, not decoded — the footage cannot prove which onset
    is `snd_exp_ground_a` vs `plane_destroy_sg`.
  - **Duration: the effect never visibly ends, and it never can — the game takes the scene away.**
    A fatal crash returns the original to the menu, so there is no single-player vantage from which
    the fire burns out on screen. `CAP-16` catches that cut: mean frame luma holds *flat* at 50 from
    impact until **t = 12.65**, then fades to black by t = 13.00 (a ~0.35 s fade, the return to
    menu). The fireball is at **full intensity when the fade starts** — it is not decaying. So the
    number to build against is not a burn-out time but a **hold time: 6.52 s wall / 9.06 sim-s from
    ignition to the fade**, during which the effect must not visibly thin out. The other two ground
    clips have no black frame at all (recording simply stopped while lit), so `CAP-16` is the only
    one that captures the cut.
  - **Crash audio ends naturally at 5.47 s wall / 7.60 sim-s after ignition** — i.e. ~1.1 s *before*
    the visual fade begins, so the last second of the burning wreck is silent. The envelope decays
    smoothly (−20 → −25 → −32 → −41 dBFS across t = 9.1 → 11.55) and reaches digital zero at
    t = 11.60; an interrupted recording would have truncated at a non-trivial level instead.
  - **Fireball SIZE, measured against the plane as an in-frame ruler — our burst is ~2.5–3× too
    big, and the `SizeScaleDefault` ×4 stand-in is the reason.** ⚠ Estimate, not a decode. Method:
    the Bloodhawk (`player_bhawk`, the plane in `CAP-16.mp4`) has no wingspan field anywhere in
    data, so the reference is the mesh-AABB figure recorded in `PlaneCollider.cs:31-34,56-60` —
    **full span ≈ 11.6 m** (half-span 5.8 m, cross-checked off `WingBandFrac 0.35` × half-span =
    2.03 m). At t = 6.27 (0.14 s wall / 0.19 sim-s after ignition) the *still-attached* wing and the
    fireball are in the same frame at the same camera depth, so no cross-frame distance assumption
    is needed: wing root→tip ≈ 215.6 px for 5.8 m ⇒ **≈ 37 px/m**; the fireball's fire-keyed pixel
    span is 380 × 342 px ⇒ **diameter ≈ 361 px ≈ 9.7 m**. Sources of imprecision: the "root" point
    is the visible fuselage seam, not the true centreline (under-measures half-span, so the 9.7 m
    is if anything a slight over-estimate), and it is one sample from one frame.
    - Against our build, simulating `flame_ball_01-large_fireball`'s `fierypuffer` verbatim
      (SIZE_RANGE 2–4, GROWTH 1→3, ±65 m/s, friction 9, number 18, 2 bursts, life 0.8–1.0) through
      `Puffer.SpawnBatch`/`_Process` at dt = 1/60: the **particle cloud** (centres only) is **8.9 m**
      across at t = 0.14 and settles at ~12.7 m — i.e. the authored velocity/friction spread matches
      the footage almost exactly, and needs no change.
    - What does not match is the **sprite size**. `Puffer.SizeScaleDefault` is **4** — explicitly a
      judged stand-in for a missing engine constant, not a decode (`Puffer.cs:276-281`, TUNE settled
      at the controls 2026-08-01) — and the quad side is that size in metres (`QuadMesh` 1×1 scaled
      by it, `EmitterRenderer.cs:134,150-156`). At ×4 the mean sprite is 15.8 m across at t = 0.14
      and the whole burst spans **27 m**, reaching **54 m** by end of life. At ×1 (authored verbatim)
      it is **13.2 m** at t = 0.14 — within ~35% of the footage's 9.7 m, and the gap closes further
      once the fire sprite's soft alpha edge is allowed for (visible fire is well inside the quad).
      **So the footage puts the missing constant near 1, not 4** — for the crash burst at least.
    - ⚠ Tension, not a verdict: ×4 was chosen because at ×1 the emitters "read as a thin scatter of
      specks against the original's volume." Both observations can be true — the deficit at ×1 may
      be *density* (18 sprites) or sprite alpha rather than size, in which case the fix is more/
      denser particles at authored size, not bigger ones. The knob is per-path, so this bears only
      on **`puffer.burstSizeScale`**, not on the trail/sustain scales that were judged alongside it.
    - **2026-08-09 update, superseding the ×4/×1 analysis above:** `PLAN-puffer-engine-deltas` traced
      `SizeScaleDefault` to an exact decoded constant — `FUN_0057c5c0` hands `SIZE_RANGE` to the
      sprite draw as a screen-space HALF-extent, so the quad side is `2 × SIZE_RANGE`, not `1 ×` —
      and separately found `DEVIATION_DISTANCE` was scattering `±d` where the engine draws `±0.5·d`
      (A2). Re-running this same measurement (`RecordingEmitterRenderer`, `fierypuffer` verbatim,
      `Puffer.Burst`/`_Process` at dt = 1/60 to t = 0.14) on the unchanged build read **mean sprite
      4.10 m, cloud span (centres) 12.52 m, whole burst span 16.62 m** — already past the footage's
      9.7 m at the old ×1 default, not under it as the analysis above concluded (that analysis used
      an analytic estimate, not this instrumented one; the two are not directly comparable). At the
      landed A1+A2 constants (`SizeScaleDefault` 2, deviation halved) the same measurement reads
      **mean sprite 8.20 m, cloud span 12.53 m, whole burst span 20.73 m** — cloud span is
      unchanged (`fierypuffer` authors no meaningful `DEVIATION_DISTANCE`; its spread is almost
      entirely the ±65 m/s random velocity, so A2 does not move this particular puffer), and mean
      sprite doubled exactly with the constant, confirming the intervention took effect. **The
      decode makes the crash burst read larger against the footage, not smaller — the opposite of
      what the ×1 analysis above expected.** Per `PLAN-puffer-engine-deltas`'s explicit instruction,
      the decode lands anyway and this is recorded as a finding, not split against the footage: the
      corrected sim's sprite may still be reading too big against `CAP-16` for a reason A1/A2 do not
      touch (sprite alpha falloff inside the quad, or the fire-keyed pixel measurement in the
      original bullet finding less than the full additive quad) — that is now a live open question
      for whoever next tunes `puffer.burstSizeScale` or the fire family's TUNE pair (D10), not
      something A1/A2 should absorb by picking a different constant than the one the disassembly
      settles.
    - ⚠ **The 9.7 m figure is a video frame measurement and is not evidence about this constant.**
      That governs every comparison in this entry: a frame-derived length cannot confirm a decode
      (`docs/verification.md` DET-12), and the sim disagreed with it at the old `×1` default too
      (16.6 m vs 9.7 m), so **no value of `SizeScaleDefault` ever reconciled the two**. Treat the
      gap as a note about sprite alpha or particle density, not as an open question hanging over
      the decode, and do **not** re-open A1 on the strength of it. Qualitative reads from the clip
      (is there a fireball, does it persist) remain useful; a measured span from it does not, and
      no re-measurement of the footage is owed.
    - Limit: the ruler only exists near ignition (the airframe is gone within ~0.5 s and no known
      length survives in frame), so this is an *early-frame* comparison. Our burst is dead by ~1.0 s
      while the original is still at full intensity 9 sim-s later — that gap is the hold time above,
      a separate matter from size.
  *Residual.* The player's **own** crash can never show the burn-out — the game cuts to menu — so do
  not re-film one hoping for it. The one untried vantage is an **enemy** plane crashing while the
  player stays alive, which would keep the scene up; worth a capture only if the hold time above
  turns out to be the binding constraint when tuning. Otherwise what remains is the **A/B against
  our build at the controls**, with the reference numbers above to judge against.

- `BL-297` `[Research]` `[Owed-playtest]` **Panel-damage semantics: what the original actually
  shows when a part is damaged — the user's re-test verdict is that our authored-data reading has
  the feature wrong.** User at the controls 2026-08-06, after `BL-288`'s pooling fix landed
  (bursts no longer teleport — that mechanical fix stands and is not in question): (1) nose
  damage sprays effects at the WINGS; (2) panels appear to tear while armor should still be
  absorbing; (3) identical repeated debris bursts read as "the same panel flies away again" — a
  torn panel should be gone once. Expectation: debris matches the point of destruction, is
  health-gated, and each panel tears exactly once.
  *Evidence (data reading, 2026-08-06):* the per-part `injure_anims` DO map panels to their part
  (`extracted/zrdr/vehicle.zrd.json`, player-1: nose→`pdpanel7`, tail→`pdpanel8`,
  leftwing→`pdpanel5`/`4`/`3` at 0.5/0.3/0.15, rightwing→`pdpanel6`/`1`/`2` at 0.4/0.3/0.15). The
  cross-part bleed the user sees is authored *elsewhere* in our reading: (a) every part's 0.99
  `<part>_damage_effects` shim → `random_gun_impact`, which sparks a random `pdp1` (40%)/`pdp2`
  (40%) and ALWAYS `pdp4` — wing sites, whatever part was hit; (b) the vehicle-level 0.85
  `player_fuelleak` (ANY part's fraction) plays a gunhit flash + fuel vapor at a random `pdp1–3`.
  The "repeats" have two shapes: `pdpanel7` (nose) is authored to throw FOUR `gimmeflakes` bursts
  within 0.4 s (one extended burst), and every panel's burst uses the same 7-flake `planeflakes`
  template, so successive panels' bursts look identical.
  *Decoded 2026-08-15 (`crimson.exe`). All three symptoms are settled without the capture.*
  Write-up: `docs/org/vehicleDamage.md` ("Damage staging", the two new subsections).
  **(2) armor, answered; ours is wrong.** Not a scale ambiguity: `FUN_004b3d70` keys the per-part
  `injure_anims` on `[part+0x30] / [part+0x2c]`, health only, and `FUN_004b7f80` zeroes the health
  damage outright while the part's armour pool covers the incoming armour damage. So a fully-armoured
  part crosses NO per-part threshold, not even the 0.99 `<part>_damage_effects` shim: the original
  shows nothing at all on a fresh armoured plane. Our combined armour+HP scale
  (`PartState.Fraction`) was why panels tore early; that is **fixed**, the per-part loop re-based on
  `PartState.HealthFraction` and the hull loop split out onto `SummaryHealthFraction`
  (`git log --grep=BL-384`). Owed at the controls as `PT-80`.
  **(1) location, answered; ours is faithful in mechanism and wrong in timing.** `FUN_00521180` binds
  an anim's node names through `FUN_004efaf0`, which searches the instance's context subtree, then
  the anim's local tables, then a GLOBAL by-name lookup (`FUN_004d0280(7, name)`). `pdpN` names are
  unique on the airframe, so a context miss falls through and finds the same node anyway: the
  context disambiguates a name, it never redirects one. There is no part-relative retarget on this
  path, so the original really does spark wing sites on a nose hit. It just does not do it until
  that part's armour is gone.
  **(3) repetition, answered; a panel tears once.** The handle arrays (part`+0x4c`, inst`+0x890`)
  start an entry only when its slot reads zero and clear the slot on the UPWARD crossing alone,
  never when the anim ends. So each entry fires once per downward crossing and can only re-fire
  after a repair (`FUN_004b3e20` wipes, `FUN_004b8180` restages). The `pdp4` repeat the user saw is
  authored and faithful: `<part>_damage_effects` is a separate entry on each of the four zones with
  its own slot, so `pdp4` legitimately sparks up to four times a flight, once as each zone first
  crosses.
  *Fix shape:* no code change is owned here. Symptom (2) is fixed (`git log --grep=BL-384`),
  and is `PT-80`'s to confirm at the controls. Symptoms (1) and (3)
  are faithful-as-authored and this item closes on them once `CAP-29` confirms the look. Do NOT
  resolve `random_gun_impact`/`player_fuelleak`'s panel pick to the nearest pdpN. The decode says
  the original does not do that.
  *What `CAP-29` still owes:* the look only. Does the flung debris read as a piece of that panel or
  as generic flakes, and what visibly changes on the airframe. Questions (a) and (b) are now
  confirmation, not decision.
  *Not decoded:* whether the interpreter's selection event really is the 40/40/always-`pdp4`
  weighted pick our data reading describes. The exe executes the authored def; what was verified is
  where the nodes resolve, not how the random branch is evaluated.
  *⚠ Traps:* do not "fix" by suppressing the authored shims wholesale (`CAP-27` already probes
  whether the spark shim exists at all in the original — coordinate, don't overlap). Do not
  re-open `BL-288`'s pooling — the theft mechanism was real and its fix is verified independent
  of these semantics.
  *Cross-refs:* `CAP-29` (the capture), `CAP-27` (spark-shim existence), `BL-288` landing
  (`docs/plans/PLAN-m3-polish-10.md` A1), `DamageVisuals.cs` (the consumer),
  `extracted/zrdr/vehicle.zrd.json` (the authority).

- `BL-394` `[Bug]` **AI identity: landed, and owed a flight.** `PlaneStats.LoadForAi` takes a def
  name, so a spawn flies a militia variant (`bhatwarhawk`, `secfury`) and gets that def's damage
  model, its `weapons` fit (`Loadout.BindAi`, bound through the same `Loadout.Bind` and told apart
  from guns by the weapon's `CANNON` flag), its authored livery and its nine-slot pilot vector plus
  `accentID`. `--ai=<plane>:def=<name>` names one directly.
  *⚠ Instant Action names none, and that is decoded, not assumed.* `FUN_0045a390` takes each spawn's
  aircraft as an index into the eleven-row plane table at `0x00620c70` and reads the PLAIN AI def out
  of it (`FUN_00426d20`), so an Instant Action Black Hat Warhawk is the plain `warhawk`. The militia
  supplies the livery alone, written into the spawn's override record from the setup-screen record at
  `0x00718dcc`. The militia defs are the campaign's, whose mission rosters name them outright. That
  is also why a Sacred Trust Warhawk exists in the original with no def behind it
  ([`docs/formats/instant-action.md`](docs/formats/instant-action.md)).
  *⚠ One militia stays unpainted:* Broadway Bomber. `BROADWAY` ships six `PEA_*` masks and no def
  anywhere authors `paint_pattern broadway`, so there are no colours to fill them with; they have to
  be read off the original as `player_fortune`'s were.
  *What still routes nothing:* a campaign mission's own enemy set, which is where the militia defs
  belong, and the `--generators` waves.
  *Owed at the controls:* fly a Black Hat flight
  (`--ai=player_warhawk:def=bhatwarhawk --ai-attack`, or a wizard wave set to Black Hat Warhawk) and
  confirm the militia paint, the eight torpedoes, and that they stay on the rail against aircraft.
  *Still open inside the landed work:* `dare_devil` is parsed and unconsumed; each authored ordnance
  entry takes ONE pylon carrying its whole round count, since the original counts rounds per weapon
  slot and has no pylons at all; a def authoring more entries than the airframe has pylons drops the
  overflow.
  *What the AI defs actually author (data, 2026-08-16).* A `weapons` block of 5-tuples —
  `fury`: `[wep_04, 4, 200, 30, 800]`, `[wep_07, 2, 200, 30, 800]`, `[wep_130, 9000, 0.05, 1, 900]`
  — overridden per militia variant (`secfury` swaps to `[wep_12, 6, 30, 200, 800]`,
  `bhatwarhawk` to `[wep_14, 8, 5, 350, 800]`). Plus `paint_pattern`/`paint_color1..3`/
  `paint_decal1..3` (the militia livery), the nine-slot pilot skill vector (`dare_devil`,
  `dead_eye`, `quick_draw`, `steady_hand`, `sixth_sense`, `natural_touch`, `stun_recovery`,
  `talker`, `constitution`), `accentID`, and `gun_pitch`/`gun_yaw` (the AI's forward-gun cone,
  `[-11, 11]` on every AI aircraft).
  *The militia mapping is already in the tree, undecoded as such.* `UI/LaunchMenu.cs`'s `Militias`
  table (13 militias × their aircraft) reproduces the militia def names exactly: `bhat*` = Black
  Hat {Warhawk, Brigand, Autogyro}, `bs*` = Black Swan {Fury}, `blake*` = Blake Aviation, `brit*` =
  British, `ha*` = Hughes Aviation, `hk*` = Hollywood Knight, `med*` = Medusa, `rus*` = Russian,
  `sec*` = Studio Security, `sti*`/`german*` = the two Hellhound militias. Two table entries have
  no def (Sacred Trust's Warhawk, Broadway Bomber's Peacemaker) — expected, since that table comes
  from `.BM` paint coverage, not from `vehicle.json`.
  *The 5-tuple is decoded, so that risk is gone:*
  `[weapon_id, rounds_carried, refire_interval_s, min_range_m, max_range_m]`, read out of the
  builder `FUN_004b59b0` ([`docs/org/aiPilot/aiWeapons.md`](docs/org/aiPilot/aiWeapons.md),
  census in `analysis/ai-ordnance-census/`).
  ⚠ Five base defs (`firebrand`, `bloodhawk`, `brigand`, `fury`,
  `autogyro`) author fields 3 and 4 transposed against all 25 militia variants, `200, 30` against
  `30, 200`, so they run a 200-second ordnance refire. That is shipped data; carry it, do not
  "fix" it.
  *The paint keys are decoded too.* `FUN_00479240` parses `paint_pattern` (`+0x220`),
  `paint_decal1..3` (`+0x230`/`+0x234`/`+0x238`) and `paint_color1..3`
  (`+0x23c`/`+0x248`/`+0x254`, three components each) into fixed slots in authored order, and
  `FUN_0047c210` resolves a spawn's scheme **per field** against a per-instance override: a decal
  falls back to the def below `-1`, a colour component on any negative, so an AI aircraft with no
  override wears its own def's scheme. Write-up:
  [`docs/org/paint.md`](docs/org/paint.md).
  *⚠ Trap, handled:* `stock_loadouts.json` holds the eleven `p*` defs alone, so an AI def name run
  through it disarms the plane. `FlightRoster` binds the def's own fit first, falls back to the
  table, and says so in the log when neither arms the plane.
  *Size:* what remains is localized — the same resolution wired into a campaign mission's enemy set
  and into `--generators`, plus the cockpit confirmation.
  *Cross-refs:* `BL-386` (the damage half — landed and closed 2026-08-16,
  `git log --grep=BL-386`; this builds on the `PlaneStats.AiDefName` seam it left),
  `docs/formats/vehicle.md` (the def-family census), `docs/formats/instant-action.md` (the militia
  table's provenance).
  *The AI ordnance trigger reads the fit now.* `AiRocketeer` takes each pylon's own engagement band
  and refire interval, and stamps both timers a launch stamps in the original: the vehicle-wide
  lockout that blocks ordnance of any kind and the launching slot's own next-ready. `AiGunner` takes
  a bound group's window the same way. Launch rates are worth tuning from here, and the
  `DAMAGES_ZEPPELIN` rule is exercisable in the cockpit rather than only in `AiRocketeerTests`.

- `BL-440` `[Feature]` **A downed zeppelin never comes apart: the authored breakup stalls on an
  unimplemented `NodeUndercover` gate.** *Evidence:* the kill already plays the authored hull-death
  def (`ZeppelinRuntime.PlayHullDeath` → `killpzep`, e.g.
  `extracted/C1/M04/mis_anim/piratezep-killpzep.json`), and that def's `main_altitude_check`
  sequence is `Initial`, so it runs from the moment the def starts: it tests
  `If NodeUndercover(rock_zeppelin)` and, on the else branch, loops forever (`Loop -1`).
  `AnimRuntime.cs:2476` stubs `NodeUndercover` to a constant `false`. The gate therefore never
  opens, `rotatezep` and `breakupzep` are never called, and nothing reaches the `StopSequence` that
  ends `floatdown`'s −3.5 gravity descent, so the hull sinks intact instead of pitching over and
  breaking up.
  *What the data authors:* `rotatezepdown` pitches `rock_zeppelin` 0 → −15° over 8 s and
  `rotatezep` eases it −15° → −7° over 0.5 s at the break; `breakupzep` then fans out 13 same-tick
  CALLs (the deepest authored fan-out in the game, the one that sized `SequenceRunner`'s cap) —
  `break1`…`break6` drop each gasbag under −9.8 gravity with a slow forward tumble and a
  `bounce_sequence.water` of `hit_waterN` (a `huge_splash`, then `huge_ripple` 0.5 s later at that
  gasbag), `breakunder` translates `underneath` −35 m over 2 s and deactivates it, and six
  `break_[lr]eng[123]1` each gate on their own `NodeUndercover` before calling `destroy_pz…`.
  *Fix shape:* the gate is the whole feature — a real ground/occlusion probe behind
  `NodeUndercover`, since the motions themselves are `ObjectMotion`/`ObjectMotionFromTo`, which
  `MotionRuntime`/`PoseChannel` already run.
  *⚠ Traps:* (a) the stub is global and its own comment justifies itself by "all 473 uses sit in
  `OnCall` definitions the bootstrap never reaches" — that premise no longer holds for `killpzep`,
  and making the condition real changes every other def that reaches it, so the goldens are the
  check. (b) the condition's `distance` operand arrives as a raw u32 (`3263299584` on the hull
  test, `3229614080` on the engines) and is not a length until decoded. (c) effect templates snap
  to absolute world points and never track a moving host (`ZeppelinRuntime.cs:524`), so a splash
  authored at a falling gasbag has to be placed from that gasbag's position at the moment of the
  call.
  *Status of the symptom:* read out of the data and the stub, not yet watched at the controls.
  *Playtest after fix:* an Instant Action `zeppelin_run`, torpedo the hull down, and watch it pitch
  over, shed six gasbags with splashes, and drop the gondola.
  *Cross-refs:* `BL-291` (closed — Instant Action's `zeppelin_run` plus a `wep_14` pylon is the
  spawn-and-kill harness this needs, `git log --grep=BL-291`), `docs/architecture.md`'s
  `ZeppelinDamage.cs` bullet (the survivor-count kill that fires the def).

## Weapons & combat

- `BL-407` `[Bug]` **World objects are hostile to everyone; the original leaves an unauthored one
  neutral.** *Evidence:* the team-space decode ([`docs/org/targeting.md`](docs/org/targeting.md)
  "The team space"), taken while unifying the emplacement and aircraft team spaces
  (`git log --grep=BL-403`). CSVM stamps every destructible with
  `AimAssist.WorldTeam` (100) through `AddStructures`'s default, which `FlightController.cs:1873`
  takes on every frame of every session, so a crate is hostile to every pilot alike. The original
  builds a world object through the same constructor as an aircraft (`FUN_004a3360` →
  `FUN_004a2570`) and takes its team from a **two-bit ownership field** on the scene node, walking
  the node then its ancestors (`FUN_004a32f0`, called at `0x004a3493`); when no ancestor carries
  one it falls through to **neutral**, and the hostility predicate's neutral clause is what makes
  it untargetable.
  ⚠ **Do not just change the constant to 0.** CSVM reads no ownership field, so every world object
  would go neutral at once and the gun assist would fall silent over every ground target and every
  zeppelin gasbag. The work is to find whether any CSVM world node carries authored ownership
  first, and only then to decide whether hostile-to-all stays as a remake-only rule.
  *How you'd know it worked:* ground targets and gasbags still take assisted fire, and whatever the
  ownership field turns out to select still does.
  *Cross-refs:* `BL-400` (the other half: structures on the Non-Aircraft SELECTION cycle need a
  curated `targets.zrd`-equivalent list — this item is the gun assist's team, they fail in
  different subsystems), `AimAssist.WorldTeam`, [`docs/org/targeting.md`](docs/org/targeting.md)
  ("The team space", where the decode this splits off from is written up).

- `BL-066` `[Feature]` **M3-deferred — ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- `BL-226` `[Feature]` `[Blocked: cockpit view]` **The incoming-fire cue set's other two halves are blocked on things that do not exist
  yet.** The near-miss third landed (`BL-087`, 2026-08-02); `bullet_hit_sg` (= `snd_ricochet1-4`,
  `player.json`'s `bullet_hit_sound`) and `window_hit_sg` (= `snd_windowhit1-3`, non-3D) did not.
  Both sit on the five `player_pfighter-bulletN` canopy-hole defs (the `bullethole_anims` of
  `docs/formats/vehicle.md`, 10 files per chapter × 8), so they are shipped and referenced, not
  orphans. The design's rule is that incoming-fire intensity is how the player reads a shooter's
  distance, calibre and ammo type; the accumulator that rates it is now decoded and running
  (`WarningShotCue`), so both cues can hang off it once their blockers clear.
  ⚠ **Traps.** (a) **`bullet_hit_sg`'s shooter blocker is GONE (2026-08-13):** since M4 A2+D14 an
  AI gunner fires real rounds at the player (`--ai=… --ai-attack=…`), so this half is wireable
  now — it hangs off the projectile-hit path, not the near-miss accumulator alone.
  (b) `window_hit_sg` additionally needs a cockpit view —
  the bullet defs are `PlayerFirstPerson`-gated. (c) **Do not fake either off our collision path**:
  firing the hit cue on a wall scrape conflates "I was shot" with "I hit something" and would make
  both wrong (the same rule that kept the `player` IMPACT row honest; its closing commit is
  `git log --grep=BL-222`). (d) Only `snd_warningshot1-3` are true orphans (in no `SOUND_GROUPS`
  entry and named nowhere) — do not conflate the four groups.

- `BL-227` `[Tuning]` `[Owed-playtest]` **Blast knockback magnitude (D10, 2026-08-01).** The
  splash falloff half of this item is closed: `Projectile.ApplyDamage` deals
  `damage × (1 − d² / IMPACT_PROXIMITY²)` to both pools, cover-tested and capped at 32 targets, per
  the decode in [`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md) "Half two, the splash"
  (`PLAN-ordnance-types` C10/C11). What stays open is the push: a directly struck rigid body takes
  `BlastImpulsePerDamage = 1 N·s` per point of damage, an invented magnitude.
  ⚠ **The impulse is decoded and is not a TUNE.** `FUN_004b9bc0` applies a per-hit impulse
  (`FUN_0048f5e0`) whose two magnitudes are `damage × vehicle_def[+0xa0]` scaled by **0.005** and
  **0.0333**, gated on the larger damage figure exceeding **5.0**
  ([`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md)). So the original scales with damage and
  with a per-airframe constant, carries two magnitudes rather than one, and has a threshold below
  which nothing moves. Read the constant and consume it; do not tune `BlastImpulsePerDamage`.
  ⚠ Traps: do not retune the authored radius or fuse distance; `DAMAGE 0` specials carry large
  effect radii and are deliberately excluded from blast damage. The per-round yield factor at
  `+0x678` (scaling radius and both damage figures together) is not modelled; nothing observed writes
  it other than 1.

- `BL-230` `[Tuning]` `[Owed-playtest]` **Near-miss trigger distance (B7, 2026-08-02).** `WarningShotCue.PassRadius` = **15 m**,
  the distance a round's swept step must pass within to sound `bullet_warning_sg`. Chosen, not read:
  the shipped `warning_shot_*` block rates the cue but says nothing about how close is close, and the
  sound def's `RANGE [20,200]` is the 3D falloff window, not a trigger radius. Judge it at the
  controls — `--incoming=<metres>` walks a burst past at a chosen distance, and
  `weapons.warningShotRadius` moves the threshold without a rebuild (config.json, so **not** under
  `--det`).
  ⚠ Traps: `CANNON_SPREAD` scatters each round several metres over any real firing range, so the
  achieved distance is a distribution — judge over a burst, never off one pass. Raising it far enough
  that a round crossing the sky sounds is the failure mode, not a louder cue.

- `BL-233` `[Feature]` `[Blocked: M4]` **Extend the proximity fuse to zeppelins (and any other M4 flyer) when they get
  bodies.** The fuse itself came back 2026-08-06 (PLAN-vs-mode B14): re-enabled **aircraft-only**
  against the registered `AircraftBody` list — never world geometry, matching the user's
  recollection that the original fuses on enemies, never terrain — detonating at the round's
  **closest approach** within the swept step, with the fused-on body carried into `Impact` (the
  per-surface IMPACT entries are reachable; the old null-collider bug cannot recur). DD = fuse
  trigger distance, IP = effect radius (settled 2026-08-02 from the choker/torpedo/flare census —
  see the B14 landing commit for the full argument). Remaining work: when M4 gives zeppelins (or
  anything else that flies) collision bodies, they join the fuse's candidate list — the natural
  seam is `CollisionLayers.Aircraft` or a shared targetable layer read by
  `ProximityFuseTriggered`'s registry.
  ✅ **The aircraft-only rule is now decoded, not recollected.** `FUN_004b5fb0` runs the fuse over the
  aircraft list (`DAT_0071dabc`) and never touches world geometry
  ([`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md)). Also decoded: when the weapon carries
  `DETONATION_DOT_PRODUCT`, range alone does not trigger it. The dot is taken against **the
  candidate's** orientation axis, not the round's, and must reach the authored threshold.
  ⚠ Traps: (a) the six plain rockets author `DETONATION_DISTANCE == IMPACT_PROXIMITY`, so a
  first-entry-into-range fuse always detonates exactly where the blast falls to zero and
  deals nothing — the closest-approach rule is load-bearing, keep it for any new candidate class;
  (b) a world-armed fuse re-detonates every rocket 15–50 m short of terrain (the 2026-08-02
  failure) — never widen the mask to world bodies.

- `BL-286` `[Tuning]` `[Owed-playtest]` **Muzzle-flash residues after the `BL-263` pick (triad kept, 2026-08-05)** — two
  small opens. (a) closed 2026-08-06: the muzzle-light stand-in magnitudes (was `BL-200`, rode
  `BL-261`/`BL-263`; `MuzzleLightEnergy` 2.5, 2-frame `MuzzleLightLife` 0.03 s — the def carries
  range/colour only) are signed off, judged in `--weapon-lab`; static, so the sign-off covers
  magnitudes only, not motion. (c) Anchor the flash quads AND the muzzle light to the muzzle
  point: at the controls 2026-08-06 the flash sits visibly forward of the muzzle ("direct at it
  would look better"), and the light is still world-fixed — at speed it lags the plane by ~2 m
  for its 2 frames, which the weapon-lab sign-off could not see. Same anchoring work, one
  landing; re-judge both **in flight**, not the lab. When landing, check whether the forward
  offset is authored (a node offset in the def) — if so this is a deliberate deviation and the
  entry's close should say so. (b) The user's engine-semantics
  hypothesis, open: the def's 3-way `RANDOM_WEIGHT` roll (30/80/140°) may be rendered
  concurrently (all branches) by the original engine rather than pick-one — which would make the
  authored form itself a triad at those exact angles. Our triad uses 120° spacing with one
  continuous roll; a 30/80/140° triad is one constant away and could be A/B'd against
  `MuzzleFlash1-3.png` if the flash shape is ever revisited.
  ⚠ Trap: the pick-one single-quad reading (+ `_muzzle1`→`_muzzle2` flip) was implemented and
  rejected at the controls — do not re-land it without new footage evidence.

- `BL-289` `[Tuning]` `[Owed-playtest]` **Gun-impact looks (A2/`BL-203`, landed 2026-08-01)** —
  ⚠ **The six `DirtDebris*` constants left this entry: the dirt-chip effect they tuned was deleted
  2026-08-15 (`BL-313`, closed), so there is nothing to A/B.** Dirt now takes the single spark.
  What remains here is the building ricochet:
  `RicochetSparks` **8**, `RicochetSparkSize` **0.55 m**,
  `RicochetSparkLife` **0.55 s**, `RicochetSparkSpeed` **22 m/s**, `RicochetSpreadDeg` **90°** (a
  stand-in — both authored assets are missing from the install). The water-splash column width
  is settled and out of this entry: `SplashColumnWidthScale` **8×** confirmed at the controls
  2026-08-06 with the fades in (`BL-265` closed — the authored quad is 5 cm wide, sub-pixel past
  ~30 m; the reference ticks measure ~0.35 m, which 8× matches). A/B the rest against
  `Dirt Splash.png` at the controls; the splash *height/timing* curves are authored data, not TUNE.

- `BL-357` `[Feature]` **The hardpoint selector steps one way only; the original cycles in both
  directions.** *Evidence:* the user at the controls of the original, 2026-08-14: the player selects
  an individual hardpoint (the half that settled `BL-062`, closed the same day), and the selection
  can be stepped clockwise *and* counter-clockwise. Ours has exactly one selector input per weapon,
  `H` / D-pad Right for pylons and `G` / D-pad Left for gun groups
  (`FlightController.cs:1456`, `docs/controls.md`), and `WeaponCursor.NextSelectable`
  (`WeaponCursor.cs:34`) only ever scans forward. **Direct corroboration, `PLAN-targeting.md` A1
  (2026-08-16):** the original's own Weapons keybind page carries `Cycle guns clockwise` (`F3`) and
  `Cycle guns counterclockwise` (`F4`) as two separate actions, same for rockets (`F5`/`F6`) — the
  original has two selectors per weapon class where we have one, confirmed from the keybind page
  itself rather than from watching a play session. *Fix shape:* a `PrevSelectable` backward scan
  with the same empty-slot skipping, plus a second binding per selector, which is where this stops
  being a two-line change: the flight keymap has no spare paired keys and the pad's D-pad is
  already spent on the two forward steps. `BL-296`'s per-player ActionMap is the natural home for
  the four named actions if it lands first.
  ⚠ Traps: (a) **The cycle order itself is right and must not be touched** (user, 2026-08-14: ours
  walks the pylons in the same order the original does). What is missing is the second direction,
  nothing else, so a reverse step is `NextSelectable` walked backwards over the same sequence, not
  a re-derivation of the order. (b) The observation is about hardpoints. The gun-group selector is
  the analogous case but was not observed, so do not assume it cycles both ways either.
  (c) Empty-slot skipping is not in question and must survive the change: both directions land on
  an armed slot.
  *Cross-refs:* the hangar's custom loadouts (`docs/plans/PLAN-hangar.md`, landed) are where
  mixed fits make the direction matter, so this item's value went up when that shipped;
  `BL-296` (ActionMap/rebinding seam), `git log --grep=BL-062` for what settled the
  per-hardpoint half.

- `BL-363` `[Bug]` **Only aircraft are AI targeting candidates, so an escort with no enemy planes
  has nothing to do.** *Evidence:* the user at the controls, 2026-08-15, flying a zeppelin run
  configured with no enemy planes: the wingmen never engaged the zeppelin and flew straight away.
  `FlightController.SelectRankedTarget` (`FlightController.cs:2233`) builds its candidate list from
  `Projectiles.CollectAircraft` alone and then drops any candidate whose source is not a
  `FlightController`. A zeppelin is a kinematic world node owned by `ZeppelinRuntime` (F17), never a
  `FlightController`, so it cannot be ranked, cannot resolve as a `primary_target`, and cannot put
  an AI into pursue. The same holds for every world destructible and turret emplacement. The decoded
  ranking already expects these candidates: `AiTargetRanking`'s own module doc names the "+0.4
  dynamics / −0.5 structure terms unmodelled (no non-aircraft candidates reach this path)", which is
  exactly this gap.
  *Fix shape:* widen the collector to the roster the aim assist already scans (it lists vehicles and
  turrets, not just aircraft), keeping the `Live` and team gates as they are, then wire the two
  unmodelled class terms in `AiTargetRanking`.
  ⚠ *Traps.* (a) **The ranking is MINIMISED**, so a large nearby structure can outrank a distant
  fighter for every pilot at once. Settle the structure term's sign and scale before widening the
  pool, or one zeppelin becomes the whole sky's target. (b) `AiGunner` solves its intercept from the
  target's `WorldVelocity` and gates on an aircraft-sized cone; a zeppelin also needs
  `DAMAGES_ZEPPELIN` ordnance (F18) or every round is refused, so a wingman with the stock fit would
  fly a pursuit it can never convert. (c) **Attacking the objective is not automatic in the
  original**: it is assigned through `primary_target` and `rating_biases`. Widening the candidate
  pool must not turn every AI into a zeppelin attacker.
  ✔ **DECODED 2026-08-15**, in [`docs/org/aiPilot.md`](docs/org/aiPilot.md) "Target acquisition".
  The pool is four typed lists, not one: `TargetVehicle` (`DAT_0071dabc`), `TargetTurret`
  (`DAT_0071d914`), `TargetStruct` (`DAT_0071d33c`) and `TargetProjectile` (`DAT_0064f78c`), swept
  by `FUN_0041f9c0` for one global minimum. Both unmodelled terms are named: **+0.4** is a candidate
  whose `mode` (`+0x67c`) is 4, a `wingman`, so enemy wingmen are de-prioritised by 480 m; **−0.5**
  is a **zeppelin gasbag** and nothing else, worth 600 m in its favour, reached through the `Target`
  virtual at vtable `+0x1c` (`0x004227a0`, the same one the overlay prints `Gasbag targeted` from).
  Trap (a) is smaller than feared and trap (b) is answered at admission: the struct list is always
  swept (the literal `1` at `0x00420002`), and its gasbag members are admitted only for a pilot
  carrying loaded `DAMAGES_ZEPPELIN` ordnance (`FUN_00420070`, flag bit `0x1000`), so a stock fit is
  never offered a gasbag. **Three constants we already ship are wrong** and are independent of the
  widening: `AiTargetRanking.BiasScale` is `+1200` where `FUN_0041ae40` returns `bias × −750` with
  `≤ −1.0` a hard exclusion, `≥ 1.0` = `−100000` and a flat `+37.5` on turrets; the bearing term's
  sign is inverted and its ±0.5 is a half-metre ahead/behind deadband, not `cos 60°`; and the ±0.2
  terms are aircraft-only (`FUN_00421950` has none). Acquisition also has a sticky standing target
  and a real attacker count our invented deconfliction does not match.
  *Cross-refs:* `BL-362` (the wingmen half of the same playtest), `AiTargetRanking`,
  [`docs/formats/ai-rosters.md`](docs/formats/ai-rosters.md) "AI modes, engine-side" (whose "+0.4
  for one dynamics class" and "−0.5 for one structure case" are now named, and whose "dynamics" is
  the `mode` field), [`docs/org/aiPilot.md`](docs/org/aiPilot.md) (the decode).

- `BL-364` `[Bug]` `[Blocked: campaign missions]` **Our AI aircraft have no patrol net, and the original gives
  every one of them one. DECODED and the Instant Action half LANDED 2026-08-15; what is left is
  the campaign roster path, which has no spawner to plumb into yet.** *Evidence:* the user at the controls,
  2026-08-15: the default mode of enemy AI is to fly straight in one direction, and an enemy wave
  out of engagement range never turns back. `AiPilot.SteerPatrol` returns immediately when `Patrol`
  is null (`AiPilot.cs:266`), leaving `TargetHeadingDeg`/`TargetAltitude` at whatever
  `HoldingCourse` set at spawn.
  **The research half is answered, in [`docs/org/aiPilot.md`](docs/org/aiPilot.md).** The engine has
  no netless patrol at all: `FUN_0041d1f0`, the net follower, resolves the net unconditionally and
  has no fallback branch. A netless aircraft is instead a `mode wingman` aircraft flying a fixed
  formation station on its `primary_target` (`FUN_0041e760`), and a `wingman` that is given a net is
  demoted to `jet` at spawn. Two censuses settle who is which: of 414 shipped `aiv` blocks, the only
  106 netless ones are `player`, `wingman_N` and `bswingman_N`, and every enemy, boat and truck
  carries a real net id; of 75 `vehicle.json` defs, only 12 author `mode wingman` and every enemy
  resolves `jet` through `basic_airplane`.
  ⚠ **The entry's own premise was wrong, and so was the doc it rested on.** Instant Action does
  NOT leave `netids` at `-1`: all three branches of `FUN_0045a390` write a one-entry list holding
  the chapter's first net id (`0x0045a8b4` / `0x0045ab18` / `0x0045ae85`), so in the original the
  ace, the waves and the wingmen all walk that graph.
  [`docs/formats/instant-action.md`](docs/formats/instant-action.md) is corrected.
  *Fix shape:* give AI aircraft a patrol net, which is `AiNets` data we already parse
  (`src/Mech3/AiNets.cs`) plumbed into `AiPilot.Patrol` at spawn. Instant Action actors take the
  chapter's first net, exactly as the original does. Campaign rosters take their authored `netids`.
  ✔ **The Instant Action half landed 2026-08-15** (`git log --grep=BL-364`): the ace, the wingmen
  and every wave member take the chapter's first net, `AiNets.ChapterFirst` reads it in `neindex`
  FILE order (**not** ascending id: C1B opens on 29 and C1C on 25 against a lowest of 11, decoded
  from `FUN_004311c0` and pinned in `AiNetsTests`), `FlightController.Activate` re-seats the walk
  the way the original's activation snap does, and `SteerPatrol` re-asserts `PatrolThrottle` so a
  plane leaving pursue does not patrol at the chase throttle. Two more decodes are in
  [`docs/org/aiPilot.md`](docs/org/aiPilot.md): the roster block's volumes are copied over the
  net's afterwards (so trap (d) below cannot bite an Instant Action actor, whose block authors all
  nine at ±10000 m), and `min_ai_active_dist` (2000 m, `player.zrd.json`) floors every activation
  volume twice over. **What is left:** a campaign roster spawner reading each block's authored
  `netids`. Nothing in `src/` reads `aiv` as a spawn roster today, so there is no seam to plumb.
  ⚠ **The net Instant Action hands out is a campaign MISSION's asset, not a patrol area meant for
  free play** (censused 2026-08-15, at the user's prompting after seeing the shapes at the
  controls). Net names are mission-scoped and the census bears the convention out: 103 of the 222
  nets are referenced by an `aiv` block and every one is used by the single mission its `M<N>`
  prefix names. Each chapter's first net is then one mission's: C1 `M4ReinfAce` is M04's
  `blakebloodhawk_8`, C1B `Patrolboat3` is M03's objectives, **C2B `PirateZep1` is the pirate
  zeppelin's own flight path**, C5 `M1Bravo` is used by nothing at all. Faithful, not a bug, but it
  is why an Instant Action flight walks an odd-looking graph; see
  [`docs/formats/instant-action.md`](docs/formats/instant-action.md) for the per-chapter table.
  ⚠ `--ai=<plane>` without a net ref still spawns a course-holder; that is the debug flag's own
  documented behaviour, kept deliberately, not a leftover of this item.
  ✔ **Flown 2026-08-15 and the landed half PASSES** (`PT-51`, now retired). A wave that loses the
  player turns and comes back rather than shrinking to a dot; an enemy breaking off an engagement
  settles back onto the graph instead of orbiting one waypoint, so trap (a)'s known failure did not
  appear; wave 2 patrols from where it teleports in; and a shared net reads as a busy patrol rather
  than a conga line. So what remains here is only the campaign roster spawner.
  ⚠ *Traps.* (a) **`AiPilot.PatrolThrottle` (0.5) is an invention that exists because the
  placeholder steering law cannot hold the tightest net rings at cruise** (`architecture.md`). Do
  not read a net-follow regression as a net-data problem before checking it. (b) `pref_engage_alt`
  is decoded and is **not** an altitude order: its one reader weights the evasive-maneuver draw
  (`FUN_004201a0`). Nothing steers toward it, so do not build an altitude hold on it. (c) The
  engagement gate was 2000 m until the 10000 m volume fix (`git log --grep=ApplyActorVolumes`),
  which is part of why waves read as flying away. (d) A net assignment also **overwrites the
  vehicle's three volumes** from the net's own where the net authors non-zero values
  (`FUN_00475fc0`), which interacts with that fix and must not be dropped.
  *Cross-refs:* [`docs/org/aiPilot.md`](docs/org/aiPilot.md) (the decode), `BL-362` (the wingman
  half, whose blocker this decode voids), `docs/formats/ai-nets.md`, `docs/architecture.md` on
  `AiPilot` and `AiModeMachine`.

- `BL-398` `[Feature]` **A rebindable keymap — the real answer to targeting's key placement, not a
  targeting-specific fix.** *Evidence:* the player-targeting plan's own out-of-scope call (a),
  2026-08-15: every one of the original's eleven targeting keys collides with our WASD +
  `Shift`/`Ctrl`-throttle flight scheme — the full collision audit is in `docs/org/targeting.md` —
  so that plan ships a curated default set on free keys (`T` `Y` `U` `I` `O` + `L`, `D-pad Up`
  tap/hold) instead of mirroring the original's letter-per-class layout (`E`/`W`/`R` plain/
  `Shift`/`Ctrl`). A rebind layer is what actually resolves key placement; targeting is only the
  feature that hit the wall hardest, because it is eleven keys deep into an already-full keymap.
  *Fix shape:* `BL-296`'s per-player `ActionMap` seam, if it lands first.
  *Cross-refs:* `BL-296`, `docs/org/targeting.md`, `docs/controls.md`.

- `BL-399` `[Feature]` **Track Target's camera behaviour — `L` is reserved, the camera itself is
  undecided.** *Evidence:* the player-targeting plan's out-of-scope call (b), 2026-08-15: the
  original's `Views 1 → Track Target` binds `L` (free in our flight keymap; our `L` is the
  viewer-only livery lab), decoded in `docs/org/targeting.md`, but "keep the target framed" hides a
  pile of camera decisions that plan deliberately deferred: snap vs smooth follow, override vs
  blend with the chase camera, behaviour with no target selected or a target behind the pilot, and
  interaction with the right-stick free look (`BL-372`). `L` is reserved in `docs/controls.md` but
  bound to nothing. `PLAN-cockpit-view.md`'s head-look decode names the mechanism this camera would
  ride: the look-state byte the controller reads (`DAT_0064ef68`) has a third value, `2`, for
  padlock, sitting beside the `0`/`1` snap/free-look states `BL-432`'s selector keys pick between —
  so `L`'s camera is this same state machine's third mode, not a bolt-on. Building it needs
  `TargetSelection.Current` plumbed into `HeadLook`'s target so the padlock state aims the head at
  the current target instead of reading player input.
  *Fix shape:* a camera-focused item once the questions above are settled — not a change to the
  targeting module itself, which already exposes `TargetSelection.Current` cleanly for a camera to
  read.
  *Cross-refs:* `BL-372` (right-stick free look), `BL-432` (the `K`/`J` mode selectors, the byte's
  other two states), `docs/org/targeting.md` "Track Target", `docs/controls.md`,
  `PLAN-cockpit-view.md` (`HeadLook`, `src/Flight/HeadLook.cs`).

- `BL-400` `[Feature]` **`Structures` as a selectable Non-Aircraft target — needs a curated
  `targets.zrd`-equivalent list.** *Evidence:* the player-targeting plan's out-of-scope call (c),
  2026-08-15: the original's Non-Aircraft cycle walks a curated mission `targets.zrd` list
  (decoded in `docs/org/targeting.md`), which our `DestructibleRegistry` has no equivalent of —
  `docs/architecture.md`'s own `DestructibleRegistry` entry calls it an **approximation** of that
  list, and walking it directly would put every crate and fence in the world on the cycle.
  `TargetPool.Rebuild` deliberately never reads `AimCandidateSet.Structures` for exactly this
  reason, so `Structures` staying out of the cycle is a property of the pool, not a gap that leaked
  in.
  *Fix shape:* a curated per-mission target list (mirroring `targets.zrd`'s authored entries) feeding
  `TargetPool` the same way zeppelin sub-parts do today (`subParts` in `Rebuild`).
  *Cross-refs:* `TargetPool.cs`, `docs/architecture.md`'s `DestructibleRegistry` entry,
  `docs/org/targeting.md`.

- `BL-397` `[Feature]` **A modernized target marker: brackets only PAST range, not under it — the
  deliberate INVERSE of the original's own rule.** *Evidence:* the user's preferred rule (brackets
  only past 500 m) was the player-targeting plan's original premise and was disproven in the
  2026-08-15 grilling: the original draws brackets only UNDER the selected gun's reach
  (`TargetHud.GunReaches`) — distant enemies get none, and near ones get brackets the silhouette
  often swallows. The user's ask is the exact inverse of that, not a memory of it. The shipped
  marker uses the original's rule as fidelity; this item is the later, separate call to add the
  modernization as an opt-in or a replacement.
  ⚠ *Trap:* do not "fix" `GunReaches`'/`TargetHud`'s gate to match this without checking this
  entry first — the two rules are opposites BY DESIGN, not an oversight left behind.
  *Fix shape:* a flag or setting flipping the gate's sense once the product call is made (past range
  = bracketed, inside = not), reusing the same hysteresis machinery already built.
  *Cross-refs:* `TargetHud.GunReaches`.

- `BL-404` `[Research]` **Does the player's rocket get an aim component in the original, the way
  the player's guns get the assist?** *Evidence:* our rocket launch spawns from the pylon marker's
  transform with no aim direction at all (`FlightController.cs:1860-1865`), on the stated ground
  that the original's aim assist `FUN_004b6530` is reached from the gun branch alone. That claim
  was made from the assist's call sites, not from reading the ordnance branch of the shot routine
  `FUN_004b6820` end to end, so it settles where the *assist* is called and not what direction an
  ordnance round actually leaves along. The decoded mount model gives a concrete reason to doubt
  it: an AI's round leaves along the mount's clamped aim, which tracks the lead to within the
  5° gate, and the mount machinery (`FUN_004b7670`, `FUN_0041afe0`) is not AI-only.
  *What to settle:* (a) whether `FUN_004b6820`'s ordnance path hands the projectile spawner the
  mount's aim `+0x48`–`+0x50` or the vehicle's forward axis, and whether that differs for the
  player; (b) whether any assist or lead solve runs for a player rocket, including the
  `FUN_00440ad0` muzzle-position branch the decode leaves unread; (c) whether the player's own
  ordnance skips the aim gate entirely the way it skips the `quick_draw_chance` roll
  (`0x004b6b41`).
  ✅ **ANSWERED, all three parts** ([`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md), "Who
  aims ordnance, and who does not"). **(a)** The player's ordnance branch hands `FUN_005aef40` a
  direction built from the **aircraft's own basis axis**, negated (taken as-is for a `REAR` weapon);
  the mount contributes the spawn position only, and its aim is never read. The AI's branch instead
  passes mount `+0x3c`, which `FUN_004b7670` writes at the end of every mount update as the clamped
  aim `+0x48`–`+0x50` rotated into world space, so an AI's round does leave along a target-tracking
  direction. **(b)** No. The assist `FUN_004b6530` is reached from the `CANNON` branch only; both
  ordnance branches bypass it, for the player and the AI alike. **(c)** The player skips the aim gate
  outright, `FUN_004b6820` jumping the whole block when the shooter is the player; when it does run
  the threshold is cos 5° for ordnance against cos 10° for guns.
  ⚠ **The residual this raised is itself settled.** "No aim on rockets" is confirmed for the player,
  so the shipped assumption holds in kind; the launch axis it left open is now
  `FlightController.OrdnanceLaunchDir`, a human's round taking the aircraft's basis axis and an AI's
  the clamped mount aim (`AiRocketeer.LaunchDirWorld`). That asymmetry is the original's, not a
  decision of ours, and no shipped airframe cants a pylon marker, so it changes nothing a player
  sees on the shipped fit.
  *Cross-refs:* `AiRocketeer` (the AI ordnance trigger),
  [`docs/org/aiPilot/aiWeapons.md`](docs/org/aiPilot/aiWeapons.md) ("The fire routine, and the aim
  gate", and its "Open" note on the muzzle-position branch),
  [`docs/org/aim-assist.md`](docs/org/aim-assist.md).

- `BL-411` `[Feature]` **Force feedback is unimplemented, and it is the only thing the `TORPEDO` flag
  does.** *Evidence:* `FUN_00480f50` drives the Immersion TouchSense API (`CImmCompoundEffect`) and
  picks one of three launch effects, gated on `ROCKET`: `TORPEDO` gets direction 0 at magnitude 1.0,
  a `REAR` weapon 180 at 0.58, ordinary ordnance 0 at 0.79
  ([`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md)). Nothing in `CSVM/src` mentions force
  feedback, vibration or haptics.
  *Fix shape:* Godot exposes `Input.StartJoyVibration(device, weak, strong, duration)`.
  ⚠ *Traps:* (a) **Godot's API has no direction**, so the original's 0°/180° split cannot be
  reproduced; a rear weapon's kick-from-behind would be lost and the result is a partial
  reproduction that reads as complete. Decide that explicitly before building. (b) Do not substitute
  a camera shake: the original's launch shake is **guns-only**, sized by `CALIBER`, so an ordnance
  shake is content the game never had. (c) Survey the other `CImmCompoundEffect` carriers before
  scoping; ordnance launch is unlikely to be the only one.
  *Cross-refs:* `BL-406` (closed; the ordnance plan excluded `TORPEDO` for this reason).

- `BL-412` `[Research]` **What does `CRATER` do? Six weapons author it and it drives a whole
  terrain-deformation subsystem.** *Evidence:* the flag sets weapon `+0x74` bit `0x2000` and parses a
  sub-block into `+0x194` (`FUN_005ad630` at `0x005ada98`); the effect runtime reads the same
  `CRATER` block at `FUN_004e5590`. The binary carries `D:\zipper\gamez\zdeclient\zdec_crater.cpp`
  with three distinct failure strings ("Tesselation Failed", "Clip Failed", "Build Failed"), a
  `Crater%d` instance name, an `OnCrater` hook and a `MAX_CRATER_RADIUS` key. That is mesh carving,
  not a decal. Carriers: `wep_04`, `wep_12`, `wep_25`, `wep_26`, `wep_27`, `wep_28`.
  *Fix shape:* decode `FUN_004e5590` and the `zdec_crater` routines into `docs/org/craters.md`:
  what the sub-block authors, what `MAX_CRATER_RADIUS` bounds, whether the carve is persistent or
  pooled, and what happens on the three failure paths.
  ⚠ *Trap:* `+0x74` bit `0x2000` is **not** the extension struct's `0x2000` (`SHAKES_CAMERA`). The
  two flag words are unrelated bit spaces.
  *Cross-refs:* `BL-413` (the implementation), `BL-406` (closed; it excluded this).

- `BL-413` `[Feature]` `[Blocked: BL-412]` **Ground-attack ordnance leaves no crater.** *Evidence:*
  six weapons author `CRATER` and the original carves terrain geometry for it; we do nothing. Blocked
  on `BL-412` because the mechanism is unread, so neither the size nor the approach can be stated
  yet.
  ⚠ *Trap:* this is a **terrain and renderer** change triggered by ordnance, not an ordnance change.
  Scope it against the terrain system's constraints (chunking, LOD, the golden manifest's mesh
  counts), not against the weapon table.
  *Cross-refs:* `BL-412`, `BL-406` (closed).

- `BL-405` `[Fidelity]` **Mounted ordnance should track the aim before it launches, not hang fixed
  along the pylon.** *Evidence:* the mount model is decoded
  ([`docs/org/aiPilot/aiWeapons.md`](docs/org/aiPilot/aiWeapons.md), "`gun_pitch`/`gun_yaw` clamp
  the mount"): `FUN_004b7670` rotates the desired lead into the vehicle frame, clamps each axis
  into its authored band and writes the result as the mount's actual aim (`+0x48`–`+0x50`), and a
  mount carrying an animated node (`+0x34`/`+0x38`) slews toward that direction through
  `FUN_00460840` instead of snapping to it. Our pylons do not move: `PylonOrdnance` parents the
  body to the pylon marker at identity and never touches it again (`PylonOrdnance.cs:46-49`).
  An AI round leaves along a launch direction up to the traverse limit off the pylon axis
  (`AiRocketeer.LaunchDirWorld`), so the mounted body and the round it becomes point different
  ways at the launch instant, which the mounting comment's "seamless" claim no longer covers.
  ⚠ *Settle the data question first.* The slewing mechanism is decoded; whether any shipped
  aircraft authors an animated node on the mount its ordnance hangs from is **not**. A fixed
  forward gun has no node and reaches the clamped direction the same frame, and if the ordnance
  mounts are the same, the original's rocket body does not visibly track either and this item is
  closed by the census rather than by code.
  *Fix shape (only if the census says yes):* the pylon marker takes the clamped direction the fire
  decision already computes, with the mounted body riding it as it does today. Ours would snap
  where the original slews unless `FUN_00460840`'s rate is read too.
  *Size:* localized, and probably closed as no-change.
  *Cross-refs:* `AiRocketeer` (whose launch direction creates the mismatch), `BL-404` (whether the
  player's rocket gets a direction at all), `docs/formats/vehicle.md` (`gun_pitch`/`gun_yaw`).

- `BL-401` `[Bug]` **The node names we spawn do not match the names the rosters author, so
  `rating_biases` matches nothing.** *Evidence:* `ObjectiveBiasFor(fc.Name, gunner.RatingBiases)`
  (`FlightController.cs:2427`) matches an authored pattern against the candidate's Godot node name.
  Those names are ours, not the mission's: `AiAircraftSpawner.cs:140` names an AI plane
  `ai{n}_{plane}` (`ai1_player_fury`) and `FlightRigAssembler.cs:421` names a human rig
  `player{n}` (`player1`). The shipped patterns are mission node names — a census of all 53
  `aiv.zrd.json` (414 blocks, 697 entries) gives `hafury*`, `bswingman_1`, `devastator_1`,
  `medkestrel_5`, `piratezep`, `fuel_truck*`, `aagun*` and the bare `player`. `AiRatingBias.Matches`
  treats `*` as the only wildcard, so `player` does not match `player1`, and `hafury*` does not
  match `ai1_player_fury`. The term is therefore decoded correctly and firing on almost nothing.
  The `player` case is the sharpest: it is authored 157 times and never negative (40 of them at
  exactly `1.0`, the always-target saturation), so the pilots most explicitly told to come after
  the player get no bias at all.
  *Fix shape:* decide what identity the bias is supposed to match, then make one side produce it.
  Either resolve the authored pattern to a spawned plane the way `PrimaryTargetName` already does,
  or carry the roster's own block name onto the spawned `FlightController` beside its Godot name
  and match on that. Prefer the second: it makes `--target=`, the breadcrumbs and the biases agree
  on one identity string instead of three.
  ⚠ *Traps.* (a) **`PrimaryTargetName` already special-cases this and the bias path does not** —
  `FlightController.cs:2395` matches the name exactly, then `:2399` falls back to
  `IsHumanPiloted` for `"player"` (`AiGunner.cs:34`). That asymmetry is the bug, so do not "fix"
  it by copying the human-piloted fallback into `ObjectiveBiasFor`: it would paper over the
  general naming problem while leaving every non-`player` pattern broken. (b) Renaming the spawned
  nodes is not the fix. `player` is `EffectCatalogue.CrashAnimRoot` and the crash-scaffold anchor,
  and the `ai{n}_` prefix is what `TargetHud.HostileTag` reads for the marker tag. (c) Only
  aircraft are candidates today (`BL-363`), so the ground and zeppelin patterns cannot match
  regardless of naming; fixing names alone will not make `fuel_truck*` reachable. (d) First match
  wins per block, so a pattern's position matters once names do resolve — do not sort them.
  *Playtest after fix:* an Instant Action wave whose roster authors `["player", 1.0]`; the pilot
  carrying it should come for the player over a nearer AI, and the `target rank` breadcrumb should
  show the `-100000` saturation rather than `0`.
  *Cross-refs:* `BL-363` (the candidate pool is aircraft-only, the other half of why the biases do
  nothing), [`docs/formats/ai-rosters.md`](docs/formats/ai-rosters.md) (slot 33 and the
  `bias × −750` decode), `AiTargetRanking.ObjectiveBiasFor`, `AiSkills.RosterRatingBiases`.

## Flight model & collision physics

- `BL-443` `[Fidelity]` **The G ramp reads the same tick's delivered lift; CSVM's is one step
  late.** `FUN_0048fc40` (call `0x48c883`) writes the delivered body-up G and the ramp reads it at
  `0x48ca1e` in the same tick, before the torques; `FlightModel.Step` rotates before it translates
  and reads the previous step's. Porting is the force-from-entering-attitude order of `Step`, which
  moves every envelope row, so it needs its own eleven-airframe `--dump-flight=all` A/B with each
  moved row attributed. Bounded: the ramp bites near `highGs` (9) and the stock full pull peaks at
  5.83 G. Ledger row "the G ramp reads the SAME tick's delivered lift" in
  [`docs/org/flightModel.md`](docs/org/flightModel.md).
- `BL-445` `[Tooling]` **Dump scenarios for the plant branches the envelope dump never enters.**
  The dump drives 7 of 16 decoded branches (8 on the autogyro). Reachable but unentered:
  `pitch-fade`, `g-clamp`, `g-ramp` (a sustained outside push), `dive-cap`, `stall` and the
  low-speed authority ramp (a slow-flight decay). Each already has a unit instrument; the branch
  coverage line in the ledger names it. Add scenarios only if whole-envelope coverage is wanted;
  do not invent a manoeuvre to raise the number.
- `BL-447` `[Fidelity]` **The eight unsupported nitro and shake edges in the parity ledger.**
  `docs/org/flightModel.md` "Parity ledger", class unsupported, beyond `BL-443`: the thin
  atmosphere band above 2000 m, the `level_off_rate` auto-level torque, the AI's `medium_aishake`
  on a nitro engage, the AI's positional `snd_nitro` blip, the nitro decay lockout on a runtime
  callback, the mouse-flying arm's `is_autogyro` roll/yaw exchange, plus `BL-448` and `BL-450`.
  Each is small and independently landable; each names its address in the table.
- `BL-448` `[Research]` **Is the 2003 m `AltitudeCapM` the dense-band edge?** The measured
  flight ceiling (an intentional exception) sits 3 m above the decoded atmosphere band boundary
  (2000 m, `6561.6796875` ft, writer `FUN_00463640`). If the original's ceiling is the thin band's
  own lift loss rather than a separate cap, the exception becomes a decoded mechanism and the cap
  constant goes. Lead recorded in the plan's A1 section; `AtmosphereBandTests` has the band.
- `BL-450` `[Feature]` **Fuel burn and the empty-tank lever freeze.** `FUN_0048e580` burns
  `[obj+0x134] −= dt · throttle · 5` (player-only, `0x48e603`), and a zero tank jumps past the
  throttle slew (`0x48e5f7` to `0x48e6c9`), freezing the lever where it stands rather than closing
  it. No fuel model exists here; the shipped missions never run a tank dry, so this matters only
  for a long-flight mode. Nitro burns no fuel (`0x48e603` reads the lever, not the boost flag).
- `BL-451` `[Research]` **A dead AI's throttle.** The death function `FUN_004b82d0` zeroes neither
  the throttle command nor the control surfaces; they are AI-written state and the AI think is
  what stops, so they freeze at their last commanded values. CSVM's three-second dead-hull flight
  should freeze the same way; check what `AircraftLifecycle`'s handover leaves in the lever and
  whether the recovery arm keeps writing it.
- `BL-452` `[Docs]` **`BL-266` carries guessed shake constants the decode has since replaced.**
  The plan's D34 shake pass (`docs/org/shakes.md`, `docs/formats/shakes.md`) traced the block-5 kick
  and the impact sources; `BL-266`'s (b) and (d) text still quotes the pre-decode readings. Rewrite
  those sub-items against the decoded numbers rather than leaving both versions live.
- `BL-453` `[Feature]` **The mission spawner does not read roster blocks.** `AiSpawn.Nitro` reads
  roster slot 34 (`0x475c9a`, three shipped rosters author it) but the mission spawner never
  fills it, so an AI nitro injector has no live producer (ledger row, unsupported). Read the roster
  block at spawn; check which other roster slots the spawner drops on the same path.
- `BL-454` `[Owed-playtest]` **Nitro dial sweep against the original.** `NitroGaugeNeedleTests`
  pins the needle law, but nobody has put the moving dial beside a screenshot of the original's.
  One screenshot of each at full, half and empty tank.
- `BL-456` `[Research]` **Trace the writers of the crashed flag `[obj+0x384]`.** Its readers are
  decoded (`0x48c4ba` selects the far-field arm, `0x48cd4a`, `0x48dfbe` gives a crashed hull
  severity and no impulse); its writers `FUN_0043d640`, `FUN_004735b0`, `FUN_004aff80` are not,
  so the ledger keeps "a wreck flies the near-field plant" as an exception. Decode when and by
  whom it is set so the wreck can fly the decoded arm.

## Environment & world

- `BL-037` `[Feature]` {CAMPAIGN} **`WorldPartitionSetActive` is decoded and unimplemented — `NodeSetActive`
  selected by area.** **Decoded 2026-08-09** from `crimson.exe` in Ghidra and written up in
  [`docs/formats/interp.md`](docs/formats/interp.md) § "`WorldPartitionSetActive` — `NodeSetActive`,
  selected by area" (dispatch `FUN_005b80a0`, rectangle walk `FUN_004db790`, shared toggle
  `FUN_004cca30` = the authored `gwNodeSetActive`). The verb walks the partition grid over its
  rectangle and calls **the same toggle `NodeSetActive` calls** — bit `0x4` of the node's flag
  word — so there is no second visibility system to build; the research question is answered and
  what is left is the decision to implement or drop. ⚠ It is NOT the C5 ground-LOD mechanism —
  that is the **subface flag** (`analysis/item9-depth-bias/CBLOCK-LOD.md`; the clutter side closed
  as `BL-250`, `git log --grep=BL-250`) — and not "the runtime system that picks between coarse
  and fine ground", a claim this entry once made and retracted (⛔ 2026-07-23), and which
  `docs/HISTORY.md`'s M2 polish-4 entry still restates.
  *Scope if built:* all 25 uses are `support\c3\*.gw` and resolve to three distinct rectangles;
  every `off` sits in a story mission, so **IA1 is unaffected in all eight chapters** and no golden
  can see it. The one real code change is in `GameZ.cs`, which parses the World `partitions` array
  but dedups it into one flat `PartitionNodes` list, discarding the per-cell membership a rectangle
  query needs (`MapEdgeExtender` already maps a world position to a cell). ⚠ Two traps recorded in
  the doc: corner order is normalised by the engine, and the rectangle is **half-open** in cell
  space — an inclusive test over-selects by one row and one column.
  *Needs:* a decision. Verifying it means a C3 story-mission A/B against the original, since
  Instant Action cannot show it.

- `BL-038` `[Feature]` {CAMPAIGN} **`FogState` is a decoded animation event we do not act on** (found 2026-07-22). Fog **can** be
  changed mid-mission by animation, but the data uses it exactly once install-wide:
  `extracted/C1/M04/mis_anim/camera1-mission_intro_animation.json`, `reset_state/events[4]` —
  `FogState { name: "drop_fog", color 0.69/0.69/0.69, altitude 10000–11000, range 1000–1500 }`.
  It carries fog parameters **inline** and matches neither C1 zone, so it is an ad-hoc third fog
  state on the intro cutscene camera, not a zone selector. Relevant because it is the only
  evidence that weather is scriptable at all; zone *selection* still appears to happen engine-side
  in the binary (same shape as the `fire2` trigger the user searched the disassembly for).
  **Documented 2026-07-22** (polish-3 item 2) in `docs/formats/anim-definitions.md` as
  decoded-but-unacted-on, with the reason: implementing it means a second write path onto the
  `csky_fog_*` globals `PlaneViewer.SetupWeather` owns, for one cutscene the remake does not run.
  Revisit if the user ever sees fog visibly change *during* a mission elsewhere.

- `BL-070` `[Bug]` **C5's `poleflare` clutter renders with the wrong billboard axis** (one of two residuals
  from polish-3 item 5, 2026-07-22; the other — the static collider probe's off-by-6/11 — closed
  2026-08-04, `docs/HISTORY.md` "M3 polish-6 C22", with a rewritten probe now committed at
  `analysis/collider-probe/`). The `cblock*` templates ship `lightpole` (`CylindricalY`) posts
  *and* `poleflare` (`SphericalY`) glows — 33,682 of each in `cblock1` alone. `ClutterBuilder.Kind`
  carries no per-kind billboard mode, so every kind goes through the one Y-axis shader: the glows
  spin upright instead of facing the camera, and they get the SUNLIGHT night dim a light source
  should be exempt from. Now *detectable* (the shared `SceneBuilder.ClassifyBillboard` distinguishes
  the two), but fixing it means giving `Kind` a billboard mode and a second material path, and it
  changes how 139,388 C5 sprites look with no reference shot to check against — so it needs an
  original-game A/B.

- `BL-076` `[Feature]` **Star twinkle + undecoded light fields** (flags 523/…, the 0.17 float) — stars/beacons
  render as fixed-size soft sprites, no twinkle.

- `BL-331` `[Feature]` **Aircraft cast no ground shadow; the original draws one, straight down**
  (split out of `BL-324`, 2026-08-09). **Fully decoded 2026-08-13, in
  [`docs/org/shadows.md`](docs/org/shadows.md).** The original rasterises each aircraft's
  silhouette into a live 32×32 modulate texture, projected onto the terrain along `SHADOW_ANGLES`
  (`[-90, 0, 0]`, straight down, in all 53 shipped files), and draws it as a ground quad. What is
  left is building it, and the A/B below.
  ⚠ **Godot shadow mapping cannot be the mechanism**, and the decode confirms it: the original
  casts no shadow map at all. The world is built `fullbright: true` and an unshaded material
  receives nothing, so `BL-324`'s removal of `_sun.ShadowEnabled` (landed 2026-08-09,
  `git log --grep=BL-324`) stays correct and is not a regression to rediscover.
  ⚠ **The authored `shadow` node is the SOFTWARE fallback, not the real shadow** (settles this
  entry's former "check the authored mesh first" caveat). It is real and it is used, but only when
  the projected path is off: `FUN_00565aa0` enables that path for the hardware renderer only, and
  `FUN_004b3050` hides the card whenever it is on. Keeping `shadow` out of `PlaneBuilder.cs:26`
  is right; do not build the card.
  *Decoded, all of what this entry once listed as open:* the size law (footprint = the projected
  bounding box, scaled 1× → **3× between 60 and 250 units for the player's own aircraft only**);
  the distance fade (**horizontal** squared distance, `(far²−d²)/(far²−near²)`, near/far 1/200 for
  aircraft and 300/600 for the Spruce Goose, skipped for the player); the altitude ramp
  (`(250−alt)/190`, full below 60, gone at 250; 180/750 for the Spruce Goose); and the alpha terms
  (`255·((1−A) + 0.8·A·k)` per channel, `k = ambient/(ambient + diffuse·|dir.y|)`, so **shadow
  darkness follows the mission's `SUNLIGHT_AMBIENT`/`SUNLIGHT_DIFFUSE`**, the `BL-332` pair).
  *Needs:* an original-game A/B, a low pass over flat ground showing size and softness against
  altitude. The decode gives it four falsifiable predictions (`docs/org/shadows.md`, last section);
  the one to shoot at first is that the **player's own** shadow trails the aircraft by ~1.5 × its
  altitude along the flight direction while an AI aircraft's sits directly beneath it.

- `BL-332` `[Bug]` **The aircraft light's intensity is hardcoded; the mission authors it**
  (split out of `BL-324`, 2026-08-09). `Launcher.cs` sets `LightEnergy 1.6` and
  `AmbientLightEnergy 0.9` (ambient source `Sky`) once at launcher level, for every mission.
  The original sets `SUNLIGHT_DIFFUSE` and `SUNLIGHT_AMBIENT` **on the same `sunlight` node in the
  same zone-apply call** as the orientation — `FUN_00472ea0` calls `FUN_004dbdb0` (diffuse) and
  `FUN_004dbce0` (ambient) directly beside `FUN_004dc610` (the rotation setter), on the named
  `sunlight` gamez Light node every chapter ships. They swing hard: `DIFFUSE` 0.4–2.0,
  `AMBIENT` 0.15–0.6, with
  C1B night at `0.6 / 0.15` against C1C day at `2.0 / 0.6`. `Weather.cs` already parses both, but
  only collapses them into the `WorldLight` scalar for the **fullbright world**
  (`AMBIENT + DIFFUSE·SunIncidence`); the shaded path — aircraft, the only lit things in the scene
  — never sees them. So a plane in C1B's night mission is lit exactly as brightly as one in C1C's
  daylight, and since `BL-324` its bearing is now per-chapter while its brightness is not.
  ⚠ Not a straight port: `DIFFUSE` is a DX7-era intensity, not Godot's `LightEnergy` units, so
  mapping 0.4–2.0 onto the light is a **new calibration**, not a substitution — which is why it is
  not part of `BL-324`. Keep it to those two constants; whether Godot's ambient should stop being
  `Sky`-sourced is a separate rendering-design question, not this.
  *Needs an original-game A/B:* a matched night/day pose showing aircraft brightness, in the spirit
  of `CAP-11`'s `WorldLight` calibration (which pinned the world half of the same pair).

- `BL-272` `[Tuning]` **Precipitation: every unit mapping from `weather.json` to a look is invented, and
  one deviation is deliberately held back** (`Precipitation.cs:29-62` — type/tint/rate/density
  are authored; fall speed, box size, particle counts, streak length/width, sway are 16 TUNE
  constants; the sprites themselves are procedural stand-ins for the original's untextured
  line/point primitives, and rain streaking along fall-direction-vs-velocity is a documented
  deviation pending an A/B). Needs original rain and snow footage to calibrate — worth a CAP
  when weather work resumes.
  ⚠ One calibration fact is already on file (`CAP-11`, 2026-08-07, user): C2B IA1's rain falls
  below the cloud cover as **one-pixel-wide streaks** — narrow enough that the 2560-wide Game DVR
  capture swallows them entirely, while ours are plainly visible in the same scene
  (`playtest/CAP-11/csvm-c2b-low.png`). Streak width is the first constant to revisit.

- `BL-317` `[Research]` **The original renders ambient wisp puffs around the plane at all times —
  a third cloud population we don't have** (user at the controls of the original, 2026-08-08).
  Identified while re-reading CAP-12's climb: the "first wisps at ~982 m" are these plane-local
  puffs, not the `fvol` field appearing and not the whiteout ramp — "another disjunct feature."
  ⚠ Vocabulary: this is a THIRD population beside the `fvol` `cloudsprite` field and the
  world-placed `cloudparent` clusters (`BL-325` carries the full vocabulary note) — a claim about
  one is not evidence about the others. Note the deleted hand-tuned `CloudPuffs.cs` (removed by `BL-273`,
  2026-08-06) accidentally imitated exactly this; its constants survive in git history as a
  starting point, but the decode should come from footage: when they are visible, their size,
  count, and whether they move with the air or hang world-fixed. Needs a dedicated original
  capture at several altitudes in clear air away from the deck band.
  ⚠ **The original exclusion of authored puffers was wrong** (corrected 2026-08-10). The census
  filtered out `ON_CALL` events, but chapter-local `speed_cue.zrd` is an `ON_CALL` animation that
  player setup starts automatically and that loops every 0.1 s. Its three distance puffers attach
  at `player (0,0,-60)` — 60 m ahead — and use `smoke101`–`103`, 2.5–4.5 m size, 3–4 s life,
  transparent→low-alpha white→transparent colour, and 30/15/8 m intervals selected by camera
  altitude. Emitted particles stay in world space. C1 and C4 have the same geometry and timing;
  C4 raises the three midpoint alphas from 0.4/0.5/0.5 to 0.6/0.7/0.7. This exactly matches the
  user's new observations: the texture pool, low opacity, spawning directly ahead, the aircraft
  passing each puff, and ~24 visible frames at ~110 mph versus ~8 at ~300 mph. Full data and
  Ghidra runtime chain: `analysis/bl-317-plane-wisps/FINDINGS.md`.
  ⚠ **Three other mechanisms are ruled out — do not re-walk them** (2026-08-09, `crimson.exe` via
  Ghidra + a census of the shipped extraction; full evidence in that day's
  `git log --grep=BL-317`). (a) The profiler bucket **`ZBT_CAMDYN_CLOUDHACK`**, whose name
  promises exactly this feature, brackets `FUN_0042ee40` — the `CLOUD_COVER` whiteout and band
  flicker this repo already decodes (`WeatherRig.BandFlicker`, `weather.md`). It renders no
  sprites at all. (b) The GameGen keyword **`fluff`** (node-flag bit 12 = `0x1000` =
  `flags.unk12`, gated by the debug switch `CameraRenderFluffClutter`) is **foliage**: 47 nodes,
  all in C1, all fir trees and bushes, bbox 3.7 × 6.7 m at ground level — see `gamez.md`'s
  bit-12 bullet, written so this one is not chased twice. (c) There is **no third cloud
  population in the world data**: sweeping every node name in all eight chapters returns only
  `cloudparent` and `cloudsprite1/2` plus effect emitters. The wisps are neither world geometry nor
  clutter; they are the authored `speed_cue` puffer above.
  ⚠ **The hard-coded aircraft-local puffer is separate engine exhaust** (2026-08-10):
  `FUN_00476250` creates a `FUN_00550100` puffer at every
  `exhaust%d` locator (`exhaust1`, `exhaust2`, …); `FUN_004afbc0` enables it only from a
  positive commanded-vs-current
  throttle gap; `FUN_0054ee10` / `FUN_0054f8b0` leave world-space `smoke101`–`103` particles
  behind the moving plane (0.4 m distance interval, 0.2–0.3 m initial size, 0.5–1.5 s life,
  near-black→transparent). Its shared textures explain the false lead, but its rear attachment,
  scale, colour, life and throttle trigger rule it out. **BL-317's spawning mechanism is now
  located; remaining work is to implement the authored `speed_cue` animation/puffers in CSVM.**

- `BL-322` `[Bug]` **C5's lit facades render ×0.58–0.66 of the original with WorldLight already at
  clamp 1.0** (split out of `BL-303` at its close, 2026-08-08; measured `CAP-11`: tower faces 10.2
  vs 15.5, low-rise 21.7 vs 37.6). Explicitly NOT fog — `BL-303`'s own adjunct note, and the Wave
  B fog work moved none of it. Candidate direction: the lit-signage/self-lit family
  (`lighting: false` models draw fullbright, weather.md) — check whether these facades author a
  flag or vertex data we modulate that the original does not.
  *Playtest after fix:* the C5 night poses in `playtest/CAP-11/README.md`.

- `BL-325` `[Feature]` **Night cloud sprites are directionally moonlit in the original; ours are
  uniformly lit** (split out of `BL-118` at its close, PLAN-overcast-match `C24`, 2026-08-09 —
  decision 2 of that plan kept it out of a two-daytime-stills milestone). The original lights a
  night cloud by which side of it faces the moon; we apply one flat `WorldLight` to the whole
  population, so a moonlit frame is right on the away side and roughly half as bright on the lit
  side.
  *Evidence:* `CAP-11` C1B (2026-08-07, `playtest/CAP-11/`): cloud cores near the moon reach
  **p90 218** (t=16) while the away-from-moon cloud sits at **p90 70** (t=5); our uniform
  `WorldLight` 0.426 rendered **p90 102** — matching the away side and ~2× dark against the lit
  side. Re-measured on the Wave-B build (`B15`, plan `B17` Table 4): our C1B moonlit cloud tops
  read **p90 187.5** against the original's **155.4 (t5) / 181.9 (t16)** at the spawn pose
  (`--chapter=C1B --pos=-5406,55,-7200 --direction=-0.391,0,-0.921`), i.e. the *band* is now
  plausible and the *direction* is still absent.
  ⚠ **Which population.** C1B ships **no `fvol` volumes at all** (`FogVolumeTests`
  `C1B "0|-|206.25|bare|cloudsprite:absent"`; its freecam census prints no `fogvol clouds`
  line), so every cloud in that footage is one of the **70 placed `cloudparent` facades** —
  ordinary world geometry. Verified by `C23`'s fork landing, which changed the `fvol` card colour
  and left C1B byte-identical (`mean|d| 0.000, 0 px changed`, `c1b-night-sea` golden `ok`).
  ⚠ **Vocabulary — three populations, never one phrase for two** (`BL-118`'s note, kept alive
  here): **`cloudsprite1`/`cloudsprite2`** are the `fvol*` clutter scatter (the deck field,
  world-locked and tiled); **`cloudparent`** are discrete world-placed clusters (C1B's 70, C1's
  28, C4's 45); and `BL-317`'s **plane-local ambient wisps** are a third. A claim about one is not
  evidence about the others, and the first two **share their textures** — `--tex-override` on
  `cloud1.tif`/`cloud2.tif` paints both (`SHOT-21`), so separate them by altitude or cluster
  position, never by texture.
  ⚠ Traps: `csky_world_light` is CAP-11-calibrated on terrain — a directional cloud term must be
  cloud-local, the way `C22`'s deck fix was deck-local. And C1's own daytime cards were measured
  faithful at `lighting: false` (`C21`/`C23`), so this must not become a second global cloud
  brightness knob beside `FogVolumeClutter`'s `CardVertexColorTune`.
  *Playtest after fix:* the C1B night spawn above, against `playtest/CAP-11/`'s t5 and t16
  frames, saying for every measured puff which side of the moon it faces.
  *Cross-refs:* `BL-317` (the third population), `BL-327` (whether `lighting` gates `WorldLight`
  on a cloud card at all — if it does not, this item's arithmetic changes), `CAP-11`.

- `BL-327` `[Research]` **Is `lighting: true` on a `Facade` cloud card a `WorldLight` gate at all —
  and what else does the original apply to cloud sprites?** (minted at `BL-118`'s close,
  PLAN-overcast-match `C24`, 2026-08-09; the fifth candidate `C23` raised and deliberately did not
  guess at.) One item, because all three open questions below are the same question — *what does
  the original apply to a cloud sprite* — and any answer to one constrains the others.
  *Evidence (the flag):* C1/C4 `fvol` cards author `lighting: false`; **C1C/C2B author
  `lighting: true`** (`docs/formats/fogvol.md`) and we honour both, so their field renders
  `222.7 × 0.784 = 174.6` where C1's renders 222.7. The `C23` fork's `M-a` landing (2026-08-09)
  darkened every card by `225/240` and un-dimmed the above-band deck floor, and at C1C/C2B that
  made the frame **worse**, exactly as predicted and stated rather than tuned around: C1C's
  above-band frame now holds placed `cloudparent` facades **235.25**, an un-dimmed deck floor
  **195.8** and `fvol` cards **163.7** (measured 163.24 / 163.83; C2B 163.24) — cloud-population
  spread **60.7 → 71.6**, deck-floor↔card **−19.8 → +32.1**. If the flag is *not* a `WorldLight`
  gate, C1C's field is 208.8 with the same TUNE and sits ~26 units under its own placed clouds —
  the relationship C1 already has (236.65 vs 208.8). Moves C1C/C2B/C5 and nothing about C1's two
  reference stills.
  *Evidence (the far field):* at the CAP-12 1700 m rung `tops-L`/`tops-R` sit **+14.9 / +29.0**
  over the altitude-matched `t97` original (were +20.1 / +30.4 before `M-a`, which neither fixed
  nor worsened it), and at the pinned above-deck pose the original frame carries **74 dead-flat
  rows at `FOG_COLOR` 175 (10.4 % of frame, rows 517–590)** between dome and near sheet while ours
  carries **zero** — our `far_fade` rim steps where the original ramps over ~240 rows. A
  distance/fade question about the card population, on the same surface as the flag question.
  *Evidence (the plateau):* the `225/240` `CardVertexColorTune` in `FogVolumeClutter.BuildCardMesh`
  has **no decoded mechanism** — it is a calibrated match to the original's measured plateau
  (208.88 `t124` / 209.16 `t59`), marked TUNE in the constant's own comment, and anything that
  decodes the real mechanism **replaces** it rather than joining it.
  ⚠ **Traps — four candidates `C23` already refuted on data; do not re-chase them.** (1) *A
  `cloudsprite` opacity like `cloudparent`'s 0.6:* `extracted/C1/zrdr/clouds.zrd.json` read in
  full is one `ANIMATION_DEFINITION` naming `cloudparent` only, C1 is the only chapter shipping a
  `clouds.zrd`, and a sweep of every `zrdr/*.json` in all eight chapters names `cloudsprite` only
  in the eight `fogvol.zrd` files. (2) *`WorldLight` on C1's cards:* 0.802 puts them at 178.6,
  *below* the original's own 204.9–213.3 at the 1160 m rung. (3) *Fogging the cards:* the same
  clutter reader's tree templates all author `fog: true`, the world's placed cloud facades author
  `fog: true`, and `B16` verified the flag is honoured — `fog: false` on the card is a deliberate
  authored distinction. (4) *Carrying the field up with the relocated deck:* puts card tops at
  1177–1277 m against CAP-12's measured "clear above by ~1128 m".
  ⚠ **Instrument:** `population.py`'s flat-red deck mask (`r > 200`) can only see an **un-dimmed**
  deck in a 0.784-`WorldLight` chapter — `255 × 0.784 = 200.0` is exactly the threshold — so
  C1C/C2B read `0.0 %` mesh before `M-a` and `6.8 %` / `0.9 %` after. That is the mask waking up,
  not deck appearing. C1's 0.802 (204) clears it either way. Under fog the same mask goes blind
  further out: at the river pose it stops classifying the deck below ~55 px of elevation, where
  the fog mix has pulled the flat red under 200.
  ⚠ **`SHOT-21`:** `--tex-override` cannot separate the `fvol` field from `cloudparent` — they
  share `cloud1.tif`/`cloud2.tif`. Separate by altitude or cluster position.
  *Playtest after fix:* C1C and C2B above their band (`--chapter=C1C --pos=-7323,1192,-3829
  --direction=0,0,-1`, and C2B's `-3843,1500,-1101 / -0.391,0,-0.921`), plus a C1 re-check that
  the two reference stills did not move; and the CAP-12 1700 m rung for the far-field half.
  *Cross-refs:* `PT-47` (d) judges the C1C split at the controls, `BL-325`, `docs/formats/fogvol.md`.

- `BL-328` `[Tuning]` **The deck floor's 20,480 m annulus half-span was sized against a mechanism
  that no longer exists — re-derive it, or decide it does not need one** (minted at
  `PLAN-weather-decompile-match` `D31`, 2026-08-09; `WorldBuilder.AddDeckAnnulus`). `C26`
  (PLAN-overcast-match) picked 20,480 m from the rim formula `f·K/halfSpan` with `K` =
  `DeckCeilingHeight` = 135 m, i.e. against a ceiling **anchored to the camera**, which made the
  floor's far edge sit at a constant **3.95 px** below the horizon at every altitude. `B13` then
  made the floor world-fixed at the tiles' authored altitude and `B14` deleted `K` outright, so the
  edge's elevation is now `f·(cameraY − 960)/20480` and **grows with altitude**: measured at C1
  looking level west, **6 px** below the horizon at y = 1192 (predicted 6.79) and **33 px** at
  y = 2000 (predicted 30.4), where the 144-tile sheet's own 6,144 m edge would put them at 22.6 and
  101 px. At y = 1192 the edge reads as a soft ~19-unit ramp over ~10 rows from the `zone2` dome
  (mean 193.9) onto the floor (212.6). Nothing is broken today — the extension is still doing more
  work than the bare sheet at every above-deck altitude, and `D31` closed the below-deck strip it
  was built for by a different mechanism entirely (the zone-1 dome; the ladder's dip fell from
  `C26`'s 2.07/+1.11 to **0.15/0.14** at every rung) — but the NUMBER now rests on a dead
  derivation.
  ⚠ Traps: **below the deck the annulus is unreachable** — the tiles are `zone_id 2` and `B12`
  culls the whole sheet at camera state 1, so any below-deck measurement of it is measuring a
  forced `--sky-zone=zone2`, not play. The ceiling constraint is the flown dome, not the map:
  C1's `zone2` dome renders at 8,744 m × 2.5 = **21.86 km** unclamped, so 20.48 km already sits
  only 1.38 km inside it and a larger annulus needs `HorizonScaleFor` checked first (`B14` fits the
  scale per dome now). Do not reinstate `DeckCeilingHeight` or any camera-anchored floor to make
  the old formula apply again — `B14` deleted it on decompiled evidence.
  *Playtest after fix:* climb C1 from 1,100 m to the ceiling looking level at a clean horizon
  (`--pos=-7323,<y>,-3829 --direction=-1,0,0`) and say whether the floor's far edge is ever
  visible as an edge.
  *Cross-refs:* `docs/architecture.md`'s `WorldBuilder.cs` entry, `PLAN-weather-decompile-match`
  `D31`/`B13`/`B14`.

- `BL-329` `[Tuning]` `[Owed-playtest]` **The D32 in-cloud flicker's rate and ramp are declared
  TUNE, not decoded** (`PLAN-weather-decompile-match` D32, 2026-08-09;
  `Session/WeatherRig.BandFlicker`). `FUN_0042ee40`'s drift rate multiplies a per-mission
  weather-struct field ≈ `+0x934` that no reader decodes and no capture pins a value for, so
  `BandFlicker.DefaultRate = 5.5f` is picked, not measured: at the re-randomized drift speed's
  midpoint (0.2..1.0, mean 0.6) it traverses the blend parameter's full [0,1] range in `5.5 * 0.6 *
  0.1 = 0.33`/s, i.e. ~3 s at the mean and 1.8–9.2 s across the randomized range — "a full traverse
  in a few seconds", not a decoded figure. `BandFlicker.RampFrames = 30` (0.5 s at the fixed 60 Hz
  step) is the amplitude ramp that keeps a session's frame 0 (and any static probe/golden capture
  at a rig's first tick) reading the unremapped `WeatherState.WhiteoutAmount` exactly — its length
  is also a guess, chosen only to be short next to a flight and long next to one frame.
  *Evidence:* `docs/PLAN-weather-decompile-match.md`'s D32 entry; the curve shapes themselves
  (`BandFlicker.LogCurve`/`AtanCurve`/`Remap`) ARE decoded from `FUN_0042ee40` and are not part of
  this TUNE — only the rate and ramp length are a judgement call.
  *Fix shape:* none pending — needs in-cloud footage of the original with visible timing (a static
  screenshot cannot show a drift rate) before either constant can move off a guess.
  *Playtest after fix:* fly into C1's cloud band and hold in the RAMP, not the opaque core — the
  core (1032–1062 m) is fully whited out and the flicker's own guard skips it there by design
  (`--freecam --chapter=C1 --pos=-2000,1000,-1792 --direction=0,0,-1` sits in the bottom ramp) —
  and compare the shimmer's pace against any original in-cloud footage (`CAP-12`'s C4 take has
  in-cloud frames) once such footage is reviewed for timing rather than just colour.
  *Cross-refs:* `docs/architecture.md`'s `Session/WeatherRig.cs` entry (D32 bullet).

- `BL-304` `[Bug]` **Water gets the WorldLight dim; the original renders it unmodulated** (`CAP-11`
  A/B, 2026-08-07; surfaced closing `BL-110`; evidence `playtest/CAP-11/README.md`). C2B ocean
  foreground, same world, matched spawn pose: original 53.9 vs ours 42.0–42.5 — ratio
  **0.78 ≈ our `world_light` 0.784 exactly**, i.e. dividing our value by the dim reproduces the
  original within 7%. C1B's night ocean points the same way (original 37–42 vs ours 9–23) but is
  noisy — moon glitter and wave texture vary with screen position — so the night number is
  support, not proof. Candidate: exempt the water material from `csky_world_light`, the same
  should-be-exempt family as `BL-070`'s poleflare glows.
  *Playtest after fix:* the C2B low pose (`--pos=-3843,200,-1101`) against
  `playtest/CAP-11/t0.5-c2b-spawn-ocean.png`.

- `BL-337` `[Feature]` **`far_fade_range` is authored on all 143 `templates.zrd` clutter blocks and
  read but not applied** (C23, 2026-08-10; deferred since Decision 3 of
  `docs/PLAN-clutter-uv-placement.md`, which held mid-plan because a fade that removes distant
  clutter would confound Wave B's density A/Bs — see [`docs/formats/templates.md`](docs/formats/templates.md)).
  *Evidence:* `far_fade_range` decodes to `[[nearMin, farMin], [nearMax, farMax]]`, both distances
  drawn from **one** `rand()` per instance (`FUN_004dd6e0` step 11); C5's city blocks fade
  200–350 m, C1's firs 500–2000 m, the install spans 50–2000 m over 27 distinct pairs. The runtime
  scale is `CameraSetClutterFadeScaleSq` (script-command string `0x0063f5bc`, dispatch
  `FUN_005b80a0` case `'C'`), which writes a single global `_DAT_0062d170` via `FUN_004d2120`. That
  global is **not clutter-specific and not per-mission** — it is the squared distance-fade scale
  for every type-5 scene node's LOD/fade (consumers `FUN_004d5de0`/`FUN_004d6010`, which compute
  `fadeScale * distanceSq` against each node's near²/far² thresholds and blend an alpha). Its
  default comes from the graphics **detail-level** setter `FUN_00440750`: level 0/1/2 write
  `0x3f800000`/`0x40800000`/`0x41100000` = 1.0/4.0/9.0, i.e. fade distance scales ×1/×2/×3 with
  detail. The script command can override it per mission on top of that. So the authored metres are
  **not literal** — they are a base multiplied by up to 3× depending on the detail setting, before
  any mission script override.
  *Fix shape:* a rendering-side change, not a placement one — a distance-fade path in (at least) the
  clutter draw shader(s), fed by the per-instance near/far pair `ClutterBuilder` already stores but
  ignores, plus a detail-level-driven global scale mirroring `_DAT_0062d170` (currently nothing in
  the remake reads the graphics detail setting for this). Interacts with `MapEdgeExtender` (fringe
  clutter must not pop at the same distance the authored fade would remove it in the original).
  ⚠ Traps: implementing this without the detail-scale half only matches the game at one detail
  level; the two fade bounds are one draw, not independent ("`translate_uv_range`, `far_fade_range`
  and `rotation_range` are grouped by BOUND" — `docs/formats/templates.md`).

- `BL-341` `[Research]` **Reopened `BL-250`: with the real `no_clutter` gate landed, 7.6% of C5's ground
  (13.8 million m², the flagged overlay area with no base layer beneath it) renders bare, and
  whether that is what the original does is untested.** `BL-250` closed 2026-08-07 on a curated
  list (`ClutterBuilder.BuriedClutterDistricts`, excluding `cblock4/5/6` map-wide) that turned out
  to be standing in for a mechanism nobody had decoded yet. B13/B15
  (`docs/plans/PLAN-clutter-uv-placement.md`) landed that mechanism — `PlaceOnMesh` now skips a
  polygon carrying the decoded `no_clutter` flag, the flag SELECTS which of two coplanar layers
  decorates rather than meaning "bare here", and the curated list was retired because it was wrong
  on 14.1% of the map even though it happened to be right where `CAP-22` looked (78.3% of C5's
  ground by area is genuinely tower country). *Evidence:* B15's landing commit
  (`git log --grep="B15: land BL-305"`) measured the gate against every flagged/base pair in C5
  and found 35% of flagged overlay area has no coplanar base polygon underneath it at all — for
  that ground the gate now has nothing left to fall back on and leaves it undecorated. *Fix
  shape:* find what the original actually draws on that 7.6% — either a third layering mechanism
  this plan didn't decode, or the original genuinely leaves it bare too (which would close this
  outright). Start from a located landmark pose — the gate itself was confirmed by the user's own
  flyover 1 km north of C5's `brooklynbridge` node, not by a nadir (`SHOT-28`,
  `docs/verification.md`) — and not from `CAP-22`'s pose, which
  cannot resolve this question (it already reads correctly). *⚠ Traps:* (a) do not re-curate a
  list as a stopgap — that is exactly the mistake this item exists to not repeat. (b) A nadir
  shot cannot distinguish a painted rooftop from bare ground any better than it could distinguish
  a rooftop from a building (`SHOT-28`); use a low oblique. *Cross-refs:* `BL-250` and `BL-305`
  (both closed — the doubled-district curation and the C5 packing bug the gate that surfaced this
  replaced; `git log --grep=BL-305`. Do not reopen either ID; IDs are never reused, per this
  file's own rule).

## Effects & animation runtime

- `BL-335` `[Fidelity]` **Our puffer blend verdict reads the sprite's darkness; the original reads a
  flag in the texture's own header.** Reported at the controls 2026-08-10 (the refuel-tank flames),
  traced the same day and **fully decoded 2026-08-13**. The decode is
  [`docs/org/textures.md`](docs/org/textures.md), with the particle side in
  [`docs/org/puffer.md`](docs/org/puffer.md).
  **The engine's rule, whole:** a sprite is drawn **additively if and only if bit 2 (`0x04`) of its
  texture's render-flags word is set**, and alpha-mixed otherwise. That word is the u16 at offset
  `0x0E` of the 16-byte texture header, landing at `+0x0c` of the image object (`FUN_0052f7f0`).
  `FUN_005a4210` is the explicit form: set gives `SRCBLEND/DESTBLEND = ONE, ONE`; clear gives
  `SRCALPHA, INVSRCALPHA`. Particle quads take the deferred transparent list instead, where
  `FUN_005a6160` sets only `DESTBLEND` from the same bit, so additive there is `SRCALPHA, ONE`.
  Blend is therefore a property of the **texture**, so it is per particle and per flipbook frame.
  **Neither the `COLORS` ramp nor the sprite's darkness plays any part.** The ramp does pick between
  two dispatch entries (`DAT_009be790` → `FUN_005a4b70`, `DAT_009be78c` → `LAB_005a4580`, installed
  by `FUN_005a8c00`), but they differ only in FLAT vs GOURAUD shading and in whether the ramp colour
  survives into the vertices; neither touches a blend register.
  **Ours** (`Puffer.Create`) is `ramp OR diesDark ⇒ Mix, else Additive`, where `diesDark` is
  `SmokeLuminance` measured off the frame a particle dies on. That is wrong in both directions: it
  draws a ramp-less unflagged sprite additively where the engine mixes, and mixes a flagged sprite
  carrying a ramp where the engine adds.
  **The census, install-wide:** 31 of C1's 881 textures carry the bit (26 of C3's 732), and they are
  exactly the emissive elements: `fire101` … `fire112`, the lens flares, the impact rings and the
  HUD hilites/indicators. Of the puffer sprites, **only `fire101` … `fire112` are additive**;
  `fire_f01`, `fire_f02`, `fire_f06`, `smoke101/102/103`, `exp_yel01` and `thickblksmoke` are all
  alpha-mixed.
  ⚠ **Do not just delete the darkness rule. On its own it makes the reported case worse.**
  `fire_n_smoke` authors `colors: null` and dies on `fire_f06`, which is **unflagged**, so
  `blend_mix` is the correct answer that our rule happens to reach by the wrong route. Removing it
  without implementing the texture flag flips that emitter to additive and puts the symptom back.
  ⚠ **`TextureArchive` does not carry the field.** It classifies alpha from the decoded pixels and
  never sees the header word, so the raw value (`texture_infos[].stretch` in each chapter's
  `texture/manifest.json`) has to be plumbed through before the rule can be applied. See
  [`docs/org/textures.md`](docs/org/textures.md)'s "Suggested extractor changes" for the field name.
  ⚠ Expect this to move every puffer-bearing golden (the seven listed in `docs/architecture.md`'s
  `SizeScaleDefault` note).
  ⚠ **Correction to the earlier entry: the original DOES depth-sort.** `FUN_005a6160` fills an index
  array, `FUN_005a5fa0` sorts it by a per-polygon key proportional to distance (`LAB_005a5f00`
  returns `key[b] − key[a]`, so farthest first), and only then does it draw. The sort is across the
  whole frame's transparent polygons, not within one emitter. Our one `MultiMesh` per emitter with
  `depth_draw_never` and no sort is a **separate delta** from the blend rule, and it is the one that
  actually produces dark-over-fire. Fixing blend alone will not close the reported symptom.

- `BL-336` `[Fidelity]` **An unauthored puffer `TIME_INTERVAL` is `1.0` s in the original; both our
  parsers invent `0.1`.** Decoded 2026-08-10 while closing `PLAN-puffer-engine-deltas` C9: the
  puffer object's constructor `FUN_00550100` writes `0x3f800000` = **1.0** to both `+0x40`
  (interval) and `+0x44` (its reciprocal), and the applier only overwrites them when the parser set
  the flag — the same absent-vs-zero shape `WIND_FACTOR` has (`B6`). Ours defaults to `0.1` in
  `PufferState.Parse` (`TIME_INTERVAL`'s fallback) and in the field initialiser, a factor of ten
  fast.
  ⚠ **Not a straight constant swap, which is why C9 left it alone.** The same `0.1` does double
  duty in `FromAnimEvent` as the **synthetic still-host sputter cadence** handed to every DISTANCE
  state (`TimeInterval = byDistance ? 0.1f : …`). That one is not a default at all — it is our own
  invention for a fallback mode the engine does not have (a distance emitter on a motionless host),
  signed off separately, and moving it to 1.0 would make every static building's sputter ten times
  slower for a reason that has nothing to do with the ctor. Separate the two before touching either.
  ⚠ The same ctor settles `NUMBER`'s default at `+0x04` = **1**, which is `BL-218`'s open question —
  check it there before re-deriving it.
  *Cost of being wrong today:* small. Every fully-defined reader puffer that reaches the sustained
  path authors its own `TIME_INTERVAL`; the default is only reached by states that do not.

- `BL-326` `[Bug]` **C3's skydome draws a magenta rectangle below the camera: its gamez names
  `cloud1`/`cloud2`, the two textures C3 is the only chapter not to ship.** Seen at the controls
  by the user 2026-08-09 and confirmed by them to be on **`main`**, then reproduced and traced
  2026-08-09. Not aircraft-related — the "moves relative to the plane" reading is the giveaway
  that it is **camera-anchored**, not plane-attached.
  *The chain, all confirmed:*
  - Node **`g1155`**, `model_index` 492, `zone_id` 1, parent 3236 — the `zone1` group under the
    gamez `horizon` node, i.e. the **camera-anchored skydome**. It is the immediate **sibling of
    the `sun` node** (model 491) that `BL-165` anchors the lens flare to.
  - Model 492's polygons carry materials **270** and **271** → `texture_index` 266/267 →
    **`cloud1.tif`** and **`cloud2.tif`**.
  - Those two names are referenced by C3's `gamez/textures.json` and are **absent from every one
    of C3's archive folders** (`texture`, `rtexture2/4/6/8/12`). Census across the install: C1,
    C1B, C1C, C2, C2B, C4 and C5 each ship **12** copies of the pair; **C3 ships 0**.
  - Unresolved ⇒ `SceneBuilder`'s debug **magenta** (which `TextureArchive.DefaultOverride`,
    `TextureArchive.cs:51-54`, deliberately shares with `--tex-override`).
  *Reproduce:* `--freecam --chapter=C3 --no-fog "--pos=0,1500,0" "--direction=0,-1,0.05"` —
  straight down — gives **2886 magenta px**, a 76×38 rectangle at (602,352), RGB (190,60,196).
  ⚠ **You must look down.** A level chase-cam flight (`--fly --chapter=C3 --plane=player_bhawk
  --frames=90`, 2861 ft) renders **zero** magenta pixels. That negative control is why this
  reads as intermittent at the controls.
  ⚠ **The fix is a real decision, not a one-liner — do not just add the names to
  `KnownAbsentFromGameData`.** That set (`TextureArchive.cs:679-683`, currently `pir_spinner` and
  C5's `barngrill`) means "the retail data genuinely lacks this, render a neutral fallback instead
  of debug magenta". It would silence the magenta, but the two candidate readings differ in what
  the player should see and the data does not settle which:
  - **(a) A genuine per-chapter data gap.** C3's authors dropped the textures; the original
    engine drew nothing, or something blank, in that slot. Then `KnownAbsent` (or suppressing
    the node) is right.
  - **(b) The original resolved them from a shared pool.** Every *other* chapter ships the pair,
    so a global/cross-chapter texture lookup in the original would have found `cloud1` and drawn
    a real cloud. Then `KnownAbsent` hides a missing cloud behind a blank card, and the fix is a
    cross-chapter fallback.
  Distinguishing them is what settles it: (b) predicts a visible cloud below the camera in C3 in
  the original, (a) predicts nothing there. **Needs an original-game A/B** — fly C3, look down,
  and say whether a cloud card is present.
  ⚠ **Traps.** (a) `--tex-override`/`--tex-census` paint magenta *by design* — rule the flags out
  before reading the colour as a fault. (b) `gfly02/03/04.tif` are also referenced-but-absent in
  C3 and are a **red herring**: no material uses them (dead registrations), so they draw nothing.
  (c) `pock1.tif`, `snow16x16.tif`, `default` and `pir_spinner.tif` are referenced-but-absent in
  **every** chapter — the baseline, not a C3 fault. (d) This node is the sun's sibling in the dome
  subtree, so anything that suppresses it must not catch the `sun` node with it — that node is the
  lens flare's anchor (`BL-165`, closed; `analysis/bl-165-lens-flare/FINDINGS.md`,
  `CSVM/src/Session/LensFlareRig.cs`).

- `BL-035` `[Feature]` `[Blocked: cutscene player]` **Animation event kinds that need weapons or cutscenes — `CALLBACK`, `OBJECT_CYCLE_TEXTURE`,
  one-shot `SOUND`** (triaged 2026-07-22, the last of `docs/plans/PLAN-anim-rendering-followups.md`
  item 2 after `OBJECT_MOTION` landed). All three still dispatch at bootstrap, so the counts in
  the "not yet acted on" report look like open work — **they are not**. Each was probed at the
  dispatch site across C1/C3/C4/C5 (def, anchor, resolved target count, payload), and each fails
  for a concrete reason rather than a suspicion. Implementing any of them today is a provable
  no-op, the same verdict `OBJECT_ADD_CHILD` got:

  | Kind | Count | Why it cannot do anything |
  |---|---|---|
  | `Callback` | ×8 every chapter | Every dispatch is `def=camera1`, **unanchored**, values 1/2/10/11/14/20/913/914 — engine notifications for the intro **cutscene** camera. This project has no cutscenes, and a callback's whole purpose is to notify mission logic that does not exist here. |
  | `ObjectCycleTexture` | ×1–2 per chapter | Every dispatch is `node=taildamage` with **`targets=0`** — the node never resolves, so there is nothing to cycle. The one real use of this mechanism (the cockpit damage-indicator hilite) is already a build-time material swap in `GaugeCluster.cs`. |

  **Pick these up when the thing they depend on exists** — a cutscene player for `Callback` — not
  before. `ObjectCycleTexture` needs neither; it needs a mission that
  actually builds a `taildamage` node, which none of the ones this project defaults to do.

- `BL-218` `[Tuning]` `[Owed-playtest]` **Puffer `NUMBER` default (2026-08-01)** — `NUMBER` is absent from 680 of C1's 721
  `PufferState` events, including `large_30sec_fire`'s `fire_n_smoke`, and `PufferState.FromAnimEvent`
  falls back to **1** sprite per `TIME_INTERVAL`.
  ✅ **The default half is SETTLED, and our 1 is right** (2026-08-10, `PLAN-puffer-engine-deltas`
  D10): the puffer object's ctor `FUN_00550100` writes `1` to `+0x04` before any authored key is
  applied ([`docs/org/puffer.md`](docs/org/puffer.md)). This entry's original reasoning — that the
  sibling `large_10sec_fire`'s `NUMBER 3` implied a higher default, leaving every unnumbered emitter
  thin — is **withdrawn**: 3 is that puffer's own authored value and was never evidence about the
  unauthored case. **Do not raise the fallback**; a
  density gap is now a look question about our sprites, never a default question. What remains:
  judge the density at the controls now that the fire's *shape* is right
  (`PT-22`) — it is a whole-effect multiplier, so a wrong value is visible on the destruction fires,
  the damage-stage sputters and the wreck smoke at once.
  ⚠ Traps: this is not the `puffer.*SizeScale` knobs — those scale sprite size, and trading
  count for size is exactly the substitution that makes a too-sparse plume read as "too small"
  instead. That substitution shipped for a while as the global 4× `SizeScaleDefault`; `BL-282`
  reverted it to the authored 1× (2026-08-05), so a density verdict now measures `NUMBER`
  alone. Do not tune it from a single `--screenshot`: sprite count only reads over a time series
  (SHOT-19). And do not infer the default from the effects readers — the `NUMBER`-carrying states
  are a biased sample, since `PufferState.FindInReader` treats the presence of `NUMBER` as what
  makes a state "fully defined" in the first place.

- `BL-231` `[Tuning]` `[Owed-playtest]` **Effect-template pool sizes (D10, 2026-08-02).** `CSVM/data/effect_pools.json` — how many
  copies of each effect template the world-effects stage holds, so that many overlapping calls to one
  effect each keep their own (`BL-225`). **Invented, and the data cannot settle it**: the original
  copies its template per call and has no such number, so any finite pool is our approximation of
  "unbounded" — which is why it is an editable file and not a `const`. Shipped: default **4 base
  +1 per extra player**, `partial_damage_obj` **8 +1**, the three gun roots **1 +0**, ceiling
  **16**. The default came from rocket concurrency (`FIRE_RATE` 1/s against ~2.5 s of authored trail
  motion → at most 3 overlapping blasts) plus a spare; the sputter root from measurement (five
  simultaneous `ap_h2otwr` kills wrapped a 4-slot pool exactly once, and do not wrap an 8).
  Judge it where concurrency is highest — a rocket burst into a cluster of destructibles, and
  splitscreen/multiplayer, where each extra aircraft is another source. **The per-player term and
  the ceiling are the two knobs a many-player build should re-judge**: at 16 players the default
  root wants 19 and gets 16.
  The instrument is in the build: `AnimRuntime.PoolRecycles` counts every call that wrapped onto a
  still-live slot and the runtime names the first per effect (`anim: effect pool for '<name>'
  recycled slot …`); the world-effects build line prints the sizes actually staged. A scripted run
  that logs no recycle had enough pool — raise the root that logs one, not the default.
  ⚠ Traps: it is not free — each slot is one more copy of that root's subtree (1 player: 147
  templates; 4 players: 252), so raising the default multiplies world-build cost and memory for
  effects that are mostly not concurrent. The three gun-impact roots stay at **1** deliberately: C8
  throttles the gun family to one play per 0.1 s per name, so pooling them buys copies nothing uses;
  raising them belongs with removing that throttle (its own step, its own emitter-count check).
  Sizing a root **0** is not a way to disable pooling — it clamps to 1, because staging no template
  at all reads in-game as a broken effect.
  **Extended 2026-08-04 (`BL-253`) with a second, smaller pool in the same file** —
  `localCallRoots`/`localCallDefault`, for `AnimRuntime.ResolveLibraryRoot`'s death-triggered
  library-root call templates (`docs/formats/gamez.md`), kept apart from `roots` because that map
  is validated against `WorldEffectsFactory.EffectStageRoots` and these names never are one. Same
  invented-number caveat, narrower scope: `facdsticks` (C2's facade-panel debris template) is the
  one entry, **base 6**, no per-player term (world geometry, not per-player ordnance) — sized
  against a facade row breaking panels ~0.2–0.5 s apart with each set's flight lasting 4–5 s, so a
  10-panel row can want 8–10 concurrent sets; 6 covers most passes and wraps (recycles the oldest,
  still-flying set) on a longer burst.

- `BL-293` `[Tuning]` **Rocket impact rings: the fixed-axis upper ring is faithful but reads
  poorly — parked** (PT-35). Faithfulness versus feels-good, decide later: the original
  (`Crimson Skies 1.02 2026-07-31 23-27-53.mp4`) shows the second (upper) HE ring always oriented
  on the same fixed axis, matching our behaviour, so ours is CORRECT as-is and this is not a bug.
  The rule behind it is decoded: `FUN_005ac7a0` spawns the `IMPACT` row's `SURFACE_ANIMATION`
  rotated from world up onto the struck surface's normal and the row's plain `ANIMATION` on the
  fixed axis ([`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md), "Half one, the direct
  impact"), and the remake follows it (`ProjectilePool.SurfaceUpBasis`, the `impact-orientation`
  suite): the ground effect (the `default` row's `SURFACE_ANIMATION` on every rocket) lies on a
  slope, while the upper ring, reached through a `CALL_ANIMATION` inside it, keeps its fixed axis
  because that is what the data authors. The proposal on the table for the feel side: upper ring
  facing the plane / against the rocket's flight direction. The ring anims carry no rotation data
  (scale/opacity only — `docs/formats/weapon-effects.md`), so any change is engine-side and a
  deliberate deviation from a decoded rule.
  Cross-link: `BL-292` (crash-splash orientation, different spawn path; scheduled in
  `docs/plans/PLAN-m3-polish-10.md` A3).

- `BL-334` `[Research]` **A stopped sequence stays callable in CSVM; in the original it is disabled
  until the definition resets.** `STOP_SEQUENCE` (`004eb610`) writes the sequence *done*, and
  `CALL_SEQUENCE` (`004eb570`) starts a sequence only from *parked* — so once stopped, a sequence
  cannot be called again for the life of the instance. CSVM halts the runner but does not persist
  that disable, so a later call restarts it. **123 definitions name one sequence in both a call and
  a stop** — mostly `flame_light_seq`, plus `chuteman_drop`/`chuteman_sway`,
  `sail_splash*`/`yacht_splash*`, and `warhawk`'s `smokepuff1..3`.
  *Fix shape:* the open question is reachability, and a static census cannot answer it — whether any
  of the 123 reaches its stop *before* its call is control flow. Instrument the runtime to log a
  call arriving at a sequence this instance already stopped, then run the 8-chapter `--freecam`
  sweep plus the effect closure. Zero hits across that surface is a disproof and the divergence
  stays documented; any hit names the def to reproduce, and the fix is a per-instance stopped-set
  consulted by `AnimInstance.CallSequence`.
  ⚠ **Traps:** do not implement the disable on the strength of the decode alone. It would change
  behaviour in up to 123 definitions to match a rule none is yet known to observe, and a sequence
  wrongly left disabled fails *silently* — the effect simply never plays again, which is the
  hardest class of bug to attribute later. The instrument comes first.
  *Cross-refs:* `docs/formats/anim-definitions.md` (the decoded `CALL_SEQUENCE`/`STOP_SEQUENCE`
  state rules). The `PLAYER_RANGE` `* 4.0` divergence the same decode opened is closed as a
  disproof — the `* 4.0` is on `PLAYER_LINED_UP`, not `PLAYER_RANGE` (`git log --grep=BL-333`).

- `BL-355` `[Bug]` **The damage/crash effect cascade hitches on first use — synchronous emitter
  construction (shader material + particle system), not GC and not allocation volume.** Diagnosed
  under the frame-hitch instrument (`PLAN-perf-hitches` G15/G16), via the scripted proxy G15 landed
  since the aircraft `DamageLab`'s own burst has no CLI repro (E13): `--crash=300 --no-vsync` (`--fly
  --chapter=C1 --plane=player_bhawk`) tripped `HitchMonitor` twice, frame 300 `frame_ms=48.43`
  (`samples=part_detach:1x33.01`) and frame 301 `frame_ms=62.11`
  (`samples=effect_pool_miss:7x54.36`), sidecar `.scratch/logs/fly-20260814-203733.hitches.jsonl`.
  **Ruled out, from the record itself:** GC — `gc0_delta`/`gc1_delta`/`gc2_delta` are **0** on both
  hitching frames, no collection of any generation fired. Allocation volume —
  `allocated_bytes_delta` is 300-350 KB on each hitching frame, three orders of magnitude under the
  ~860 MB burst `PLAN-perf-hitches` B5 needed to move the GC/alloc columns at all. GPU/render —
  `render_cpu_ms`/`gpu_ms` stay at their normal ~0.5/0.2 ms on both frames; the cost is entirely
  inside the CPU/script span `HitchMonitor`'s `frame_ms` measures.
  **Mechanism, traced live** (a temporary, reverted `GD.Print` in `EmitterDirector.Assert`'s miss
  branch — `git diff` empty afterward): the crash's own dispatch names ten distinct first-time
  misses in the same one-two frames — `lgpuffer` on `piece1`/`piece3`/`piece4` (`large_firetrail`),
  `spurtpuffer1`..`5` on `fly_trail1`..`5` (`call_crash_trails`), `fierypuffer` on `flame_ball_01`
  (`large_fireball`), `trailpuffer2` on `yellow_spark_01` (`small_yellow_sparks`) — every one a
  `(name, host, def)` key `EmitterDirector.Assert` has never seen before, each paying
  `_factory.Create`'s full build (a `Puffer` plus, nested inside the same scope per the code's own
  comment, `EmitterRenderer.Attach`'s `MaterialCreate`) synchronously, inline in the frame the crash
  fires.
  **This is NOT pool exhaustion — raising `effect_pools.json`'s `crashRoots` sizes will not fix
  it.** `large_firetrail` is sized 6 and only 3 concurrent pieces were in flight; no
  `AnimRuntime.PoolRecycles` wrap occurred. The pool avoids RELOCATING an already-built emitter onto
  a new call; it does nothing for the first build of a distinct key, which is what costs here.
  *Fix shape:* pre-warm the crash rig's (and, by the same mechanism, `DamageLab`'s) effect
  templates — construct each `crashRoots`/damage-stage emitter once, off the frame that needs it
  (plane spawn, session build, or a loading beat), the idea `StartupProfile`'s `prewarm` phase
  already applies elsewhere — rather than leaving the first assert to build synchronously.
  Alternatively, spread a compound event's misses across several frames instead of one dispatch
  batch.
  **Confirmed at the controls, 2026-08-14** (interactive `--fly`, vsync on, real play — not the
  `--crash=` proxy): `.scratch/logs/fly-20260814-210336.{log,hitches.jsonl}`, a session working
  through the `DamageLab` panel, tripped `HitchMonitor` 31 times in ~7 s (frames 3373-4243; 6 of
  those records lost to a sidecar-queue overflow, filed separately as `BL-356`) — **24 of the 25
  that survived carry `effect_pool_miss`** as their named site, in the same paired-consecutive-frame
  shape the `--crash=` proxy showed, spaced roughly every 40-90 frames as different parts/thresholds
  were dragged for the first time. **This answers the open recurrence question below: yes,
  repeatedly** — not a one-time session cost. It recurs because there are enough distinct
  `(name, host, def)` keys (8 parts x armor+health x several `injure_anims` thresholds each) that a
  real sweep through the panel keeps finding new, never-before-built ones; it is not that any single
  key re-triggers construction on a repeat. The 25th trip (frame 4243) is a genuine outlier worth
  naming separately: `frame_ms=79.91` with `samples=[]` — nothing in `PerfSample` claims any of it,
  and every counter (`draws`/`prims`/`nodes`/`gc*`/`alloc`) sits at baseline. Unexplained by this
  item's mechanism and not chased further here; possibly an OS-level stall rather than a CSVM one.
  ⚠ **Traps.** The original diagnosis was the `--crash=` proxy alone (`FlightController.Crash()` →
  `CrashRuntime`), not a captured aircraft `DamageLab` slider-drag session — `DamageLab.Reapply()`
  still has no *scripted* repro (E13/G15). The 2026-08-14 controls capture above closes that gap
  with a real one: the two share the same `EmitterDirector.Assert`/`WorldEffectsFactory`
  construction path, and the crash rig plays the same damage-stage template family
  (`crashRoots`'s `planeflakes`/`yellow_spark_02`/etc. are the `pdpanelN` effects `DamageLab`
  triggers) — no longer inference alone.
  ⚠ **Formerly-open question, now answered: does the cost recur across a session, or only once?**
  Recurs — see the 2026-08-14 capture above (24 separate trips, not one). Still open: whether any
  SINGLE `(name, host, def)` key re-triggers construction on its own repeat (a second drag of the
  SAME slider back past the SAME threshold) — the capture shows many DIFFERENT keys firing once
  each, not one key firing twice, so that narrower question is untested either way.
  *Cross-refs:* `PLAN-perf-hitches` G15/G16 (the diagnosis), `BL-356` (the sidecar losing 6 of this
  session's 31 trips to its queue — fixed in commit `73512b47` by raising the defaults to fit the
  storm), `BL-231` (the pool-size tuning item
  this is explicitly NOT — a size increase would not touch this cost), `docs/verification.md`
  PERF-14.

- `BL-418` `[Bug]` **The sonic burst hitches on its first plays: first-time emitter/material
  construction for its nine puffers, the `BL-355` mechanism on the world-effects runtime.**
  *Evidence:* the sonic weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08 --weapon-fire
  --infinite-ammo --weapon-surface=default --weapon-standoff=90`) trips `HitchMonitor` on the first
  bursts (sim frames 141/202/263, one per fresh slot copy) with 130-290 ms frames whose samples read
  `effect_pool_miss:8x`, and once with a 70 ms frame naming `effect_checkout` alone; `PoolRecycles`
  stays 0, so it is not the pool wrapping. `sonic_ground_effect` calls nine puffer defs (`sonic_puff1`,
  `sonic_puff4`..`sonic_puff11`, `extracted/zrdr/sonic_control.zrd.json`), and each first
  `PUFFER_STATE 1` on a never-seen `(name, host, def)` key takes `EmitterDirector.Assert`'s miss
  branch (`CSVM/src/Mech3/Anim/EmitterDirector.cs`, the `PerfSite.EffectPoolMiss` scope), building
  the `Puffer` and, nested inside it, `EmitterRenderer.Attach`'s material, synchronously in the frame
  the burst fires. With four pool slots per root, four bursts each pay it once per slot copy before
  every key exists.
  *Fix shape:* pre-warm those emitter keys at stage build (`WorldEffectsFactory.BuildWorldEffectsRuntime`,
  once per pool copy), the same idea `BL-355` names for the crash rig, so the first burst finds every
  emitter built. Not a pool-size change (`effect_pools.json` sizes concurrency, not first construction).
  *How you would know:* the same probe run to eight bursts shows no `effect_pool_miss` sample after
  the build, and no `HitchMonitor` trip whose samples name `effect_checkout`.
  ⚠ *Trap:* the checkout re-reset (`AnimRuntime.ResetCheckedOutCopies`) runs in the same
  `effect_checkout` scope; a hitch attributed to that site is this item's construction cost, not the
  reset, until measured otherwise.
  *Cross-refs:* `BL-355` (the crash/damage cascade's identical mechanism), `BL-406` (closed).
- `BL-419` `[Fidelity]` **The sonic ground burst does not read like the original's: ours is soft cyan
  hoops rising in the air, the original is one flat crisp pale-green ring growing on the terrain.**
  *Evidence:* `OriginalScreenshots/Videos/CAP-23 Rocket Sonic Ground.mp4` (frames 200-330 at 30 fps,
  impact at ~204): a bright white star flare with two or three thin pale rings for ~0.3 s, then ONE
  flat crisp pale yellow-green annulus lying on the terrain with radial striations and a dark centre,
  expanding smoothly for ~3 s and fading by ~4.2 s, plus a thin blue-white vapour column and no
  smoke. `CAP-23 Rocket SONIC Air.mp4`: the air detonation is a small brief white sparkle. Ours
  (`--weapon-lab=wep_08 --weapon-fire`, sim frames 12-126): three or four fat soft cyan hoops read as
  rings rising in mid-air, all finished inside ~1.1 s, then `ring_down1` grows into a very large
  fuzzy cyan torus above the ground from 1.3 to 2.1 s, under a thick grey puffer column the original
  does not have. Total life is comparable (~3.8 s authored vs ~4.2 s measured), but ours is
  front-loaded and airborne where the original is one continuous ground ring. The defs are
  `sonic_ground_effect` calling `ring_up1..4` at t=0 and `ring_down1` at +1.2 s on `sonic_ring1..5`
  (`extracted/zrdr/sonic_rings.zrd.json`, `sonic_control.zrd.json`), placed as a `SURFACE_ANIMATION`
  with world up rotated onto the struck normal.
  *Where to look:* how the anim runtime reads the ring defs' motion (a translation up versus a scale
  in the ground plane), their opacity ramps and colour, and whether the puffer smoke belongs to this
  def at all; the burst can be filmed frame by frame in the weapon lab against the reference frames.
  This is a decode question against `crimson.exe`'s anim interpreter before it is a tuning one: no
  number here should be adjusted to the footage.
  *How you would know:* a weapon-lab burst on flat C1 ground reads as one flat pale-green ring on the
  terrain growing for about three seconds, no airborne torus, and the same def in the air reads as
  a brief sparkle.
  *Cross-refs:* `CAP-26` (the rocket-impact rings capture; the sonic half is answered by the CAP-23
  clips above, and its "look for" list should gain the flat-ring-versus-airborne-hoops question),
  `BL-418`, `BL-406` (closed).

## Audio

- `BL-079` `[Feature]` **Positional 3D audio for other aircraft** — all sound is own-plane non-positional today;
  the original's IA traffic is clearly audible in the reference video.
  ⚠ **The "with Doppler" half of that claim is now suspect and must not be built against.** This
  entry originally read "clearly audible with Doppler"; that was an impression off a listen, never a
  measurement. `CAP-09` measured the original's *world* emitters and found **no Doppler at all**
  (`docs/HISTORY.md` 2026-08-04, which closed `BL-160` and carries the full method): the police siren
  plays at its source asset's pitch to within **0.008 %**, and at its source rate to within 0.14 %,
  straight through a 250 mph overflight.
  `CAP-09` contains no other aircraft, so it does not settle the IA-traffic case
  on its own; but the engine that declines to pitch-shift a police siren is unlikely to pitch-shift a
  passing plane. Treat Doppler on IA traffic as **unverified**, and measure it (same method: track a
  tonal component against the source WAV) before implementing it.

- `BL-223` `[Bug]` `[Owed-playtest]` **Damaged-engine loop gates on the wrong thing: any part's worst fraction, not the
  engine part's health pool (B5, 2026-08-01; regated per playtest 2026-08-06, PT-26).** Today
  `PlaneStats`/`FlightAudio` blend `snd_damagedengine` from `1 - PlaneDamage.WorstFraction` across
  ALL parts — first scratch anywhere brings the loop in. Two fixes, both data-supported:
  1. **Gate by zone:** blend from the **engine-marked destroyable part(s)** only — the
     `destroyable_parts` `engine` flag marks the part the engines physically live in (tail on the
     Bloodhawk, wings on the Balmoral; `docs/formats/vehicle.md`), matching the user's read of the
     original. Damage elsewhere leaves the engine sounding healthy.
  2. **Gate by pool:** the loop responds to that part's **hit-point pool**, not its armor —
     armor-only damage stays silent. Implementable now: the two-pool
     `PlaneDamage.Apply(part, healthDamage, armorDamage)` landed 2026-08-04 (`PLAN-armour-layer`).
  The `damaged_engine_sound` `f0/f1` fade window (`["snd_damagedengine", 0.0, 1.0]`, shared via
  `basic_airplane`) then reads over the engine part's health fraction. A pleasant consequence:
  with armor spent first, the loop naturally starts only once real airframe damage exists — which
  answers the old "should it ramp rather than snap on first scratch" question by construction.
  No capture owed: the `engine` data flag plus the user's recollection carry the zone rule, and
  filming the original to prove armor hits don't trigger it would be trying to hear a negative.
  The gain TUNE survives as this entry's tail: `FlightAudio.DamagedEngineMixGain` (default 1.0,
  the def's own sounds.json volume, unattenuated) has no reference recording of its own — judge it
  against the healthy engine at the controls after the regating. Config keys:
  `flightAudio.damagedEngineMixGain`.

- `BL-252` `[Tuning]` `[Owed-playtest]` **Overspeed-whine volume** (`prop_sound`). `CAP-10` plus a
  live cross-check incidentally confirmed the **gating** of the original's dive/overspeed sound and
  left only its level open.
  ⚠ **This entry's SUBJECT is now wrong, and its observation is the valuable part.** There is no
  whine: no shipped def names `prop_sound`, so slot 1 is unassigned install-wide, and
  `WhineMixGain` no longer exists to tune. What the entry actually recorded is that the original
  plays *something* starting exactly at `1.0× fd_speed`, and that is still true and still
  unexplained by the engine slot, whose two non-throttle terms read turn rate and attitude and know
  nothing about speed (`docs/formats/vehicle.md`). **The open candidate is the RATTLE**, whose
  shipped curve is volume 0→1 over `1.0`→`1.2× fd_speed`, i.e. the same foot the entry measured, and
  which unlike the whine IS assigned on every def (`snd_planeshake`). ⚠ Settle what the sound is
  before tuning any level; this entry has already tuned the wrong slot once.

  **The gate is the plane's own maximum level speed, not a fixed number.** User test 2026-08-04
  (Hoplite and autogyro): hold straight and level at 100% throttle — which by definition settles at
  max speed — then dive, and the sound starts exactly as the speed goes past it. That is `1.0×
  `fd_speed``, i.e. precisely the foot of the shipped `prop_sound` curve (volume 0→0.5 over 1.0→1.1×
  `fd_speed`), so **the gating needs no change**. ⚠ **Do not read a threshold off the airspeed dial:
  the gauge art, including its red arc and its `300` mark, is the same for every plane** and so
  cannot express a per-plane limit — a trap this entry walked into once already.

  `CAP-10`'s Bloodhawk footage times the edges and agrees: the 400–900 Hz band steps up at t≈5.2 s
  and back down at t≈11.7 s in `CAP-10.mp4`, while the needle crosses the corresponding dial position
  at t≈5.0–5.5 and t≈11.5–12.0 — both edges inside ~0.3 s, and sharp rather than a continuous swell,
  as a ramp band crossed in well under a second should look. `CAP-10 2.mp4` repeats it (audio on
  t≈7.5, off t≈17.0).

  What is *not* settled is the volume: the +2 dB measured here is a band-limited figure, not a
  loudness, and the mix ratio differs by view because the original's **cockpit** engine is damped
  while ours is not — so it cannot be read across. **Needs a level match by ear against the
  original, not another measurement** (user, 2026-08-04: "the only tune parameter would be volume").

- `BL-269` `[Tuning]` **The 3D sound falloff curve between the authored `RANGE` radii is an admitted
  approximation** (`WorldSounds.cs:159-161` — endpoints authored, curve "an approximation of
  the original's, hence TUNE"). Low stakes per sound but global: every positional sound's
  audible footprint. A calibrated fly-past recording of one loud fixed emitter (the C1
  refinery flare is a candidate) would trace the real curve.

- `BL-281` `[Tuning]` `[Blocked: CAP-27]` **The ricochet sounds are audible but very faint.** `PT-25` (c), 2026-08-05:
  `snd_ricochet1–4` play under the per-impact spark burst but sit too low to read. A mix-gain
  question with no reference recording behind it — same shape as `BL-223`'s damaged-engine gain,
  and to be judged at the controls rather than derived. ⚠ Judge only after `CAP-27` decides
  whether the original has this effect at all: `BL-090` already calls the 0.99 `injure_anims`
  entry that drives it "plausibly an authoring leftover", present on 1 of 11 aircraft, so the
  capture may delete the feature rather than tune it.

- `BL-285` `[Tuning]` `[Owed-playtest]` **Engine start/stop residues from `BL-267` (landed 2026-08-05)** — two constants
  pending the user, both in the same cockpit sitting. (a) `ThrottleSlamSmoke.SlamThreshold`
  0.25: the CAP-21 footage only bounds the slam gate to "a single 1/8 step never fires,
  idle→5/8 fires" — the 2/8–4/8 band is unobserved, so 0.25 is the smallest threshold
  consistent with both and a declared TUNE. (b) The listen A/B: `EngineStartRamp` is now the
  `startprops` authored 2.0 s and the crash/destruction wind-down plays `snd_propstop` — judge
  both by ear. ⚠ Trap: `BL-268` (`docs/plans/PLAN-m3-polish-10.md` C21, landed 2026-08-06) removed
  the blanket ×0.2 mix scale on these same paths, raising the own-ship mix ~5× — judge the
  ramp/stop cue against the new, unscaled level, not the old ×0.2 one. ⚠ Second trap, added by
  `PLAN-splitscreen-polish.md` D32 (`BL-371`, landed 2026-08-15): `snd_propstop` (the wind-down
  half of this A/B) now carries splitscreen's `MixGain` too — 1 in 1P, so this pending single-pilot
  judgement is unaffected, but a splitscreen listen must judge it at whatever `N` the pilot is
  testing, not assume the 1P level. `snd_propstart` (the other half of this A/B) is unchanged —
  D32 kept it raw, "your prop" on respawn stays loud on purpose.

- `BL-391` `[Tuning]` **Own-ship engine loop reads too loud, including single-player.** Found
  2026-08-15 at the `BL-126` splitscreen chrome playtest — a 4-player Dogfight session flagged the
  stacked engines as too loud, but the user confirmed on a follow-up single-player listen that the
  base engine level itself, not just the splitscreen stacking, is too hot. Not a splitscreen item:
  `FlightAudio.MixGain` is `1` in 1P (no attenuation applies), so this is the vehicle.json
  `engine_sound` mix level (or the detuned dual-voice stack's combined gain, `EngineDetuneRatio`)
  read too loud on its own terms. *Fix shape:* a level match by ear against the reference video,
  same method `BL-223` and `BL-269` already used for this signal chain.

- `BL-424` `[Feature]` **A choked engine still sounds like a running one.** `PT-69` (d): the choker
  (`wep_12`, `TANGLER`) cuts thrust for 5–13 s and nothing in the audio chain reacts, so a choked
  aircraft, your own included, keeps its full engine loop. `FlightAudio` drives the loop from
  throttle and damage (`damaged_engine_sound`), never from `FlightController`'s engine-dead timer.
  ⚠ What the original plays over the cut is **undecoded**: the `PT-69` row asserted an engine-loop
  swap, but no `docs/org` page records one, so decode the original's behaviour (silence, a stop/start
  pair, or a second loop) before building. Scope is every loop a session renders: the own-ship one,
  and the AI planes' positional loops (`PLAN-ai-damage-and-engine-audio`), which read the same
  engine model and would otherwise keep running through a choke too.
  *Fix shape:* gate `FlightAudio`'s loop on the engine-dead timer the same way the thrust cut reads
  it, with whatever the decode says the original plays over the gap.
  *Cross-refs:* `BL-421` (closed; it confirmed the engine-audio model at the controls), `BL-223`/`BL-285`
  (the loop's damage and start/stop inputs), `BL-406` (closed; the choke itself landed there).

## Cameras & views

- `BL-150` `[Feature]` **plan-sized — not a TUNE. Numpad camera views — the whole scheme needs a rebuild, not a
  retune.** ⚠ **This IS the chase camera's head-look controller, not nine authored poses — read
  `BL-435` first.** `PLAN-cockpit-view.md`'s decode of `FUN_0042c7f0` found the numpad views run the
  same head-look state machine C21 ported as `HeadLook`, floored at `−π/2` instead of level; the
  rebuild below should implement that controller, not a table of nine poses. Item (f)'s `+`/`−`
  distance trim is the same control as `BL-433`'s External Camera Zoom axis — build them together.

  Current implementation: `FlightController.cs:286-303` (`Views[]` table, keys
  Kp1/2/3/4/6/7/8/9 only — **no Kp0**), `:1653-1676` (`ActiveView()` — held key beats the scripted
  `PinnedView`, first array match wins on multiple keys down), `:1685-1690` (`ApplyFixedView` —
  instant snap, no smoothing, shares the chase camera's `ViewDist`); `--view=N` is the scripted,
  machine-verifiable twin. Cockpit testing (2026-07-30) overturned the "layout is settled" claim this
  whole scheme was built on and found five more open questions:
  (a) **Layout is wrong — MEASURED 2026-08-04 from the `CAP-07` scripted re-take, all nine keys.**
  The original's layout, read off nine settled stills (method and confidence below):

  | key | camera sits | ours today (`CameraController.cs:53-60`) |
  |---|---|---|
  | `Kp1` | **ahead + starboard, below** | left + below flank (no fore/aft term) |
  | `Kp2` | **dead ahead, level** | straight below |
  | `Kp3` | **ahead + port, below** | right + below flank (no fore/aft term) |
  | `Kp4` | **starboard flank, level** | left flank |
  | `Kp5` | **unbound — confirmed, not assumed** | unbound ✓ |
  | `Kp6` | **port flank, level** | right flank |
  | `Kp7` | **astern + starboard, below** | left + *above* flank |
  | `Kp8` | **directly below (belly plan view)** | ahead of the nose, looking back |
  | `Kp9` | **astern + port, below** | right + *above* flank |

  Three structural corrections, not a symbol shuffle. (i) **The original has no above-the-aircraft
  view at all** — every non-level position is below; our 7 and 9 are the only above views and both
  are wrong. (ii) **The four corners carry a fore/aft term our table has none of**: bottom row
  (1,2,3) is the forward hemisphere, top row (7,9) is aft. Ours splits them above/below the flanks
  instead, so this is a different *shape* of layout. (iii) **4/6 and 2/8 are both swapped** — 4 shows
  the starboard side, and 8 is the belly while 2 is the nose-on view.

  *How it was measured.* Nose-in-image direction plus which surface is visible fixes the quadrant
  analytically: with image-right = `u × d`, the nose projects with horizontal component ∝ sin φ and
  vertical ∝ −sin ε · cos φ (φ = azimuth from dead astern toward starboard, ε = camera elevation
  *below*). The level side views calibrate the sign — a camera to starboard must show the nose
  pointing image-right, and `Kp4` does. The four corners all show belly, underwing ordnance and the
  ventral skull fin, so ε > 0 for each; their nose directions are up-right / up-left / down-right /
  down-left for 1 / 3 / 7 / 9, giving the four quadrants above.
  ⚠ **Read the limits.** These are *quadrants and signs, not degrees.* Getting degrees needs the
  render's field of view, and **a self-calibration attempt on this clip failed — do not repeat it.**
  The method was sound in principle: the night sky's stars are world-fixed points, they are
  detectable (200 per frame at ≥55 counts over a 25 px local background), they are genuinely sky
  rather than screen artefacts (when the camera returns to base after a hold, **181 of 200** base
  points come back within 3 px, while at +2 s / +4 s / +10 s into a hold almost none survive), and a
  pure-rotation homography `K R K⁻¹` fitted across a large swing would pin `f`. It fails on the
  *size* of the swing: base and hold frames share essentially no sky, so the fit needs the rotation
  chained frame-to-frame through the transition, and the chain loses the fast core. Frame-to-frame
  star displacement is 0.01 px settled but 17 px median and 255 px peak while slewing, and in those
  few fastest frames the matcher locks onto a spurious near-identity consensus instead of the true
  shift. The residual-vs-`f` curve is consequently flat above ~1300 px (1.759 → 1.732 px rms out to
  `f` → ∞), so `f` is bounded from below only: **wider than ~84° vertical is excluded, nothing
  else.** *What would fix it:* a slower slew is not available (it is the thing being measured), so
  either a capture that pans the camera slowly across the sky once for calibration, or a
  known-geometry object in frame — the aircraft's own wingspan from the mesh at the known shipped
  `dist` would do it directly, which is the cheaper route and needs no new footage.
  `Kp8` is the weakest of the nine: at a near-vertical elevation the azimuth is degenerate, so
  "directly below" rests on the plan-form silhouette being unforeshortened plus visible underwing
  ordnance (occluded from above), not on the nose-direction solve. One take, one aircraft — the
  layout is a per-key constant so that is fine for the table, but do not read distances off it.
  ⚠ **Correction, 2026-08-04: `Kp0` is rudder-left, not a camera view.** The 2026-07-30 cockpit
  session read it as a second 45°-underside-front view alongside 7 and concluded "the original binds
  0; we bind none" — that was a misattribution, and the *camera* half of it is withdrawn. Our
  omission of `Kp0` from `Views[]` is therefore **correct** and needs no change; the camera set is
  `Kp1`–`Kp9`. (Whether `Kp0`/`Kp.` should drive rudder at all is a separate input question this
  entry does not own.) The underside-front position stands for 7 on its own.
  (b) **Motion is wrong in kind, not just speed** — ours snaps both ways (`ApplyFixedView` has no
  smoothing branch at all). **Measured 2026-08-04 from the `CAP-07` scripted re-take, 16 transitions
  (a press and a release for each of the eight moving keys).**
  ⚠ **The "ease reads linear" claim this entry carried is WRONG — the ease is exponential.** Sky
  travel was tracked as the cumulative frame-to-frame displacement of matched star points, which is
  a monotone proxy for camera rotation and needs no FOV. Normalised, the profile is heavily
  front-loaded: **33% of the travel in the first 10% of the move, 93% by the halfway point.** Fitting
  `v(t) = 1 − e^(−kt)` gives rms **0.012–0.080** against **0.37–0.50** for a linear ramp — the wrong
  model by a factor of 6–40, on every one of the 16 transitions. A smoothstep is worse than linear.
  - **Rate `k` = 7.50 ± 1.62 /s on the press, 7.70 ± 3.20 /s on the release** (wall seconds) — the
    two agree well inside their spread, so **the ease is symmetric out and back**, which is the one
    part of this entry's original claim that survives. 90% of the way in ~0.30 s wall.
  - ⚠ **Those are WALL seconds and the original's clock runs fast (k = 1.390, `FINDINGS.md`).** In
    sim seconds the constant is **≈ 5.4 /s**, 90% in ≈ 0.43 s. Implementing 7.5 would run the ease
    39% quick — the same trap `BL-148` documents for the stall blink.
  - **The form is exactly the smoothing our chase camera already uses** — `pos += (target − pos)·k·dt`
    (`CamSmooth` 8 /s, `CamRotSmooth` 7 /s) — so (b) is a matter of routing `ApplyFixedView` through
    that existing law rather than inventing an ease curve. Our hand-picked 8 /s is in the right
    region but is a *wall*-rate; the measured sim-rate is ≈ 5.4 /s.
  - ⚠ **`k` is a lower bound, the shape is not.** The tracker undercounts the fastest 1–2 frames of
    each slew (see the calibration note in (a)), and undercounting the early, fast part biases `k`
    *down* and makes the curve look *less* front-loaded than it is. The exponential-vs-linear verdict
    therefore only strengthens under the bias; the constant itself wants a re-measure once an FOV
    calibration exists and the rotation can be integrated as an angle rather than a pixel proxy.
  (c) Distance: resolved — the fixed views take the per-plane shipped distance with the chase
  camera (`CameraController`).
  (d) **Combined keys ADD as numpad-direction vectors — MEASURED 2026-08-04 from the `CAP-08`
  scripted combo sweep, 14 staggered combinations.** Ours has no concept of this at all: `ActiveView`
  is a single-view selector that takes the first array match, so it can only ever return one of the
  eight positions. That is not a near-miss — the original reaches positions our code cannot express.
  - **The second key is never ignored.** In all 14 steps the silhouette after adding the second key
    differs from the first key's own settled silhouette at mask IoU **0.096–0.530**, against a
    repeatability floor of **0.833–0.978** measured from the same key held alone in two different
    steps (7 such pairs, up to 90 s of hand-flown drift apart). Nothing is close to the floor, so
    "the first key wins while it is down" is refuted outright.
  - **The four OPPOSITE pairs return the camera to the base chase view**: `1+9`, `3+7`, `2+8`, `4+6`
    give IoU **0.992 / 0.986 / 0.995 / 0.988** against that step's own settled base frame — at or
    above the floor, i.e. *the same view*. Independently corroborated on two of them: releasing
    `2+8` and `4+6` produced no camera motion at all (0.7 and 3.2 MAD of silhouette rate, against
    33.6–102.1 for the 14 unambiguous press-alone slews), because the camera was already at base.
  - **The two TRIPLES collapse onto their middle key**: `7+8+9` matches `Kp8` at IoU **0.891** and
    `1+2+3` matches `Kp2` at **0.955** — both inside the floor band, and both confirmed by eye
    (`7+8+9` is Kp8's unforeshortened belly plan-form; `1+2+3` is Kp2's nose-on).
  - **The eight ADJACENT pairs are genuine third positions**: their best match to *any* single-key
    view or to base runs only **0.077–0.445**, far under the floor. So `blend` is the answer to (d)'s
    question, not `replace` and not `ignore`.
  - **One law explains all fourteen with no exceptions.** Treat each key as its 2-D offset from `Kp5`
    on the numpad grid (`Kp8`=(0,+1), `Kp6`=(+1,0), `Kp9`=(+1,+1), …); **sum the offsets of the held
    keys; the resultant's direction selects the camera position, and a zero resultant is the default
    chase camera.** Opposite pairs sum to (0,0) → base ✓ (4/4). `7+8+9` sums to (0,+3) ∥ `Kp8` ✓ and
    `1+2+3` to (0,−3) ∥ `Kp2` ✓. Adjacent pairs sum to directions halfway between two keys, which
    are not any key ✓ (8/8).
  - **The route does not pass through base**, confirming the claim this entry already carried: across
    each add-second-key transition the silhouette's distance from the settled base view never dips
    below the nearer endpoint. The camera goes straight from the old combined position to the new one.
  - **The combined position is a settled position, not a moment in transit.** Median silhouette rate
    over the last 1.2 s of each hold is **2.1–19.6 px/s** against a no-key baseline of **6.1** and
    slew peaks in the hundreds; the elevation over baseline is the pilot's own manoeuvring, which
    moves the A-alone windows just as much (4.5–38.2).
  ⚠ **Read the limits.** (i) **The law is confirmed where it is testable and merely unfalsified
  elsewhere.** Mask IoU certifies *identity* but saturates into noise once two views differ by more
  than roughly a quadrant, so it cannot say *which* intermediate position an adjacent pair reaches —
  the 8 blends confirm only "not any key, not base". What carries the law is its six *exact*
  predictions (four cancellations, two triples), all six of which hold. Pinning a blend's actual
  azimuth needs the FOV calibration (a) is still missing. (ii) **Mid-ease interrupt on RELEASE is
  still untested** — the rig leaves a 3.5 s gap, so every ease-back completes before the next press.
  What *is* tested is a mid-ease *press*: the triples' third key lands 15 ms after the second, while
  the camera is still slewing, and the endpoint is exactly the held-set's resultant — so an arriving
  key re-targets rather than restarting. (iii) One take, one aircraft; the layout is a per-key
  constant so that is fine, but do not read distances or degrees off it.
  *Method:* `analysis/numpad-combo-views/` (`pass1` → `sync` → `events` → `decide` → `match` →
  `settle` → `stills`). The clip is VFR (frame intervals 16.67–41.70 ms), so everything is placed on
  PTS, never `frame/fps`. The rig's tooltip did not survive the capture, so the log was aligned to the
  video by detecting slews and matching the schedule's asymmetric cadence: **video_t = log_t + 3.978 s**,
  26/28 anchors within 350 ms at sd 27 ms, against 15/28 at sd 54 ms for the press/release alias.
  Both clocks are wall clocks, so the sim-clock factor does not enter.
  (e) **No gamepad binding existed in the original** (a right-stick/right-stick+modifier scheme would
  be invention) and **5 is unbound — now measured, 2026-08-04, not assumed.** Held for 3.5 s in the
  `CAP-07` re-take, the frame deviation from its own pre-press baseline is **0.310**, *below* the
  0.348–0.360 a no-key stretch of the same length scores, and against 2.5–10.5 for every key that
  does move the camera. Kp5 does nothing. Matches today's deliberate omission
  (`CameraController.cs:51-61`), so no code change — this line is now evidence.
  (f) **Numpad + / − trim camera distance slightly** — wholly new, unimplemented; the user already has
  video evidence for this one.
  *Fix shape:* a rebuilt `Views` table (the layout in (a)), an eased position/orientation update on
  top of `CameraController`'s existing per-plane radius, and the +/− trim as a new input. (d) is no
  longer a state machine to design: replace `ActiveView`'s first-match selector with a **sum of the
  held keys' numpad offsets**, map the resultant direction onto the (a) layout, and treat a zero
  resultant as "no fixed view" — the existing ease then carries the camera there, and the
  no-snap-to-base behaviour falls out for free because only the target changes.
  What remains open is (b)'s constant (wants an FOV calibration) and (f)'s +/− trim.
  ⚠ **`CAP-07` took two takes; the first is rejected and must not be re-analysed.** In
  `CAP-07 Numpad 1,2,3,6,9,8,7,4.mp4` the presses overlap: 10 camera transitions for 8 keys in
  20.9 s, with direct position-to-position lerps that never pass through base, so only 6–7 of the 8
  holds come to rest and the filename's key order cannot be mapped onto them one-to-one. The usable
  take is `CAP-07 scripted Run.mp4`, driven by `analysis/capture-rigs/NumpadViewSweep.ahk` — one key
  held alone at a time with a return to base between, and a `sweep-log.txt` that timestamps every
  press, so key windows are read from the log rather than inferred from motion.
  **(b) is still open even on the good take.** The ease is now measurable in principle — every move
  does start from a settled base — but no ease law has been fitted, because a per-frame camera angle
  needs a field-of-view calibration this clip has not been put through. All nine holds *do* settle:
  frame-to-frame motion over the last 1.2 s of each is 0.055–0.278 against a 0.090 baseline.
  ⚠ **Traps.** (a) **Only `--view=` is machine-verifiable** — live held-key input cannot be scripted
  here, so any fix to (b)/(d) is correct-by-construction only until played; do not close this off a
  passing `--view=` capture alone. (b) **The fix is not a symbol swap.** The corners gain a fore/aft
  term they do not have today and every above-the-aircraft view disappears, so recode the table from
  (a) rather than permuting the existing rows. And the table above is quadrants, not degrees — the
  exact azimuth/elevation still wants an FOV-calibrated solve. (c) Don't retune
  the chase radius here in isolation — the fixed views and the chase camera share one number in
  `CameraController` by design; a fix landing only in one place desyncs the two cameras again.
  (d) The combination law is pinned by six exact predictions but the eight blended positions are
  only bounded, so **do not quote a blend's azimuth** as if it had been measured, and re-check the
  implementation against the four cancellations and the two triples — those are the cases with a
  right answer to check against.

- `BL-255` `[Feature]` **A third main view — "nose view" — exists in the original and we do not have it**
  (user, 2026-08-04, while handing over `CAP-17`). Alongside cockpit and third-person there is a
  view with **no cockpit drawn, the camera sitting at the front of the plane, and the same instrument
  set as third-person** (the free-floating ALT/MPH/GUNS/ROCKETS dials, not the cockpit panel).
  `CAP-17 C2 south.mp4` is filmed in it throughout and is the reference footage — the dials sit at
  the third-person positions (altimeter hub ≈ (1092, 412), speedometer ≈ (1092, 2148) at 2560×1440,
  mirror-symmetric about screen centre) over an otherwise unobstructed forward view.
  Unknown and not investigated here: which key selects it, where it sits in the cycle, and the exact
  eye offset along the nose. Distinct from `BL-150`, which is about the *held-numpad* views around
  the aircraft, not the main view set.

- `BL-260` `[Feature]` `[Blocked: camparam decode]` **The death and flyby cameras stay gated on a decode; two crash-camera fields unwired**
  (partial-close 2026-08-05: the crash camera and look-behind landed — `docs/HISTORY.md`).
  `CamParams.cs` parses all four authored `camparam.zrd.json` blocks; still dormant: the **death
  camera** (`death_interval/z/x/alt/min_alt` — placement magnitudes with unknown axes, and the
  engine has no distinct shot-down state to attach it to) and the **flyby camera** (12 fields
  describing a re-siting roadside pass). What gates both is the trigger and the axis assignment,
  and both live in `crimson.exe`'s camera path — not in footage. Also unwired on the landed crash
  camera: `crash_elev` 40 (duplicates `crash_y` 45's vertical role) and `crash_chord_y` 1000 (units
  unknown). ⚠ Do not try to separate the implied 56° from 53° look-down by measuring frames: an
  angle off footage cannot confirm a decode (`docs/verification.md` DET-12). A capture can still
  show what the death and flyby shots *look* like, which is worth having once the fields are
  decoded; it cannot supply the field values. Details and the
  decoded fields: `docs/formats/camparam.md`.
  ⚠ Trap: near-matches between authored fields and hand-picked values are suggestive, not
  decodes — wire nothing on one coincidence; each remaining camera waits for its capture.

- `BL-266` `[Research]` `[Owed-playtest]` **Plane wobble: residual decode questions after the
  wiring landed.** The oscillators are wired (`ShakeDefs`/`PlaneShake`, visual-only roll on the
  plane node; law and measurement in [`docs/formats/shakes.md`](docs/formats/shakes.md) and
  `analysis/gun-wobble-shake/`). The dressing behind the shake is a decoded camera
  random-walk (`crimson.exe`), not the remake's dated sawtooth — details below.
  **Resolved (landed on `main`):**
  - **(a) made faithful** — 2026-08-19 the fire source now IS the original's random-walk
    accumulator (`PlaneShake.FireBullet` steps `Walk += (rand−0.5)×2·(factor×caliber)·2.0·6.2832·1.2`
    = uniform ±7.54·(factor×caliber)/shot, wep40 ±2.11e-2 rad, decoded from `FUN_0042be10`; decayed
    by the authored `damp` τ≈80 ms). Merged to `main` (`eba69782`, branch experiment `bl266-random-walk`).
    **`GunBuzzKickScale` (default 1.0 = faithful) is the one tune knob — dial it, never
    `magnitude_factor`.** The `camera+0x24` consumer traced NEGATIVE (2026-08-19) — that negative is
    the FIRE block only; the high_speed finding below (same `FUN_0042be10` writer on block 4,
    `camera+0xd4/+0xd8/+0xdc`) shows the mechanism is live, so the fire gap is a **mechanism/law
    mismatch, not a render-pipeline loss** — this port is the first real feel of the kick law.
    (The old approach-(B) suspects are also settled: fire-rate is one round per tick at authored
    `FIRE_RATE` (8.0 for wep_40) — the "12–13/s" was a redraw-window artifact — and 60 fps
    pose-interpolated render loss tested NEGATIVE.)
  **Still open, all data/fidelity questions:**
  - (b) the impact sources' per-event quantities are stand-ins declared TUNE (gun hits reuse
    caliber, rockets use armor damage) — a being-hit capture pins them.
  - (c) the `ON_CALL` `small/medium/large` `damage_shakes` defs stay unwired — unknown caller,
    likely script/set-piece.
  - **(d) high_speed shares the gun's random-walk accumulator — decoded, engine port now owed.**
    The 2026-08-18 excess-over-gate correction (`(speedRatio − gate)/quotient`, `554edcee`) returned
    the dive rattle to ~zero at rated max, but the ported gun buzz (~7× louder) exposes it as ~6× muted
    vs the original's dive. **2026-08-19 the binary settled the open hypothesis: the original drives
    `high_speed` through the SAME random-walk accumulator as the gun** — `FUN_0048c470` (per-frame
    player updater) reads `camera+0xec/0xf0` (`min_speed`/`magnitude_quotient`) and calls
    `FUN_0042c070(4, mag)` = the identical `FUN_0042be10` accumulator the `fire_bullet` path uses
    (component index 4 vs 0, kicking 3-axis accumulators `camera+0xd4/+0xd8/+0xdc` per frame). So the
    original is NOT the remake's damped sawtooth — it is a second random-walk accumulator fed by the
    existing excess-over-gate `SetSpeedRatio` law. That mechanism mismatch (sawtooth vs random-walk) is
    the root cause of the ~6× muted dive. Trace: `analysis/gun-wobble-shake/FINDINGS.md` (high_speed
    section) and `docs/formats/shakes.md`. **Open/fidelity action:** port `PlaneShake`'s `_speed` path
    to a `_fire`-style random-walk accumulator (tune `GunBuzzKickScale`-equivalent knob), then playtest
    the dive against the original clip.
  - **(fidelity) judge the port, then dial.** Playtest owed: fly the merged build and judge
    `GunBuzzKickScale` (1.0 default = faithful step) against the original clip before touching it.
    Two honest caveats: the random-walk **decay model (τ≈80 ms) is an engineering guess, not a
    decode** (the original `camera+0x24` consumer is negative — but that negative is the FIRE block
    only; `high_speed`'s accumulator is at `camera+0xd4/+0xd8/+0xdc`, a different block, see (d) above);
    and with the buzz now ~7× louder, the quiet `554edcee` dive rattle reads ~6× softer than the gun
    because the engine's `_speed` is a sawtooth where the original is a random-walk — track the (d)
    engine-port + playtest.
  ⚠ Traps: `SHAKES_CAMERA` is NOT the fire-path shake mechanism — its sole carrier among all
  48 weapons is `wep_26` "FW", a zero-damage scripted fake weapon (a scripted detonation-shake
  marker); the fire path is the unflagged `fire_bullet` source. And the near-match trap: several
  magnitude candidates coincide with authored constants — wire nothing on one coincidence (the
  caliber law stood because the candidates separated by an order of magnitude each way).
- `BL-420` `[Research]` **Decode the original's per-view base FOV from `crimson.exe` and record it under `docs/org/` — the engine holds a single 62° assumption that the binary refutes.** The original's camera projection has **exactly two base horizontal FOVs, 60° and 80°, both stored in radians as half-angle constants** (`1.0471976` = `92 0a 86 3f` and `1.3962634`), and **which one applies is gated per-camera-mode** (live mode at `camera+0x14c`, selected in `FUN_0042b660`): mode **6** → 80° (`FUN_006024d9`), every other mode (0–5, 7, 8, 9) → 60° (`FUN_00602508`). Modes 6 and 7 are the only two first-person views (both set the `DAT_009fd17c` first-person flag via `FUN_004e7100`, both route through the first-person placement `FUN_0042d980`, neither uses chase-position math — `FUN_0042dc20`/`FUN_0042c5c0` dispatch). So the three named views resolve definitively: **3rd Person / chase = 60°; Cockpit view = mode 6 = 80°; Nose view = mode 7 = 60°**. The cockpit/nose assignment is pinned by a direct render gate: `FUN_0049fb00` (the per-frame player render, sole caller `FUN_004a0220` = main tick) draws the cockpit interior model `cockpit1` (`DAT_0071c314`) **only when mode == 6**, so mode 6 is the interior cockpit view (80°), and mode 7 is the no-interior forward view (60°). The two first-person views also share the **same camera position** — both place the camera at the plane's `cockpit_camera` marker (`DAT_0071c328/32c/330`), so there is **no separate nose-camera offset**; mode 7 differs only in not drawing the interior/hull, not head-looking (fixed forward), and being 60°. The constants are **horizontal**; `FUN_006024d9`/`FUN_00602508` aspect-correct to stored vertical via `atan(tan(H/2) · (16:9)/(4:3))` → 60°→46.8° vertical, 80°→64.4° vertical. The project's current single **62° vertical assumption does not exist in the binary** — the 62°-in-radians constant `1.082104` (`63 82 8a 3f`) is absent, so the assumed number is unsupported and the correct base is 60°.
  *Evidence:* ghidra-mcp read of the open `crimson.exe` (`/crimson.exe`): `FUN_0049fb00` (player render; draws `cockpit1` `DAT_0071c314` only when mode==6 via `FUN_004cca30(x,1/0)` around the interior draw), `FUN_0042b660` (mode gate), `FUN_00602508` (60° H-FOV; writes `_DAT_00a1eff0`/`_DAT_00a1eff4`), `FUN_006024d9` (80° H-FOV, mode 6), `FUN_0042b570` (frustum/projection, contains `0.5235987755982` = 30° = 60°/2), plus the 60°/80°/50.0/2.5 constants side-by-side at the data table `0060409c`. FOV is stored in radians (anim loader `FUN_00502da0` converts degrees→radians via `0.017453292`). The `0x3f860a92` 60° literal is also used by `FUN_0049d940` (player aim camera) and `FUN_004a0220`. Camera object is `DAT_0064ef78`. Placing the camera: both first-person modes run the same placement `FUN_0042d980`, which sets the camera to `plane_pos + plane_rot · (DAT_0071c328,32c,330)`, i.e. the plane's `cockpit_camera` marker offset (bound in `FUN_00473480` from the `cockpit_camera` node; default fallback `DAT_0075d1b8/bc/c0` = `(0,0,0)`). Plane-model `cockpit_camera` node translations (decoded from `extracted/C1/... planes/nodes.json`) put the camera on the fuselage centerline a bit above the local origin — default fighter `player_pfighter`: `(0, +0.75, −0.2)` — with +Y up, ±X the wingspan (ailerons at ±63, elevators/tail at −Z ≈ −37), so +Z = nose/forward and the marker is centered, ~0.75 up, marginally aft of the origin. There is **no `nose_camera` node or per-mode offset** — mode 7 reuses the cockpit_camera point. The `cam_anim` ZAN cockpit sequence (`player-gi_1stperson`) carries no FOV (it shows the interior/hides the plane via `cockpit1`/`camera1`), so the base FOV is not authored in `.ani` data.
  *Fix shape:* **the decoded facts landed as [`docs/org/cameraViews.md`](org/cameraViews.md) (2026-08-18), and the mode-6/mode-7 first-person half of the model landed in code** (`PLAN-cockpit-view.md` A3): `CameraController.HorizontalToVerticalFovDeg` renders Cockpit at 80° H and Nose at 60° H, both aspect-corrected off the live viewport, deliberately scoped to those two new modes only (Decision 3, "new modes only") and never touching the engine's 62° global. **What remains is the EXTERNAL half.** `GameSession.cs:475`/`:2624` and `Launcher.cs:490` still write the single 62° vertical global to every chase/fixed-numpad/back/pad-look/crash camera. Migrating those three sites to the decoded 60° horizontal base (with the aspect-corrected 46.8° vertical this page already pins) is the remaining work, and it unsettles two judgements made against the current 62°: `PLAN-overcast-match.md:1463`'s overcast sky match and `docs/org/tracers.md:258`'s tracer calibration. Carry that warning into whichever session does the migration — both need re-judging after the base FOV moves, not just re-measuring against the same footage.
  *⚠ Traps:* (i) **The two `CAMERA_STATE`/`CAMERA_FROM_TO` functions (`FUN_00502da0`, `FUN_00503e70`) are animated/in-script FOV changes only (`.ani` H/V_FOV events) — not the base per-view FOV; do not wire the engine's base FOV to them.** (ii) **The 80° is attached to camera mode 6 specifically, not "first person" generally** — mode 7 is also first-person but is 60°, so gating on "is first person" alone would read the mode-7 number wrong. (iii) The `Virtual Cockpit` string is a HUD/perf/zoning label (`FUN_0059c340`), not a view — ruled out. (iv) ~~Which of cockpit vs nose is mode 6 (80°) vs mode 7 (60°) was not pinned~~ — **resolved**: the `FUN_0049fb00` render gate (`cockpit1` drawn only when mode==6) pins mode 6 = Cockpit (80°) and mode 7 = Nose (60°). The remaining subtlety is that **both modes share the same `cockpit_camera` position** (no separate nose offset exists), so "nose" is a render/head-look/FOV variant of the same camera point, not a physically different marker. (v) "62°" invariants elsewhere are the assumption being corrected, not corroboration.
  *Cross-refs:* `BL-255` (the nose view that exists in the original — the cockpit/nose FOV split this item decodes feeds that entry), `BL-150` (numpad fixed-view FOV calibration is still missing — a documented 60°/80° base + the aspect conversion is the calibration input it needs), `docs/formats/camparam.md` (chase/tuning only; does not cover FOV), `PLAN-overcast-match.md:1463` and `docs/org/tracers.md:258` (the 62° assumption to correct), `PLAN-cockpit-view.md` A3 (landed the mode-6/7 half of this model).

- `BL-432` `[Feature]` **The original selects head-look behaviour by key; CSVM infers it from which
  device moved — a recorded behavioural difference.** `OriginalScreenshots/Keybinds Views 1.png`
  binds `K` **Access Snap Look Mode** and `J` **Access Smooth Look Mode**, both free in CSVM's
  flight scheme today. The look-state byte the head-look controller reads (`DAT_0064ef68`,
  `PLAN-cockpit-view.md`'s decode of `FUN_0042d010`) is a mode selector with three values — `0`
  snap, `1` free-look, `2` padlock (`BL-399`) — that the original's player flips explicitly with
  these two keys. `PLAN-cockpit-view.md` C21 instead infers the mode from the input source: the
  numpad snap cluster snaps, the mouse/right stick pans smoothly, and both are live at once rather
  than one active mode at a time. That is a genuine behavioural difference, not just an unbound key:
  the original's player cannot free-look while snap is the active mode (or vice versa), and CSVM's
  player always can.
  *Fix shape:* either wire `K`/`J` as an explicit mode toggle gating which input path
  `HeadLook.Step` honours that frame, or judge the device-inferred behaviour as the better port and
  record why. Build beside `BL-399` — it is the same byte's third state.
  *Cross-refs:* `BL-399` (padlock, the byte's third state), `PLAN-cockpit-view.md` C21 (`HeadLook`,
  `src/Flight/HeadLook.cs`).

- `BL-433` `[Feature]` **Numpad `+`/`−` are unbound; the original uses them for the chase camera's
  zoom, which CSVM has no equivalent of.** `OriginalScreenshots/Keybinds Views 2.png` labels the
  pair **External Camera Zoom In/Out** — decoded as keys `0x43`/`0x44` moving a stored zoom value at
  `2·dt`, clamped `[0, 1]`, smoothed at `1.5`/s (`PLAN-cockpit-view.md`, "What the data actually
  ships"). `BL-150`'s own item (f) records the same pair, independently measured, as a "camera
  distance trim" — read together, that trim IS this zoom axis, not a separate control. The
  head-look controller's own center key (`0x3e`) also zeroes this same zoom value in free-look, so
  the two features share one piece of state.
  *Fix shape:* one zoom axis bound to numpad `+`/`−`, driving `CameraController`'s chase distance
  with the decoded rate/clamp/smoothing; the center key's zero-the-zoom behaviour rides along once
  `HeadLook`'s center path reaches the chase camera (`BL-435`).
  *Cross-refs:* `BL-150` item (f) (same control, measured independently), `BL-435` (the center-key
  zero), `PLAN-cockpit-view.md` (constants, `DAT_0064ef30`/`38`).

- `BL-435` `[Feature]` **The original drives the chase camera through the same head-look controller
  as the cockpit views; CSVM's chase view has no look-around at all.** `FUN_0042c7f0` (the chase
  placement dispatcher) calls the identical `FUN_0042d010(0xbfc90fdb, 0)` that
  `PLAN-cockpit-view.md` C21 already ported as `HeadLook` — same states, same snap table, same
  2 rad/s pan, same smoothing rates — with its elevation floor at `−π/2` instead of first person's
  level floor, so the chase
  camera can look down as well as up. The original's numpad snap cluster (`Kp1`-`Kp9`) plus
  `F9`-`F12` **External Camera** keys plus `F7` **Access Chase View** (menu label,
  `OriginalScreenshots/Keybinds Views 2.png`) are this same mechanism, not a separate feature.
  ⚠ **This is the mechanism behind `BL-150`'s numpad "fixed views."** `BL-150`'s own `CAP-07`
  measurement found the numpad views compose as a SUM of each held key's 2-D offset from `Kp5`, with
  a zero resultant giving the default chase pose — exactly this controller's snap-direction
  composition, applied to the chase camera's `−π/2`-floored instance rather than a table of nine
  authored poses. `BL-150`'s rebuild should therefore build look-around (this item), not nine
  hand-placed camera positions.
  ⚠ **`F7` conflicts with CSVM's own debug binding.** `docs/controls.md` already records `F7` as
  unassigned in CSVM's flight scheme specifically because `docs/org/cameraViews.md`'s correction
  identifies "Access Chase View" as the mode-9 FLYBY, not the following chase — a contradiction
  between the menu label and the decoded behaviour nobody has settled. Resolve which behaviour the
  `F7`-labelled binding actually maps to before choosing a CSVM key.
  *Fix shape:* reuse `HeadLook` (`src/Flight/HeadLook.cs`, C21) on the chase camera with
  `PitchFloor = -π/2` instead of building a second controller; the snap cluster becomes
  `CameraController`'s numpad table per `BL-150`'s law once that item's rebuild lands.
  *Cross-refs:* `BL-150` (the fixed-view numpad table this supersedes as a mental model), `BL-433`
  (the same F9-F12/zoom cluster's `+`/`−` half), `PLAN-cockpit-view.md` (⚠ table row 2, C21
  `HeadLook`), `docs/org/cameraViews.md` (the F7/flyby correction).

- `BL-436` `[Tuning]` `[Owed-playtest]` **The cockpit view's whole feel is unjudged at the controls
  — one sitting owes seven separate decisions `PLAN-cockpit-view.md` made without one.** (a)
  `PlaneBuilder.InteriorScale` (B11) is a declared TUNE, framed static (0.04, "the panel ~0.7 m
  ahead of the eye") and never flown. (b) Head-look feel (C21): the snap directions, the 2 rad/s
  free-look pan rate, and the elevation/azimuth smoothing rates (3.0/5.0 per second) are all
  decoded constants, none flown. (c) The azimuth-sign port decision (C21): the original's two input
  paths disagree on sign and CSVM picked one convention for both (positive = left) — confirm it
  reads right rather than backwards. (d) The autohead sub-cap port decision (C22): only the
  plane-local X/Y velocity drives the lean, the forward (Z) component dropped before scaling, a
  port choice made without decoded evidence either way. (e) The damaged-over-cockpit engine-sound
  precedence (D31): when both the damage swap and the cockpit swap are live, damaged wins — an
  evidence-gapped port decision, no shipped def authors the conflicting case. (f) Wobble in first
  person against the original: the cockpit camera inherits the plane node's wobble by riding the
  drawn pose (A2, `docs/org/shakes.md`) — compare amplitude and character in the cockpit at the
  controls against the original's own cockpit view. (g) The Nose head-look confirm:
  `PLAN-cockpit-view.md`'s ⚠ table row 2 retired `docs/org/cameraViews.md`'s old "head fixed in
  Nose" reading in favour of "head-look runs, only autohead is gated" — fly Nose and confirm the
  free-look is really there, since the retired reading may have been a live impression rather than
  a misread decompile.
  *Fix shape:* one cockpit sitting across a couple of airframes covers all seven; each is a
  judgement call, not a re-decode.
  *Cross-refs:* `PLAN-cockpit-view.md` (every decision above, by wave: B11, C21, C22, D31), `BL-391`
  (engine level, kept separate from (f)).

## HUD & UI

- `BL-113` `[Tuning]` `[Owed-playtest]` **Compass tape** — `TileOverscan` / `RimGain` / the nearest-tick look remain TUNE
  (north = −Z is now confirmed against the original, 2026-07-30 — do not reopen).

- `BL-181` `[Tuning]` `[Blocked: menu hub]` **Marker HUD + scoreboard layout is a provisional pass, not a fidelity sign-off — pending
  the menu hub.** Playtested 2026-07-30
  (`./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`): `MarkerHud`/`StuntScoreboard` placement,
  fonts and distance units "work for now." The verdict is explicitly contingent on the menu hub not
  existing yet — once it lands, distance units, font choice and scoreboard layout may need to match
  its chrome rather than today's placeholder styling. Blocked on the menu-hub milestone, not on data.
  *Fix shape:* re-review `MarkerHud.cs`/`StuntScoreboard.cs` placement once the menu hub UI exists,
  against the hub's own type scale/units rather than in isolation.

- `BL-296` `[Feature]` **Per-player ActionMap: named actions over the raw key/pad polling, as the
  rebinding seam.** Every control is hard-polled today (`Input.IsKeyPressed`/`IsJoyButtonPressed`
  scattered across `FlightController` (~a dozen bindings), `MenuInput`, `SpectatorCamera`), with
  per-player device routing via `PadDevices`/`UseKeyboard`; `docs/controls.md` is the binding
  record. The shape (decided with BL-295, 2026-08-06): a named-action indirection — helpers like
  `FirePressed()` become `actions.Held(Action.FireGuns)` resolved by a per-player `ActionMap`
  (player 1 keyboard+pad, others pad-only), persistable so a rebinding UI can edit it later. Godot's
  built-in `InputMap` supports runtime rebinding but is app-global, so the per-player layer stays
  ours either way. ⚠ *Traps:* NOT an event/message bus — fire is a held control on the 60 Hz
  fixed-tick sim, edge detection lives in the consumers (`FireControl`), and events would break
  scripted `--det`/`--hold` runs; the polling *sites* are the seam, `FireControl` itself never
  changes (it consumes `FireInputs` booleans). Update `docs/controls.md` when this lands.

- `BL-351` `[Feature]` **Generalise the targeting HUD: target-cycling keybindings for the
  original's target classes.** Requested 2026-08-14 alongside M4 H22 (which extends the VS
  targeting elements to AI enemy planes but picks the target automatically). The original ships
  several bindings to cycle the tracked target by class: enemies/objectives, allies, and
  non-aircraft (ground/sea vehicles, turrets, zeppelins). Wanted: the same class-cycling on our
  targeting HUD — per pane in splitscreen, reusing the VS/H22 drawing elements unchanged, only
  the selection source generalises. Ground work: pull the original's exact bindings and cycle
  order from its input config/manual before designing ours; wire through whatever input seam
  exists when this lands (`BL-296`'s ActionMap if it has landed, hard polling if not).
  *How you'd know it worked:* in a session with AI planes, a zeppelin and turrets, the target
  key cycles hostile aircraft; the non-aircraft key walks the zeppelin and turrets; each pane
  tracks its own pick. Depends on H22's target-tracking plumbing; `docs/controls.md` gains the
  bindings when it lands.

- `BL-409` `[Feature]` **The load screen shows a plain panel, not the original's artwork.** A
  session build stalls the frame loop for several seconds, so both interactive paths into one (the
  launchscreen's Fly and an Instant Action Restart) draw `UI/LoadBoard` first: the shared board
  style, the chapter and mode named, no progress. The original draws a composed screen, and every
  piece of it sits in `extracted/rimage/`, which we already read for the HUD font and the impact
  pipper, so no new asset pipeline is needed. The background is `loadframempt.png` (800×600: riveted
  metal border, black interior, filmstrip column, winged skull at bottom centre), with
  `loadframempt2.png` a second variant. The load bar is `prog_blk.png` and `prog_red.png` (236×54
  each, the same strip of six round lamps, unlit and lit), drawn part-filled as the build proceeds;
  `prog_blkload.png`/`prog_redload.png` (338×28) and `blkprog.png` (297×14) are other bar styles the
  game ships. The three photos are shipped artwork rather than captures: `mp-shotdown`, `mp-crash`,
  `mp-cannon`, `mp-zepdown`, `mp-gasbag`, `mp-torpedo`, `mp-flagcapture`, `mp-flagreturn`,
  `mp-dangerzone2` (about 230×190 RGBA, sepia, the polaroid border and tilt baked in), named for
  objective and event types, with per-mission campaign sets alongside them (`nw-m1*`, `rm-m4*`,
  `ha-m2*`, `hw-m5*`, `mh-m3*`).
  *Decoded, see [`docs/org/loading-screen.md`](docs/org/loading-screen.md).* The screen is a `zrdr`
  dialog (`extracted/zrdr/Loading.zrd.json`, 81 of them) named by `sprintf` from `loading_i%d%c` /
  `loading_c%d%d` / `loading_m%d%c` (`0x0062950c`, `FUN_004a1910`). **There is no per-mission photo
  choice:** every Instant Action dialog carries the same three, `MP-shotdown`, `MP-crash`,
  `mp-dangerzone2`, and the environment digit selects nothing; only the mission letter varies, and
  only the text (`a` DOGFIGHT AN ACE, `d` SQUADRON, `s` STUNT FLYING, `z` ZEPPELIN RUN). Multiplayer
  varies its pictures by game mode, campaign carries none. **The bar is not measured:**
  `FUN_004a2100` is called at 16 fixed points with a literal fraction (0.01, 0.02, 0.04, 0.07, 0.10,
  0.20 … 0.80, 0.90), is monotonic, and never reaches 1.0; the repaint (`0x005c3d40`) fills
  `floor(bitmapWidth × fraction)` pixels of `prog_red` over `prog_blk`, a pixel clip rather than a
  lamp count. **The original does not decouple its build either:** `FUN_004a18a0` is a redraw pump
  called after each milestone and throttled to one draw per 0.1 s, so the load screen runs at 10 fps
  off the loading thread itself. A propeller cycles beside the bar at 6.0 fps (`prp0`…`prp37`).
  *The bar needs the build decoupled first.* Godot cannot draw while `GameSession.StartSession` is
  on the stack, so an animated bar needs the build to yield. The cheap route is an `async`
  `StartSession` awaiting `SceneTree.ProcessFrame` at the phase boundaries the
  `StartupProfile.Mark`/`Record` pairs already mark (`gamez`, `world`, `bind`, `anim`, `plane` and
  the rest); C# carries the `using` scopes and the early `return false` paths across a yield
  unchanged. Four things break and need handling: `Launcher._Process` ticking the capture director,
  the glTF exporter and the hitch monitor against a half-built `_session`; `_hitchMonitor.Rearm` and
  `PerfSample.Reset`, which assume the build is one block between two frames; `StartupProfile`'s
  `total = boot + Σ(phases) + rest + first_frame` invariant, which stops holding once render time
  lands inside `rest`; and the CLI path, which must gain no frames at all or `--det`, `--screenshot`
  and the goldens shift (`BeginLaunch` is already the interactive-only door).
  ⚠ *Traps:* (a) Do NOT derive the bar from measured phase durations. The original hand-assigns a
  fraction per step, and that is the shape to copy: our `StartupProfile.Mark`/`Record` pairs already
  bracket the same kind of boundary, so each gets a literal fraction and a monotonic setter. A
  measured bar is the thing that misbehaves here, because on the menu-driven path a build takes
  about 6.75 s of which `rest` is 3.63 s, so a bar weighted by the named phases alone sits near half
  and then jumps. The yield is the real prerequisite, not the attribution.
  (b) `SavedGames/<pilot>/Snap_*.png` is the scrapbook memento system
  (`MOMENTOSELECTION.SCRIPT`, the `MS_` widgets in `LAYOUT.CSV`), not this screen, and
  `loadframe.png` is a different screen again (the light route-planning frame). (c) The artwork is
  4:3 at 800×600 with the border painted into it, so meeting a widescreen window (pillarbox, crop,
  or nearest-neighbour upscale for a period look) is a taste call the binary cannot settle. (d) The
  board must stay cheap to construct: it is built on the frame BEFORE the build, so a large decode
  there just moves the stall earlier. (e) Whatever it draws, the Launcher frees it in the same tick
  the build returns, so it can never draw over the world's first frame or a `--screenshot` capture.
  *Still open, and prior to the work:* whether to reproduce the original screen at all. Its art is
  800×600 with the frame painted in, which is below our window and cannot be re-rendered at a higher
  resolution. Reproducing it means accepting a visibly soft screen, so the alternative is to keep a
  screen of our own and take only the mechanism (yield, milestone fractions, an animated element).
  The decode above serves either choice.
  *Cross-refs:* `UI/LoadBoard.cs`, `Launcher.BeginLaunch`/`RunOwedLaunch`, `Utils/StartupProfile.cs`,
  [`docs/org/loading-screen.md`](docs/org/loading-screen.md), `docs/architecture.md`.

- `BL-431` `[Feature]` **The cockpit interior's `gauges` subtree (39 meshes, `cockpit1`) renders as
  static geometry — the needles never move, and two interior warning lamps ship parked hidden.**
  `PLAN-cockpit-view.md` (Wave B) built the interior render but deliberately kept `GaugeCluster` as
  the only DRIVEN instrument set (Decision 1): the in-3D dial faces, bezels and panel are authored
  geometry with no needle animation wired to them, so a Cockpit-view capture shows the screen-space
  HUD dials drawn over a static 3D panel holding a fixed pose. Two lamp nodes, `lowalt_on` and
  `stallwarning_on` (present on all 11 airframes, shipped `active: true`), are parked hidden by
  `PlaneBuilder.ParkInteriorStates`/`IsInteriorDrivenState` alongside the windshield bullet-hole
  decals — nothing lights them. Bundled here with a second, related question: whether `GaugeCluster`
  should reposition per `hud_v2.zrd`'s `POSITION_1ST` layout key when the pilot is in a first-person
  view — the HUD initializer reads distinct `POSITION_1ST`/`POSITION_3RD` layout keys from the
  archive and CSVM's `GaugeCluster` consumes neither (`docs/org/cameraViews.md`, "The in-binary
  strings expose no view-name tokens").
  *Fix shape:* drive the authored needles and the two lamps off the same telemetry `GaugeCluster`
  already reads, then decide whether the screen-space cluster retires in first person, moves to the
  `POSITION_1ST` layout, or keeps doubling up over the 3D panel as it does today.
  *Residue:* nothing is hidden for this, and nothing needs to be. The instruments were reported
  flickering, and the mechanism turned out to be the mount scale rather than the missing needle
  drive: every depth bias `SceneBuilder` emits is a fraction of VIEW DISTANCE, so mounting the
  interior at `PlaneBuilder.InteriorScale` (0.04) left one authored priority level worth about
  86 µm of depth at the panel's 0.42 m, below the float noise of a view-space transform computed
  at a chapter's world coordinates. The bezel rings that ring each instrument (`horizn` at
  priority 1 over the `dash` panel at 0) swapped winner with the panel from frame to frame. The
  interior now builds on its own `SceneBuilder` carrying `DepthBiasScale = 1/InteriorScale`, which
  restores the absolute separation the authoring assumed; the airframe, the world and all four
  plane-bearing goldens are untouched by it. What remains in a Cockpit capture is texture shimmer
  on the finest dial markings (the compass drum's ticks, the small dials' graduations) as the
  panel's projected position wobbles sub-pixel — an aliasing artifact of high-frequency instrument
  textures, not a draw-order one, and driving the authored needles will not change it either way.
  *Cross-refs:* `PLAN-cockpit-view.md` B11 (parked the states; also settles that the `gauges` child
  itself must stay visible — it is not a needle overlay). The windshield bullet-hole decals
  (`bullet1`-`bullet5`) share the same parked-state mechanism but are driven by the unrelated
  `cockpit_bulletholes` anim-def family (`docs/plans/PLAN-m3-polish-5.md:453`), not this item.
  Neither that family nor either other `PLAYER_1ST_PERSON` def (`muzzle_burst`, `player-1`'s
  `pdpanel4`/`pdpanel6`) targets any node inside `gauges`, and no runtime binds a plane's own
  subtree apart from the crash rig's narrow subset — so nothing animates the panel per frame.

## Splitscreen

Our splitscreen mode (2–4 players) has no counterpart in the original, so every rule it authored
against "the player" or "the camera" needs an explicit splitscreen verdict: generalise it, take a
nearest/union rule, or record it as deliberately single/global. This theme collects that work
(sweep of 2026-08-15). **Route fixes through the two existing seams instead of minting new ones:**
`GameSession.PlayerPositions` (nearest human) for gameplay rules that say "the player", and the
viewer set behind `ProjectilePool.Viewers` / `ScreenSize.NearestFloor` for draw rules that say
"the camera". Sim state stays global — the mission wind is the worked example
(`Session/WeatherRig.Tick`, stepped once per frame outside the per-rig loop on purpose). Splitscreen-scoped items that live with
their own system: `BL-231` (per-player pool term), `BL-296` (per-player ActionMap), `BL-299`
(MP spawn maps), `BL-301` (Dogfight tuning), `BL-314` (race countdown), `BL-351` (per-pane target
cycling).

The theme's first batch (`BL-126`, `BL-365`–`BL-376`) landed via
[`docs/plans/PLAN-splitscreen-polish.md`](docs/plans/PLAN-splitscreen-polish.md) (2026-08-15,
complete — the chrome playtest F52/`BL-126` closed it out). New splitscreen findings mint here as
usual.

- `BL-380` `[Bug]` `[Blocked: per-instance fog shader uniforms]` **Fog-zone selection stays
  player-1-only in splitscreen: `csky_fog_color`/`_range`/`_alt`/`csky_world_light` are one GLOBAL
  shader uniform set, written from rig 0's camera weather state alone
  (`Session/WeatherRig.cs:459-466`), so a pane on the other side of a fog-zone boundary from P1
  renders P1's fog, not its own.** Split out of the `BL-338` residual sweep 2026-08-15 (plan B14):
  the whiteout overlay and the deck regime are already per-rig (the same `WeatherRig.Tick` loop) —
  only the fog GLOBALS lag behind, because `ApplyFogGlobals` writes session-wide shader uniforms,
  never per-instance ones.
  *Evidence:* `WeatherRig.cs:459`'s own comment: "Driven by rig 0, because the fog parameters this
  writes are GLOBAL shader uniforms — one set for the whole session ... In splitscreen with one
  player under the deck and one over it, both panes therefore wear player 1's fog." Pre-existing
  (`SetupWeather` always wrote one global set before splitscreen existed), not introduced by it.
  *Fix shape:* per-instance fog uniforms on every fogged mesh instance, selected by whichever
  pane's camera the instance should answer to — a shader-architecture change (per-instance state
  keyed off the viewer set), not a wiring change.
  *⚠ Traps:* a second `RenderingServer.GlobalShaderParameterSet` call does not fix this — that is
  still one value for the whole process, not one per viewport. Any new `instance uniform` this adds
  to `shaders/csky_instance_uniforms.gdshaderinc` must be APPENDED, never inserted — Godot assigns
  instance-uniform slots by declaration order per shader, and the file's own header names the
  2026-07-17 `csky_fog_on` index-collision bug this ordering contract exists to prevent.

- `BL-389` `[Tuning]` **Splitscreen weapon mix needs a retune: rockets too quiet, guns too loud,
  especially four guns firing at once.** Found at the `BL-126` chrome playtest (F52,
  2026-08-15) — `FlightAudio.MixGain`/`Projectile.cs`'s pool `MixGain` (the `1/sqrt(N)` equal-power
  attenuation D31/D32 landed) reads right in isolation but the per-weapon balance under it does
  not: a 4-player Dogfight with simultaneous gunfire is too loud relative to rocket explosions,
  which read as too quiet against it. *Look for:* rocket vs. gun relative level across 2P/4P,
  specifically four guns firing together. *Fix shape:* a judgement call at the controls on the
  per-def volume terms feeding `Projectile.cs`'s `def.Volume * 0.2f * MixGain * distanceGain`
  (line ~2238) — not the `1/sqrt(N)` splitscreen term itself, which is confirmed correct.

- `BL-434` `[Research]` **Splitscreen cockpit interior/audio behaviour is unprofiled and unjudged
  past one pilot.** `PLAN-cockpit-view.md` (Decision 5) built cockpit rendering and the
  `cockpit_engine_sound` swap verified single-player-only, no further. (a) **Per-viewport interior
  draw cost is unprofiled**: each pilot's rig now carries its own `cockpit1` subtree and
  `CockpitVisibility`, so a splitscreen session with 2-4 cockpit-view pilots draws that many
  interiors simultaneously. (b) **The per-pilot `cockpit_engine_sound` swap against splitscreen's
  `MixGain` term is unjudged at the controls** — `BL-391`'s "own-ship engine loop too loud" finding
  predates the cockpit swap and never isolated the `_cp` def specifically. (c) **Today's hiding
  mechanism is node visibility on a shared plane node, not a per-viewport render flag**:
  `CockpitVisibility` hides the OWN rig's `healthy` body node, so a pilot sitting in the cockpit
  hides THAT AIRCRAFT'S body in every pane that can see it, not just their own — a cross-pane effect
  unjudged at `N > 1`.
  *Fix shape:* profile per-viewport interior cost at 4 players; a splitscreen listen for the
  cockpit-swap/`MixGain` interaction; confirm or fix the cross-pane body-hide visually at the
  controls with 2+ cockpit-view pilots in the same session.
  *Cross-refs:* `PLAN-cockpit-view.md` B11 ("Splitscreen posture"), `BL-391` (base engine level,
  the audio half of (b)), `BL-389` (splitscreen weapon mix, same playtest family).

## Missions, modes & campaign

- `BL-426` `[Bug]` **A failed stunt mission records and announces a new best time.** Seen at the
  controls: losing an Instant Action stunt run still shows NEW BEST on the wrap-up.
  **The mechanism.** `GameSession.StuntSummaryFor` (`GameSession.cs:3083-3094`) calls
  `store.RecordIfBest(key, run.Elapsed)` behind two guards and no third: the objective is
  `ZonesFlown`, and player 1 has a `Stunt` run at all. Neither asks whether the run was
  **finished**. So every end of an Instant Action stunt mission records a time, a loss included.
  A failed run then wins the comparison almost every time, because it ended early: `RecordIfBest`
  (`ScoreStore.cs:59-63`) takes any total lower than the stored one, and dying halfway round
  produces exactly that.
  **Why it is not cosmetic.** The write persists immediately to `user://stunt_scores.json`, so a
  bogus time becomes the record a later honest run is measured against and, being unbeatably short,
  can never be displaced by real flying. The damage outlives the session that caused it.
  **The shape of the fix is already in the file.** The solo path does this correctly by
  construction: `StuntScoreboard.OnRunCompleted` (`StuntScoreboard.cs:156-160`) only runs on
  completion, and the board uses `StuntMission.AllComplete` (`:98`) as its own retire test. The
  Instant Action wrap-up needs the same predicate; the two paths disagree today and only one of
  them is right.
  ⚠ **Traps.** (a) Do not gate on the mission's win/loss flag instead. Decision 10 of
  [`docs/plans/PLAN-instant-action.md`](docs/plans/PLAN-instant-action.md) has every player fly
  their own zone set with the mission ending when all of them are done, so a splitscreen mission
  can end with one pilot complete and another not; the test belongs on the run, per pilot, not on
  the mission. (b) `prevBest` is read *before* the record (`GameSession.cs:3091-3092`), so a fix
  that stops the write without touching the display would still show a stale figure. (c) The store
  is in `user://`, not the repo, so any machine that has already hit this carries a poisoned file
  that no code change repairs. Decide explicitly whether to invalidate existing entries.
  **Open question, a taste call.** Whether a failed run shows its elapsed time at all (without the
  NEW BEST flag) or shows no time. The original's own behaviour here is not decoded.
  *How you'd know it worked:* fail a stunt mission deliberately, confirm the wrap-up claims no best
  and `user://stunt_scores.json` is byte-identical afterwards; then complete one and confirm it
  does record.

- `BL-350` `[Bug]` `[Blocked: mission animations]` **Generator-spawned planes crash inside closed hangars
  (C1/M04 `--generators`, user-reported 2026-08-13).** The spawn position is decoded-correct: the
  original's launch routine (`FUN_00452450`) teleports the launched vehicle to the same generator
  node, joined by roster `group` over parked vehicles. What differs is the mission layer: the
  original plays a hangar-door-open animation (mission scripting/`WAKE_ANIM`, out of M4's scope)
  before launch, so the geometry the plane flies through is open. Ours never plays it, the door
  stays closed, and the collision sweep kills the plane on frame one.
  ⚠ **Traps.** (a) Do NOT "fix" the spawn placement — it matches the decode; the missing piece is
  the door animation, not the position. (b) Leads preserved from a partial decode: the launch also
  sets two timers (`+0xac = now + 1.5`, `+0xb4 = now + 2.5`) and an initial velocity with a
  −22.352 m/s vertical component (the zeppelin drop case). The carrier path now carries both timers:
  `+0xac` suppresses collision and AI ground blow for 1.5 s, then `+0xb4` limits the ground-blow
  response to ×0.15 until 2.5 s. (c) The launched-vehicle mechanism itself
  (parked roster planes, not fresh spawns) is a separate fidelity gap from this bug; B6's fresh-spawn
  stand-in is documented in its landing commit.

- `BL-314` `[Feature]` `[Blocked: PT-45]` **Race countdown — a rolling start on rails before the run clock
  opens.** The abreast starting grid landed 2026-08-08 (`RaceGrid`), so every pilot in a splitscreen
  stunt race now begins on one line, on one heading, at one altitude. What is still missing is the
  moment a race starts: today the clock is running the instant the world appears, so whoever's
  loading screen ends first is flying first. Deliberately split off from the grid rather than landing
  with it, because it changes a **deliberate** clock rule and that rule deserves its own scrutiny.
  Do not start it before `PT-45` has judged the grid at the controls — a countdown over a grid nobody
  has flown tunes the presentation of an unvalidated start.

  **The shape, as decided.** (a) A **rolling start**, not a full freeze: the aircraft stay
  physics-alive and moving through the count, which reads as a race start rather than four parked
  planes popping into motion, and is exactly as fair as a freeze since nobody may manoeuvre. (b) The
  countdown flight is **on rails** — a kinematic level walk of the field, driven straight into
  `_model.Reset(...)` (`FlightController.cs:649` is the existing call shape:
  `_model.Reset(pos, attitude, SpawnSpeed, throttle)`), arranged so that **GO is exactly today's
  spawn state**. Nothing is simulated during the count, so there is no sink to fight, no per-plane
  divergence, and the handoff is the pose the flight model already starts from. (c) `--det` never
  sees any of this: like the grid, the countdown is reached only through the race path, so a scripted
  run must remain byte-identical and the whole feature stays hand-flown verification only.

  **⚠ Traps — read before touching this.**

  1. **Do not derive the pre-GO setback from a speed.** Spawn speed is the mission's own
     (`PLAYER_INIT[4] × 0.1`, 18 m/s in nearly every mission), resolved per session by
     `SpawnPicker.StartState` and carried on `FlightStart`
     ([`docs/formats/spawns.md`](docs/formats/spawns.md)). It is data, so it differs
     between missions and can differ again whenever a mission is re-read; any "start N seconds back
     at the spawn speed" arithmetic therefore hard-codes one map's number into a rule meant to hold
     on all of them. The on-rails walk above avoids this by construction: it simulates nothing and
     it ends on the spawn pose whatever the speed is.
  2. **This changes `StuntMission.Elapsed`'s documented rule.** "The clock never stops" is stated
     twice and on purpose (`StuntMission.cs:109-113` on the property, `:247-249` on `Tick`) — it is
     why a mid-run crash freeze still costs you time. A countdown means the clock must not *start*
     until GO, which is a different claim from stopping it mid-run; make the distinction explicit in
     both comments rather than deleting the rule, or the next reader reads the crash freeze as
     negotiable too.
  3. **Do not simulate the count and do not freeze the sim.** A physics-alive count that is actually
     flown re-opens the sink (`FlightController.Respawn`'s start state) and diverges per plane; a hard freeze
     was rejected as the presentation this mode wants. Both are the alternatives already considered.
  4. **The instrument is a hand-flown sitting, not a screenshot.** A race grid is not photographable
     (chase-cam panes; a 60 m neighbour is out of frustum), and `--det` cannot reach this path at
     all, so an automated check can prove only that scripted runs are unchanged. Everything about
     whether the count *feels* like a race start comes from `PT-45`'s sortie.

  *Unlocked by the grid, noted here rather than promised:* race best-times become feasible once a
  race has a defined start (`StuntRace.cs`, `ScoreStore.GetBest`/`RecordIfBest`) — and would want
  their own key namespace, since a countdown makes race and solo totals diverge again.

- `BL-299` `[Research]` **Decode `net.zrd.json` as the multiplayer spawn table → the retail MP1–MP3 maps for
  Dogfight.** 45 files, one flat group each, node counts quantised by mission type (MP1→80,
  MP2/MP3→48, campaign→8), 23 distinct payloads shared across files — shape and distribution say
  *spawn table*, not patrol route (`docs/plans/PLAN-M4-ai.md` survey; its "do not build patrol on it"
  warning stands). Now there is a consumer to validate a decode against: Dogfight (`--vs`) plays
  the IA1 `dogfight_ace` list today; a confirmed spawn decode gives it the maps the original
  authored for exactly this mode. MP worlds already load (`--mission=MP1`); only their spawns fall
  back today (`SpawnPicker` warns).

- `BL-300` `[Cleanup]` **Tighter aircraft collision shapes — convex hulls per clipped region instead of
  boxes.** Pays off twice since PLAN-vs-mode A1 single-sourced the shape set: the same
  `PlaneCollider.Parts` feed the terrain sweep (close-stunt false crashes from box overhang —
  user-reported 2026-08-06) and the aircraft body (being-shot fairness, blast nearest-point
  falloff). Keep the `Relabel`/part-name contract intact — `PlaneDamage`'s "tail" arm depends on
  it (its architecture.md ⚠), and `MapStruckPart` consumes the names unchanged. The Bloodhawk's
  uncovered canard tips are the known gap to close.

- `BL-301` `[Tuning]` `[Owed-playtest]` **Dogfight (VS mode) tuning** — every deliberate v1 deferral, to be re-judged from
  `PT-43` evidence, not speculation. **Aim-assist strength settled 2026-08-13** from `PT-43`(a)/(b):
  the shipped `sticky_bullet_*` constants (decoded in
  [`docs/org/aim-assist.md`](docs/org/aim-assist.md), built by `BL-342`) read right at the
  controls — damage balance plane-vs-plane felt good and guns are now a practical kill weapon
  without rockets, a marked improvement over firing with no assist at all. No retune. Still open:
  spawn camping / spawn protection (none in v1) — **confirmed a real problem, not speculation, by
  `PT-43`(c) 2026-08-13**: every player has a fixed spawn point and camping one is very much
  viable; the fix is spawn rotation, likely alongside whatever `--vs`'s existing spawn-spacing
  logic already tracks per-pane; suicide penalty and last-damager credit (0 / none in
  v1), sudden-death overtime on a drawn time-out (draw declared in v1; `PT-43`(f) found draw
  frequency fine at the 5-kills/5-min defaults, so this stays low priority), menu-side match
  options (kill target and time limit are CLI-only), `dogfight_ace` vs `zeppelin_run` spawn
  spacing, the self-blast exemption (own rockets can't hurt you — the guns invariant applied
  consistently, not a balance call), VS HUD line/arrow sizing at 4-player panes (`PT-43`(d):
  confirmed readable and correctly edge-flipping at both 2 and 4 players, `BL-126` chrome playtest
  2026-08-15 — no retune owed; the general HUD text-scale config covers the separate font-size preference). Related,
  not absorbed: `BL-126` (splitscreen chrome, closed 2026-08-15). ⚠ The stunt race's
  abreast starting grid landed 2026-08-08 and deliberately did **not** touch `--vs` — it is
  selected only when a race exists, so Dogfight still walks the scattered `dogfight_ace` list.
  Spawn spacing here stays this item's call from `PT-43`, and copying the grid over is the wrong
  reflex: four dogfighters 60 m apart on one heading is an instant head-on merge every round.

- `BL-134` `[Feature]` **Cutscene player — the missing consumer (M04's zeppelin, `letterbox`, `CALLBACK`).**
  **This is a missing subsystem, not a bug.** The cutscene defs run because nothing tells them they
  are cutscenes. What is absent is a **cutscene player** owning camera control, the `letterbox`
  bars, scene sequencing, and the end-of-cutscene handoff to gameplay. Same dependency as the
  `CALLBACK` event kind (see `BL-035` — all 8
  dispatches are `def=camera1`, unanchored, triaged as intro-cutscene notifications). **Pick the two
  up together.**

  **⚠ Traps — read before touching this.**

  1. **Do NOT skip the cutscene defs at bootstrap.** That fix was proposed, scoped, and
     **REJECTED by the user 2026-07-22**: *"the M0x missions are campaign missions and we need those
     animations if we want to restore the campaign."* `generic_intro` ×12 and
     `mission_intro_animation` ×1 are decoded, working **assets** — the missions' authored intro
     movies. Deleting their bootstrap throws away campaign capability to suppress a cosmetic symptom.
  2. **C1/M04's pirate zeppelin flying above the overcast is an ACCEPTED artifact**, not a defect to
     work around. Do not lower the zeppelin, do not suppress the def, do not "fix" `letterbox` bars
     if they appear. Lowering it is content invention — the same trap as the C3 palms.
  3. **An 8-chapter regression is inert here by construction.** The default mission is IA1, which
     has no intro at all; no `IA1` and no `MP` mission bootstraps a cutscene def. Verify per-mission
     or not at all (`docs/verification.md` DIAG-10).
  4. **Verify by what disappears, not by what looks right** — `generic_intro` is shared across 12
     missions and may currently be driving things nobody has looked at.

  **M04 is the ready-made first test case.**

  Its data is fully decoded, so it is an end-to-end exercise for free. The symptom that exposed all
  of this: the pirate zeppelin builds, renders complete and flies — it is simply **above the
  clouds**, at y 1505→1546 while C1's opaque `cloudlayer` deck sits at y = 960 and the player spawns
  at y ≈ 110. Freecam onto it with `--freecam --chapter=C1 --mission=M04
  "--pos=-5358,1505,-2200" "--lookat=-5358,1505,-1810" --no-fog`.

  **Confirmed 2026-07-22 from the script data — the "jumps" are cutscene cuts, not waypoints.**
  The user watched it move smoothly for ~20 s, jump twice about 10 s apart, then vanish, and asked
  whether the jumps were AI waypoints. They are not:

  - `data-c1-m04-zrdr-introanm-pzep1-piratezep.zan.json` is **48 frames at exactly 1/3 s apart, a
    uniform straight line**: each frame steps a constant (−13.4, +2.1, −17.3), from
    (−5352, 1504, −1802) to (−5981, 1602, −2611) over **15.67 s** (≈66 units/s). Frame 0 and frame
    47 carry translate+rotate+scale; all 46 between are translate-only. There is no dwell, no
    branch and no waypoint structure anywhere in it — so the smooth phase is this script, and it
    simply **ends**.
  - The jumps are therefore what happens *after* it runs out, and the dispatch graph says what
    that is: `camera1-scene1` and `piratezep-scene2` **both call `letterbox`** — the cinematic
    black-bars overlay — and `scene2` also calls `pfighter11` and `open_pzeplaunchdoors`, while
    `piratezep-pzep_launch_player` calls `pz_open_hanger_doors` / `pz_deploy_hook` /
    `pz_retract_hook`. That is the M04 **intro movie**: zeppelin flies in, cut, launch doors open,
    a fighter launches.

  So each jump is a **hard cut between cutscene beats** — correct for a movie, nonsense as
  gameplay — and "then it's gone" is the last beat deactivating it. **User-confirmed 2026-07-22:**
  *"Yeah those are the cut scenes."* The same def demonstrably drives `letterbox` too, so a useful
  open check remains: **are we currently drawing letterbox bars in M04?** If so that is the same
  missing consumer with a far more visible symptom, and a good first target for the player.

  **Scope, surveyed across all 53 missions' `startanims.zrd.json`: 13 bootstrap a cutscene def.**

  | def | missions |
  |---|---|
  | `generic_intro` | 12 — C1/M05, C1B/M03, C1C/M01, C2B/M04, C3/M01, C3/M02, C3/M05, C4/M01, C4/M02, C4/M04, C5/M01, C5/M04 |
  | `mission_intro_animation` | 1 — C1/M04 (the bespoke one) |

  **No `IA1` and no `MP` mission names one** — all 13 are `M0x` story missions, so this is invisible
  in instant action and multiplayer, which is what the project defaults to. That bounds the blast
  radius neatly and explains why it went unnoticed until someone flew `--mission=M04`.

  **❌ The fix this evidence originally led to was REJECTED — see Trap 1.** For the record, it was:
  skip those two def names when the animation bootstrap walks `startanims`. Small and data-driven, a
  name check against the start-anim list rather than a new subsystem — and wrong, because it buys a
  cosmetic fix with campaign capability. Kept here so nobody re-derives it and thinks it is new.

  **Verification note that outlives the rejected fix:** verify *by what disappears, not by what
  looks right*. `generic_intro` is shared across 12 missions and may currently be driving things
  nobody has looked at, so a change here is checked by enumerating removed motion per mission. An
  8-chapter regression cannot catch any of it: the default mission is IA1, which has no intro at
  all, so the regression is inert here by construction (`docs/verification.md` DIAG-10).

- `BL-243` `[Feature]` **The original carries destruction across missions in a state log; CSVM has no log, so a
  warm instant action starts clean where the original does not.** Decoded 2026-08-02 out of
  `BL-099` — the mechanism, the user's five-step A/B in the original, and the 62-definition
  `PERSIST_LOG` list are written up in `docs/formats/anim-definitions.md` ("`SAVE_LOG` /
  `PERSIST_LOG` are the cross-mission state log"); read that before touching this. The rule:
  **every mission load applies the log, only campaign missions write it, and it commits to the save
  and survives a process restart.** `SAVE_LOG ON` (568 defs) puts a definition in the log at all;
  `PERSIST_LOG ON` (62 defs, a strict subset, all fixed world scenery) additionally carries it
  across mission boundaries.
  **Scope of the divergence is narrow.** CSVM builds every session from the bootstrap, so we match
  the original for campaign missions and for a cold instant action; we differ only for an instant
  action loaded after a campaign mission **in the same process** — the exact case that produced
  `BL-099`'s burning fuel depot. There is no campaign flow yet, so today this is unobservable in
  practice.
  **What implementing it would take:** a chapter-scoped store of `PERSIST_LOG` definition states
  that outlives `Launcher.ReturnToMenu`'s teardown (which currently QueueFrees the whole session
  subtree), applied after the `RESET_STATE` bootstrap and before `zepstate`/`startanims`/the `.gw`,
  written only on a campaign-mission exit. Because `fuelboxconnect*` is a persisted def, the stored
  state cannot be just a destroyed/healthy flag — a *running* looping animation is part of what the
  original carries. (The `BL-099` fuel-depot report that produced this item is answered in full in
  that doc — a cold IA1 renders intact tanks by spec, in the original and in CSVM alike; do not
  "fix" the depot, and do not re-derive the depot chain analysis it records.)
  ⚠ **Traps.** (a) **Do not use this to explain away a rendering difference.** It is the reason a
  screenshot of the original is not evidence about the shipped data, which makes it an equally good
  way to hand-wave a real bug; anything blamed on the log needs the mission sequence that produced
  it. (b) Two facts are still untested and would change the design: whether the commit happens at
  damage time or at mission completion, and a direct A/B separating the two flags (destroy a
  `PERSIST_LOG` object and a save-only one in one campaign mission, then load an IA — the first
  should carry, the second should not).

- `BL-256` `[Feature]` **Stunt screenshot feature, triggered off `DzRadius` — much later, by user decision
  (2026-08-04).** `DzRadius` (15 m, user-hand-tuned) is settled
  as the **marker-centre radius**: scoring crosses the authored `dzpathN` gate pair
  (`docs/HISTORY.md` 2026-08-01 "M3 Wave C C9"), and the constant's remaining roles are the `dzN`
  marker centre and, eventually, the trigger for a stunt screenshot feature. No design beyond
  this sentence exists yet — recorded so the constant's purpose and the feature intent survive.
  The screenshot latch should also play `snd_dangerzone_camera` (`dangerzone_camera.wav`, a
  data-orphan SFX named by no `SOUND_GROUPS` entry and no world data; the user confirms it is
  the automatic-screenshot sting, not a zone-cleared cue — formerly `BL-090` item 5, closed).
  ⚠ Do not retune or delete `DzRadius` as dead code — it is reserved, and the 15 m is the user's.

- `BL-361` `[Feature]` {CAMPAIGN} **Scripted-path vehicles: a second movement law, decoded, with
  nothing in `CSVM/src` for it.** Surfaced 2026-08-15 by the ground-blow emitter decode (`BL-359`,
  since closed — `git log --grep=BL-359`), which had to establish what the byte at `+0xcc` means
  before it could say whether zeppelins repel the player. Write-up in
  [`docs/org/flightModel.md`](docs/org/flightModel.md)'s "Ground blow".
  *What it is:* the aircraft update dispatcher `FUN_00489ea0` branches on `obj+0xcc` **before** it
  reaches any flight law. Non-zero, and the object is driven by the path follower `FUN_0048a110`;
  zero, and it runs the movement law selected by `obj+0x67C` (`0`/`4` being `FUN_0048e580`, the
  flight integrator). So a placed vehicle can be a puppet on an authored waypoint list rather than a
  simulated aeroplane, and it hands itself back to the flight model on reaching the last waypoint.
  *The lifecycle, decoded:* the spawner `FUN_0047c210` sets `+0xcc = 1` when the spawn record carries
  a path (`0x0047c568`) together with a freeze flag `+0xd4 = 1` (`0x0047c57e`), so the vehicle sits
  motionless at its first waypoint until a mission goal releases it (`FUN_0046a2b0`, `0x0046a2c3`,
  reached from the goal-action runtime `FUN_0046a490`). That same runtime can attach a path at any
  time with `FUN_004940d0` (`0x0049427c`), which sets `+0xcc = 1` and `+0xd4 = 0` so the vehicle
  starts moving at once. Only `FUN_0048a110` clears `+0xcc` (`0x0048a863`), and only on the final leg.
  *The follower's law*, per tick, with `dt` = `DAT_009ad744`:

      target   = next waypoint, y raised by the vehicle type's ride height at type+0x218
                 (a flat 0.2 m for movement classes other than 0/4)
      heading += clamp(headingError / 60°, ±1) · dt        radians, so ≥60° of error gives 1 rad/s
      speed    = 17.8816 m/s, which is exactly 40 mph      held until the final leg
      forward  = speed · (1 − |clamped heading error|)     it barely advances while turning hard
      advance the leg when dot(target − pos, legDir) ≤ 5.0

  On the **final** leg the target is replaced by a point **300 m** along the leg direction, its y
  gains `(speed/110mph − 0.4) · 83.3` once speed passes 0.4 of 110 mph (44 mph), and the speed term
  becomes `speed += 4.0302024 · dt` instead of the fixed 40 mph. Fixed taxi speed, a ground-height
  offset from the vehicle type, a final-leg acceleration with a climb-out, and a handoff to the
  flight model at the end read as **the runway takeoff run**, though `FUN_004940d0` shows a goal can
  attach a path for any purpose.
  ⚠ *Traps.* (a) **It is not AI behaviour and not our `AiMode.AvoidCrash`.** The follower replaces the
  flight model outright; nothing in it is a steering input to `FlightModel.Step`. (b) **The freeze
  flag `+0xd4` is separate from the path flag `+0xcc`**, and only the follower clears the path flag. A
  design folding the two into one boolean cannot express "placed and waiting", which is the state
  most authored vehicles spend most of a mission in. (c) `FUN_004afd00` gates AI radio chatter on
  `+0xcc` too, so a path-driven vehicle is silent; do not model the movement and leave the voice on.
  (d) **`4.0302024` and the `83.3` climb gain were read but not identified**, unlike `17.8816` (40 mph
  exactly), `0.020335784` (1/110 mph) and `0.95492965` (3/π, the 60° heading-error normaliser). Nail
  those two before shipping a takeoff that looks right at one airframe and wrong at another.
  *Size:* LARGER. It needs a path source in the mission data, the follower, and a hook in the goal
  runtime. Campaign-scoped: Instant Action places no vehicle on a path (`ia.zrd.json`'s
  `dzpath1`–`dzpath5` are danger-zone gates, not vehicle paths), so no golden can see it.
  *How you'd know it worked:* a mission-opening aircraft sits still on the strip until its goal
  fires, then rolls at a steady 40 mph, accelerates and climbs out on the last leg, and flies
  normally from the moment it leaves the path.
  *Cross-refs:* [`docs/org/flightModel.md`](docs/org/flightModel.md)'s "Ground blow" —
  ground blow's own emitter test reads this byte, so a spawned vehicle put on a path stops repelling
  the player the moment it completes the path and drops into the flight model. Ground blow itself
  shipped without the registry filter (its player probe simply excludes aircraft), so building the
  follower means revisiting whether a path-driven vehicle needs to become an emitter in this build.

- `BL-362` `[Feature]` **Instant Action wingmen never form up on the player.** *Evidence:* the user at the controls,
  2026-08-15: wingmen fly away instead of staying near the player, with no formation-flying
  behaviour anywhere in the engine and the placeholder law driving them.
  *What we do today:* `GameSession.BuildFlightRigs` places wingman `i` on the decoded spawn fan
  (`InstantActionRuntime.WingmanSlotFor`) and hands it `AiPilot.HoldingCourse`, so it holds the
  player's spawn heading and altitude for the rest of the mission. The decoded escort chain (0/1/3
  escort the player, 2/4 escort 1/3) is wired only into `AiGunner.PrimaryTargetName`, which names
  who to SHOOT, not who to stay near.
  ⚠ **And that assignment is unreachable in our engine:** `SelectRankedTarget` skips every same-team
  candidate before it tests `PrimaryTargetName` (`FlightController.cs:2253`), and a wingman sits on
  `AimAssist.PlayerTeam` exactly like the player it is pointed at. So the chain resolves to nothing,
  on every wingman, in every mission. Verify that before designing on top of it.
  *What the decode says:* [`docs/formats/ai-rosters.md`](docs/formats/ai-rosters.md) on
  `primary_target` says formation-looking behaviour in the original rides nets whose trailer names
  `player`, and `primary_target`, "not this slot". Both halves turn out to be real, and which one
  applies depends on whether the wingman has a net.
  ⚠ **This paragraph used to rest on "a wingman gets no patrol net (`netids` keeps its `-1`)",
  quoted from `instant-action.md`. That was wrong and the page is corrected** (`BL-364`,
  2026-08-15): an Instant Action wingman IS given the chapter's first net. So the sentence that
  followed it here, "a wingman has no net, so the trailer half cannot be the mechanism", was wrong
  twice over, and is struck: in C1 that first net is `[10, "player"]`, so the trailer half is
  exactly the mechanism there (`BL-377`, landed and closed).
  *Fix shape:* answer the decode question first, then a station-keeping input source in `AiPilot`
  dispatched from `AiModeMachine`. Do not invent a formation offset ahead of it: `WingmanSlotFor`'s
  fan is decoded as a SPAWN placement, and reusing it as a flying station is a guess wearing a
  decoded number.
  ⚠ *Traps.* (a) The nine `AiMode` values are decoded from the engine's own debug readout and none
  of them is "form up"; a tenth is invented and has to be named as such, out of `NameOf`'s verbatim
  vocabulary. (b) **Friendly fire is decoded as real** (M4 A2): a wingman holding a tight station
  will die to the player's guns, which is correct, and must not be papered over with a damage or
  collision exemption. (c) It is a chain, not a star: 2 and 4 station on 1 and 3, so a dead leader
  leaves its follower without one, and that case needs an answer rather than a crash.
  ⚠ **DECODED 2026-08-15, and the blocker above is void.** [`docs/org/aiPilot.md`](docs/org/aiPilot.md):
  a netless `mode wingman` aircraft flies a fixed formation station on its `primary_target`
  (`FUN_0041e760`), so `primary_target` on a friendly is a **formation leader**, not a target
  assignment, and `SelectRankedTarget` skipping same-team candidates never mattered. The station is
  a body-frame offset from the leader: **(6, 0, 18)** when the leader is the player (6 m out, level,
  18 m astern) and **(8, −2, −8)** when it is another AI, with an 80 m separation push, a 700 m
  join threshold and a speed-ramped trail distance of 106.68 m to 259.08 m when it is chasing
  instead. So `WingmanSlotFor`'s spawn fan was indeed the wrong thing to reuse, and the real offset
  is now decoded rather than invented.
  ⚠ **But it is a campaign behaviour, not an Instant Action one.** `BL-364`'s decode shows Instant
  Action gives every actor a patrol net, and a net demotes a `wingman` to `jet` at spawn, so in the
  original an IA wingman walks the chapter's first net and does **not** hold station. The shipped
  netless wingmen are the campaign's `wingman_N` / `bswingman_N` roster blocks. Read off the code
  path, not observed at the controls of the original, so an IA capture would be worth having before
  building station-keeping for that mode.
  ✔ **And Instant Action's half is now DELIVERED, by a different mechanism** (`BL-377`, landed and
  closed 2026-08-15; [`docs/org/aiPilot.md`](docs/org/aiPilot.md) "The trailer" is the decode, and
  `git log --grep=BL-377` the work). An IA wingman takes the chapter's first net, and in C1 that
  net is anchored to the
  `player`, so the whole graph is carried around the player and the wingman patrols around them
  without any station-keeping at all. So "wingmen never form up on the player" is answered for
  Instant Action by the original's own means; what remains here is the CAMPAIGN's netless
  `mode wingman` station, whose offsets are decoded above and which nothing in `src/` yet flies.
  ⚠ It is not a formation and should not be judged as one: the wingman walks a figure-eight
  ~1 km across that happens to travel with the player, so it comes close and then swings out again.
  ✔ **The Instant Action side was flown 2026-08-15 and passes** (`PT-50`, now retired): the three
  Fury wingmen spawn as a plausible flight, they start patrolling and are carried along by the
  player-anchored net rather than drifting off alone, and friendly fire is confirmed possible as
  Decision 3/A2 requires. That verdict covers Instant Action only, which is why the item stays open
  on the campaign station below.
  *Playtest after fix:* fly a CAMPAIGN mission whose roster has netless `wingman_N` blocks and watch
  one hold the decoded body-frame station on its leader, 6 m out and 18 m astern of the player, from
  the 700 m join threshold inward, and trail at the speed-ramped distance when it is chasing.
  *Cross-refs:* [`docs/org/aiPilot.md`](docs/org/aiPilot.md) (the decode), `BL-363` (the other half
  of the same playtest: an escort with nothing targetable), `BL-364` (the patrol nets, and the
  correction to `instant-action.md` this rests on),
  [`docs/formats/ai-nets.md`](docs/formats/ai-nets.md) (the anchored net that delivers the IA half,
  closed as `BL-377`).

## Tooling, platform & docs

- `BL-427` `[Feature]` **Extract `langui.dll`'s string table.** Split out while the Ammo Selection
  screen was built (`git log --grep=BL-353`). `extracted/messages.json` carries the weapon **names**
  (`MSG_WEAP_APIERCING_ROCKET` → "Armor-piercing rocket", the `MSG_WEAP_*` block at ids 12124–12160)
  but no prose beyond them. The original's Ammo Selection screen also shows a description pane for
  the highlighted round ("Slugs — These standard lead bullets do damage equally well to both armor
  and internal components", visible in `OriginalScreenshots/Ammo Selection Gun DropDown.png`), and
  nothing in the extracted data contains that text. It can only be in
  `CrimsonSkiesGame/GOSDATA/ASSETS/BINARIES/langui.dll`, a Win32 resource table no tool of ours
  reads.
  **Why it is worth its own item.** The wizard's own decoded facts already cite langui ids (the
  thirteen militias at 3670, the presets at 3600–3618), so the table is being read second-hand today
  from decode notes rather than from the file. It no longer carries `BL-352` with it: the *View
  Story* page has no per-preset prose to find, which the decode of that screen settled.
  ⚠ **Traps.** (a) Our Ammo Selection screen ships without description panes, which is a stated
  divergence rather than an oversight; adding them is this item, not a bug fix on that screen. (b) A
  quarter-width four-player pane has no room for a prose block, so the panes are not simply "the
  screen plus a description" once the strings exist. (c) `langui.dll` is in the game install, which
  is git-ignored and absent from worktrees — read it by absolute path.

- `BL-033` `[Cleanup]` `[Blocked: SDL >= 3.4.4]` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

## Misc

- `BL-072` `[Feature]` **Paint scheme follow-ups** (the core landed 2026-07-20 — see `docs/formats/paint.md`
  "Known divergences"; these are the leftovers):
  - **The paint UI's "Shade" column** is unmodelled — three Colour *and* three Shade
    dropdowns exist in the UI, only three colours in the data. We ramp black → colour.
  - **A livery picker in the launchscreen.** Selection is CLI-only (`--paint=`); flight
    randomizes per player. Decide from playtest whether the menu should offer it.
  - **AI/ace liveries.** `ia.json` `ace_*` and the AI defs' own `paint_*` are parsed into the
    catalog but nothing flies them — there are no AI aircraft yet.

- `BL-077` `[Feature]` **Visual prop spin-up/down** (`startprops`/`stopprops` disc crossfade) — spawning mid-air
  already turning is by design; becomes relevant with a landing/shutdown flow
  (`FlightAudio.OnEngineStop` is already wired for the audio half).

- `BL-284` `[Bug]` `[Blocked: CAP-34]` **Wing-light flare: soft round glow vs the original's sharp star burst; view-dependence
  unproven.** Follow-up from `BL-119` (landed 2026-08-05): with the authored one-sided quad restored
  and the blink at the measured ~1 frame, the flare reads as a compact soft amber glow — much closer
  than the old billboard blob, but the PT-03 reference still shows sharp radiating star points that
  our plain radial `oil_liteflare` sprite does not produce. Whether the original draws the flare
  from every angle (a one-sided quad is roughly chase-view-only) is also unmeasured — one orbit
  clip of a lit plane in the original settles both — any player plane works: `vehicle.zrd.json`
  wires `wing_lights_blink` (or `brigand`'s own `wing_lights_brigand`) into every player craft's
  `start_anims` except the Bloodhawk, which has neither the anim nor flare nodes. (Earlier notes
  here said only piratefighter/brigand carried it — that read `wing_light.zrd.json`'s two
  `ANIMATION_DEFINITION`s alone; `vehicle.zrd.json`'s per-plane `start_anims` is the wider
  wiring and is what the runtime actually plays from, per `WingLights.cs`'s doc comment.) Also riding
  here: `WingLightBlinker.LightEnergy = 1.0` is a declared TUNE — the def authors the point
  lights' range/colour only, no intensity.
  ⚠ Traps: (a) re-adding the billboard is the rejected fix — PT-03's screenshot is against it.
  (b) don't edit or swap the sprite to fake the star: the star points may be the original engine's
  flare *rendering* (a cross-flare pass), not the texture asset — the orbit clip decides first.
