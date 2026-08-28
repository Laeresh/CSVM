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

- `BL-515` `[Research]` **CM04 (C3/M03): the Barracuda takes damage from every side, and the original may
  only accept hits inside its hangar.** *Evidence:* reported at the controls as a question: the
  submarine can be damaged from any angle, where the recollection is that the original demands
  shooting into the open hangar. Not decoded either way. *Fix shape:* read the Barracuda's
  destructible data in C3 (`barracuda`, activated by `sub_movement`, `docs/formats/gamez.md`): which
  node carries the HP pool and whether that node is the hull or an interior hangar volume. If the
  pool sits on an interior node, our hit resolution is landing on the hull's collider instead and
  that is the bug. If the pool is on the hull, the original behaves as we do and this closes as an
  answer. The lead is the mission's own voice line, which tells the player to shoot into the
  Barracuda's hangar; that is a hint about where the weak point is, not proof the hull is immune.
  *⚠ Traps:* the report is a question prompted by that voice line, not a memory of the original's
  hit rule; do not build a hangar-only rule from it. If the data shows one pool on the hull, the
  voice line is flavour and this closes. *Cross-refs:*
  `BL-512` (the same sub's launch motion), `BL-522` (its launched fighters).

- `BL-570` `[Feature]` **The difficulty setting has no menu row.** *Evidence:* the scale itself is
  live (`Flight/Difficulty`, `--difficulty=<normal|hard|hardest>`), but a CLI flag is the only way to
  change it, so a player launching normally always flies the default Normal. The original puts it on
  the game-options screen: `IDS_GO_DIFFICULTY_TITLE` "Difficulty" with
  `IDS_GO_DIFFICULTY_DESC` "Select the difficulty level for a solo campaign", over the three
  `IDS_DIFFICULTY` rows Normal / Hard / Hardest (`rof/ui_strings.json` ids 109-111).
  *Fix shape:* a row on the options screen writing the same 0/1/2 the flag parses, persisted with the
  rest of the profile so it survives a launch, with the flag continuing to win for a scripted run.
  *⚠ Traps:* it is a campaign-scope setting, not a per-mission one, and Instant Action does not read
  it: an IA wave's own skill stands in for that spawn, which is a different control the wizard
  already owns. Do not wire the menu row into the IA path or a wave will fly at two difficulties.
  And it selects a hit-point tier only, so it must not be presented as changing how well the enemy
  flies or shoots, which it does not.
  *Cross-refs:* `Flight/Difficulty`, `docs/formats/instant-action.md`, `docs/cli.md`'s
  `--difficulty`.

- `BL-557` `[Bug]` **The roster's `init_health` and `armor` overrides are parsed away, so the named
  aces spawn too soft.** *Evidence:* the original's roster spawn applies slot 7 `init_health` when
  greater than zero and slot 66 `armor` when greater than or equal to zero, then the difficulty
  scale ([`docs/formats/ai-rosters.md`](docs/formats/ai-rosters.md),
  [`docs/org/vehicleDamage.md`](docs/org/vehicleDamage.md)). `RosterSpawnPlan` carries neither value,
  `CampaignRosterPlan.Build` does not read them, `SpawnFor` cannot forward them, and the assembler
  seeds the airframe defaults instead. There is no `InitHealth` symbol in the tree at all. The census
  (`analysis/aim-assist-ttk/Census-RosterDurability.ps1`) over the 414 extracted blocks finds, among
  the 251 enabled non-player-team ones, **25 authoring a positive `init_health` and 20 a non-negative
  `armor`**, every armour value between 90 and 132 and every one of them an override that RAISES
  durability above the airframe default. They are the mission's named aces: `hafury_1`-`_6` at
  108/108 in C2/M03, `hkfirebrand_9` at 132/132, the Black Hat Brigands at 126/126, and so on.
  *Fix shape:* add the two fields to `RosterSpawnPlan`, read them in `CampaignRosterPlan.Build`,
  forward them through `SpawnFor`, and apply them in the assembler before the difficulty scale and
  the jitter, at `AiFlightAssembler.Assemble`'s `WithEnemyDurability` call, which is where the
  engine's spawn order is already reproduced.
  *⚠ Traps:* **a missing slot is not a zero.** Blocks are not fixed-width (field-count histogram
  42/65/66/67/68/81) and 33 of the 414 stop at 66 fields, so slot 66 does not exist on them; a reader
  that maps absent to `0.0` invents 18 armour-stripped hostiles in C2/M05, C2B/M04 and C3/M01 that
  the data does not author. No shipped hostile authors `armor 0`. The two gates also differ and both
  matter, `init_health` only when `> 0` but `armor` when `>= 0`. The eight per-zone roster slots are
  `-1` on all 414 blocks and stay parsed-and-ignored; do not revive that path. And note the
  direction: fixing this makes those enemies TOUGHER, so it does not relieve a long time-to-kill, it
  lengthens it on exactly the fights that should be hard.
  *Cross-refs:* `Flight/Difficulty` (the scale this lands in front of),
  `analysis/aim-assist-ttk/FINDINGS.md` (whose census of this field is superseded by the script
  beside it).

- `BL-561` `[Research]` **Aircraft projectile hit volumes are tuned convex decompositions, and the
  original's hit geometry is untraced.** *Evidence:* `PlaneCollider` builds an aircraft's hit boxes
  from model triangles under several constants marked `TUNE` (wing band, tail split, minimum
  thickness, volume split, part limits). Those boxes are what a round is tested against. The original
  runs a polygon-accurate segment query gated by per-node flags, with `INTERSECT_BBOX` nodes swapping
  the polygon test for a bounding-box one
  ([`docs/org/weaponRay.md`](docs/org/weaponRay.md)). Hit-rate parity is therefore unestablished, and
  the direction of any error is unmeasured: a collider narrower than the model loses hits, a wider
  one invents them.
  *What to settle:* whether the aircraft nodes the original tests carry `INTERSECT_BBOX` (in which
  case a box decomposition is the right shape and only its extents are in question) or reach the
  polygon loop, and how our boxes compare with the model's own silhouette.
  *⚠ Traps:* this is a hit-RATE question, not a damage-per-hit one; do not chase it with a TTK
  stopwatch, which cannot separate the two. Measure rounds fired against rounds registered on a held
  burst at a fixed target, then compare. And settle `BL-557` first, and take the reading at a known
  `--difficulty=`: with the pools wrong, any TTK number here is unusable as evidence either way.
  *Cross-refs:* `BL-557`, `Flight/Difficulty`, `docs/org/weaponRay.md`,
  `analysis/aim-assist-ttk/FINDINGS.md`.

## Weapons & combat

- `BL-066` `[Feature]` **M3-deferred — ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- `BL-226` `[Feature]` **The incoming-fire cue set's other two halves are now wireable.** The
  near-miss third landed (`BL-087`, 2026-08-02); `bullet_hit_sg` (= `snd_ricochet1-4`,
  `player.json`'s `bullet_hit_sound`) and `window_hit_sg` (= `snd_windowhit1-3`, non-3D) did not.
  Both sit on the five `player_pfighter-bulletN` canopy-hole defs (the `bullethole_anims` of
  `docs/formats/vehicle.md`, 10 files per chapter × 8), so they are shipped and referenced, not
  orphans. The design's rule is that incoming-fire intensity is how the player reads a shooter's
  distance, calibre and ammo type; the accumulator that rates it is now decoded and running
  (`WarningShotCue`), and the Cockpit/Nose modes make the first-person branch live.
  ⚠ **Traps.** (a) **`bullet_hit_sg`'s shooter blocker is GONE (2026-08-13):** since M4 A2+D14 an
  AI gunner fires real rounds at the player (`--ai=… --ai-attack=…`), so this half is wireable
  now — it hangs off the projectile-hit path, not the near-miss accumulator alone.
  (b) `window_hit_sg` is `PlayerFirstPerson`-gated through the bullet defs, so dispatch it only
  while Cockpit or Nose is the effective view. (c) **Do not fake either off our collision path**:
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
  ⚠ Traps: (a) **The cycle sequence is settled and must not be re-derived**: the selector walks the
  hardpoints in physical mount order (`Loadout.PylonStepOrder`, `FireControl`'s `pylonStepOrder`),
  which is NOT the order the list is built in (`Loadout.PylonFillOrder`, 1,5,2,6,3,7,4,8, which says
  only which pylons a fit occupies). Stepping the list itself sent the gauge arrow back and forth
  across the belt on a full fit. What is missing here is the second direction, nothing else, so a
  reverse step is `NextSelectable` walked backwards over that same sequence.
  (b) The observation is about hardpoints. The gun-group selector is
  the analogous case but was not observed, so do not assume it cycles both ways either.
  (c) Empty-slot skipping is not in question and must survive the change: both directions land on
  an armed slot.
  *Cross-refs:* the hangar's custom loadouts (`docs/plans/PLAN-hangar.md`, landed) are where
  mixed fits make the direction matter, so this item's value went up when that shipped;
  `BL-296` (ActionMap/rebinding seam), `git log --grep=BL-062` for what settled the
  per-hardpoint half.

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


- `BL-559` `[Research]` **Do the original's gun rounds carry the launcher's velocity?** *Evidence:*
  [`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md) ("Launch velocity is inherited, and decays
  over `LOCK_ON`") decodes `FUN_005aef40` as copying the launcher's velocity into a round only when
  the weapon carries `LOCK_ON`, and writing a zero vector otherwise. No gun authors `LOCK_ON`.
  `ProjectilePool.InheritedAtLaunch` deliberately holds guns outside that rule, and the comment above
  it says so and states the question was never settled. Two decoded facts pull the other way and are
  the reason this is worth reading rather than assuming: the aim assist solves its intercept on the
  RELATIVE velocity, which is the correct solve only for an inheriting round, and the decoded pipper
  places itself at `muzzle + 0.5 × (VELOCITY × nose + planeVelocity)`
  ([`docs/org/aim-assist.md`](docs/org/aim-assist.md)), which is where an inheriting round would be.
  Either guns take a spawn path other than `FUN_005aef40`, or the `LOCK_ON` gate is narrower than the
  ordnance page states, or the original's sight and its rounds genuinely disagree.
  *What to settle:* which spawn function the `CANNON` branch of `FUN_004b6820` calls, and whether the
  `+0x30`..`+0x38` launch-velocity copy is reached on that path.
  *⚠ Traps:* not a TTK item. If CSVM is wrong here it is wrong in the player's FAVOUR, since an
  inheriting round lands where the relative-frame lead predicts and a non-inheriting one falls short
  of it. Do not "fix" it as part of a lethality pass, and do not change the pipper formula or the
  assist's relative-velocity solve to match a change here without re-reading both: the three are one
  system and the decode page records the sight and the assist as deliberately disagreeing already.
  *Cross-refs:* `docs/org/aim-assist.md`, `docs/org/ordnanceTypes.md`, `ProjectilePool.Ballistics`.

- `BL-567` `[Bug]` **CM04 (C3/M03): the Pandora's broadside cannons fire on the player; the
  original's broadsides engage zeppelins only.** *Evidence:* reported at the controls with the
  original's rule stated: broadside cannons attack enemy zeppelins and never aircraft. The sortie
  log shows `shot hit P1 (fuselage→nose): wep_28 armor=5.0/25 ...` and three more `wep_28` hits on
  the player; `wep_28` is the piratezep broadside's weapon. **The report stands against the
  decode.** `crimson.exe` resolves every `targets` name through the general node table
  (`FUN_004bd8d0` calling `FUN_004d0280(7, name)`, the lookup that finds the player's own node),
  matches the node against the zeppelin roster (`FUN_004bede0` via `FUN_004bd430` on the roster
  at `0x71df80`), and at fire time (`FUN_004bfe00`) fires on a node that matched no zeppelin at
  the node's own position (`FUN_004cf2c0`): the `player` node is a target like any zeppelin, no
  team or hostility field is read, and the remake's `ZeppelinRuntime.Cannons.ResolveTarget` (via
  `ZeppelinBroadside.FirstLiveTarget`) matches it. The chain is in
  `docs/formats/mission-entities.md` "Broadside firing". *What remains:* a flown original-game
  check. Fly C3/M03 in the original into the Pandora's broadside arc (inside `cannon_fire_range`,
  abeam the hull, long enough for the deploy and a volley) and record whether the broadsides open
  on the aircraft; if they hold fire, the gate the decode did not find is somewhere the static
  read did not reach (a range or altitude term in `FUN_0053e56d`'s solve, or the node's
  `FUN_004cf2c0` position read failing for a vehicle node), and that is what to decode next.
  *⚠ Traps:* do not add a hostility or team gate on the strength of the report alone; two decodes
  found none, and only the flown check outranks them. *Cross-refs:* `BL-517`'s closing commit
  (`git log --grep=BL-517`), `docs/formats/mission-entities.md`,
  `docs/formats/anim-definitions/cutscenes.md` "The name is what resolves". *Playtest after fix:* `CAP-46`.

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
  callback, the mouse-flying arm's `is_autogyro` roll/yaw exchange, plus `BL-448`.
  Each is small and independently landable; each names its address in the table.
- `BL-448` `[Research]` **Is the 2003 m `AltitudeCapM` the dense-band edge?** The measured
  flight ceiling (an intentional exception) sits 3 m above the decoded atmosphere band boundary
  (2000 m, `6561.6796875` ft, writer `FUN_00463640`). If the original's ceiling is the thin band's
  own lift loss rather than a separate cap, the exception becomes a decoded mechanism and the cap
  constant goes. Lead recorded in the plan's A1 section; `AtmosphereBandTests` has the band.
- `BL-456` `[Research]` **Trace the writers of the crashed flag `[obj+0x384]`.** Its readers are
  decoded (`0x48c4ba` selects the far-field arm, `0x48cd4a`, `0x48dfbe` gives a crashed hull
  severity and no impulse); its writers `FUN_0043d640`, `FUN_004735b0`, `FUN_004aff80` are not,
  so the ledger keeps "a wreck flies the near-field plant" as an exception. Decode when and by
  whom it is set so the wreck can fly the decoded arm.
- `BL-562` `[Perf]` **The physics tick costs ~39 ms per frame late in CM11 (C2/M02), so the sim runs
  at about half of wall time.** *Evidence:* a flown CM11 session's hitch records
  (`.scratch/logs/game-*.out`, `[perf] hitch … physics_ms=…`) show the frame baseline rising from
  9 ms at launch to 30–40 ms with `physics_ms` at ~39 ms of it once six aircraft, the trailer's dust
  puffers and the roadblocks are live; 2770 rendered frames then covered 52 sim seconds (one
  parked-plane `flight:` line per sim second). Godot caps physics catch-up per frame, so a
  physics-bound frame lets the sim clock fall behind the wall clock: the mission takes about twice
  as long to play as its `TimeMs` records, and every `_Process`-driven consumer that still reads wall
  time drifts against the aircraft (the animation runtime moved onto the physics tick for this
  reason, see `AnimRuntime._PhysicsProcess` and the `anim-clock-realtime` suite). *Fix shape:* profile
  one CM11 session past the roadblocks with `--perf` and the hitch sidecar's `samples`, attribute
  the physics step (`FlightController._PhysicsProcess` chain: six flight models, AI mode machines,
  projectile sweeps, the objective graph's per-tick scans, puffer emitters at 1 m distance
  intervals on the trailer) and bring the step under the 16.7 ms budget on the reference rig; a
  perf scenario in `analysis/perf/scenarios.json` for the late-CM11 state is the regression gate.
  *⚠ Traps:* a wall-clock measurement of anything in that session is not a sim measurement, so
  compare durations in sim seconds (the log's 1 Hz `flight:` cadence, `GameClock.Frame`), never in
  wall seconds; do not raise `max_physics_steps_per_frame`, which only deepens the catch-up spiral.

## Environment & world

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

- `BL-508` `[Research]` **The original never alpha-tests, so every alpha texture we scissor is an
  invention rather than a reproduction.** *Evidence:* decoded from `crimson.exe`
  (`analysis/alpha-classification/FINDINGS.md`, "The original has no cutout path"). The renderer is
  `zvid_ddd3d.c` over `IDirect3DDevice3`, and `D3DRENDERSTATE_ALPHATESTENABLE` is set nowhere in
  the whole `0x0059e000–0x005ab000` layer, nor are `ALPHAREF` and `ALPHAFUNC`, so alpha test holds
  its Direct3D default of FALSE for the entire run. Blending is one per-texture mode field
  (`tex+0x10 == 4`, `FUN_005a4210`) against a fixed `SRCALPHA`/`INVSRCALPHA` pair, and the
  archive's `TextureAlpha` class decides pixel PRECISION only (`FUN_005a27e0`: colour key for
  `Simple`, 8888 → 4444 → 1555-at-128 for `Full` depending on the card). Ours scissors 388 of the
  604 alpha textures, tree and fence cards included. *Fix shape:* decide per family whether
  faithfulness or the current look wins, then either widen `TextureArchive.SoftAlphaCoastline` or
  drop the pixel rule entirely; if it goes all the way, `AlphaIsSoft` and its 0.45 threshold become
  dead code and the census script goes with them. *⚠ Traps:* blending moves a surface into the
  transparent pass with no depth write and per-object sorting, which is a real risk on the
  thousands of coplanar foliage and railing cards a hard cutout currently keeps in the opaque pass
  — the coastline sheets were safe because they are few and flat, and that does not generalise.
  A period video card without a 4444 or 8888 texture format collapsed `Full` alpha to 1 bit at
  threshold 128, so a 1-bit look in reference footage may be the hardware and not the intent; check
  which the shot is before treating it as the target. *Playtest after fix:* a low pass over C1's
  tree lines and C2's Eiffel replica, where the erosion this rule was written to prevent would show
  first. *Cross-refs:* `analysis/alpha-classification/FINDINGS.md`, which carries the decode and the
  install-wide census; the coastline commit that filed this (`git log --grep=SoftAlphaCoastline`).

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

- `BL-035` `[Feature]` **Animation event kinds that need weapons or cutscenes — `CALLBACK`, `OBJECT_CYCLE_TEXTURE`,
  one-shot `SOUND`** (triaged 2026-07-22, the last of `docs/plans/PLAN-anim-rendering-followups.md`
  item 2 after `OBJECT_MOTION` landed). All three still dispatch at bootstrap, so the counts in
  the "not yet acted on" report look like open work — **they are not**. Each was probed at the
  dispatch site across C1/C3/C4/C5 (def, anchor, resolved target count, payload), and each fails
  for a concrete reason rather than a suspicion. Implementing any of them today is a provable
  no-op, the same verdict `OBJECT_ADD_CHILD` got:

  | Kind | Count | Why it cannot do anything |
  |---|---|---|
  | `Callback` | ×8 every chapter | **Answered.** The values 1/2/10/11/14/20/913/914 are the cutscene vocabulary, decoded in `docs/formats/anim-definitions/cutscenes.md`, and `CutsceneController` is the host that raises them, for a story mission's intro and for the `landings.zrd` approach triggers `LandingApproachRuntime` starts. What is left here is only the OTHER codes the mission-script host reads: **3, 12, 13, 86, 701, 702, 800–803, 950, 951, 965–968**. Those reach the host today and are declined, so they are now testable rather than unreachable. C3/M01's own drop raises `951` (teleport the player to the camera pose) and it is the first one worth doing. |
  | `ObjectCycleTexture` | ×1–2 per chapter | Every dispatch is `node=taildamage` with **`targets=0`** — the node never resolves, so there is nothing to cycle. The one real use of this mechanism (the cockpit damage-indicator hilite) is already a build-time material swap in `GaugeCluster.cs`. |

  **The blocker on the `Callback` half is gone**: the approach trigger exists, so the remaining
  codes now arrive at a live host and each can be implemented and tested against C3/M01's own drop
  and hookup. `ObjectCycleTexture` still needs a mission that actually builds a `taildamage` node,
  which none of the ones this project defaults to do.



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
  `BL-406` (closed).

- `BL-535` `[Bug]` **A repeat sonic burst pays a 10 to 14 ms slot re-reset on every play from the third on.** With every emitter pre-built at bind (`AnimRuntime.PrewarmEmitters`), the weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08 --weapon-fire --infinite-ammo --weapon-surface=default --weapon-standoff=90`) still records one `effect_checkout` sample per burst once `hitchMonitor.floorMs` is 10 and `medianMultiple` 1.2 under `--no-det --no-vsync`: 0.6 ms for the first two bursts, 10.2 to 13.9 ms for every burst from the third on, one call each. The step lands two bursts before a 4-slot pool could recycle, so it is not a wrap and not construction; it is the cost of re-resetting a slot copy that has run before, in `AnimRuntime.ResetCheckedOutCopies` (the re-reset that fixed the rings vanishing from the fifth burst on). Under the stock monitor it never trips, so it is a per-burst cost rather than a hitch, and at vsync it is inside a frame.
  *Where to look:* what the re-reset walks per copy (every template node of the subtree, or only the ones the last run posed), and whether the END pose can be recorded at stop time so the reset is a replay of a short list. ⚠ `docs/verification.md` PERF-14: the stock probe cannot fail on this; the lowered monitor is the only instrument that sees it, and it needs both knobs, since the trigger is the larger of the floor and median × multiple.
  *Cross-refs:* `BL-231` (closed; the pool-size judgement this was measured under), the `effect-pool-reset` suite (the pose contract the re-reset keeps).
- `BL-536` `[Bug]` **Every .NET collection in flight is a gen1 collection with ~45k objects pending finalization and ~30 MB promoted, pausing 24 to 29 ms about every 12 s.** The 90 to 120 ms stall every ~190 frames that this item was filed on is gone: it was the `EXECUTION_BY_RANGE` sweep re-measuring 69 deferred anchors' mesh bounds on every 8 m cell crossing, ~25 MB/s of finalizable `StringName`/`Godot.Collections.Array` wrappers (now measured once per anchor, `AnimRuntime._rangeOriginLocal`). What remains is under `HitchMonitor`'s 40 ms floor and no longer trips, but a `dotnet-trace` GC-verbose capture on `--fly --chapter=C1 --plane=player_bhawk --perf --no-vsync` still shows every collection as gen1 (`gc0_delta` and `gc1_delta` move together), `FinalizationPendingCount` ~45k at each one and a residual 1.7 MB per 60-frame `--perf` window, of which `GodotWorldQuery.Ray` (a `PhysicsRayQueryParameters3D`, an `Array<Rid>` and a result `Dictionary` per cast, several casts a sim step), `AnimInstance.Live()` (an iterator per frame from `AnimRuntime.Retirable`) and `GaugeCluster.DrawGaugePoly` (a `Color[]` and `Vector2[]` per polygon per draw) are the sampled allocators.
  *Where to look:* what keeps promoting ~30 MB into gen1 per collection when the allocation rate is 3 MB/s (finalizable Godot wrappers survive their first collection by construction, so the ray-query objects are the first suspect: reuse one `PhysicsRayQueryParameters3D` per caster), and whether the 24 to 29 ms pause is the finalizer queue's registration rather than marking. ⚠ PERF-13: compare only within one vsync mode; the residual pause needs `hitchMonitor.floorMs` lowered to be seen at all (PERF-14's lowered-monitor caveat).
  *Cross-refs:* `BL-355` (closed; the capture that first showed the unexplained trip), `PLAN-perf-hitches`.
- `BL-537` `[Owed-playtest]` **Effect pools at four players, judged in play.** The pool sizes in `CSVM/data/effect_pools.json` were re-judged on a build with no first-use construction cost: rockets and the sonic burst never wrap, a four-object simultaneous death wraps `flame_ball_01` at 4 and 6 slots and is quiet at 8 (now shipped), and seven or more identical deaths in one frame wrap at the 16 ceiling and cannot be sized away. At the controls the single-player half reads right: four fireballs burn out in place, and the seven-death wrap is not visible under the debris. Still owed: a 4-player splitscreen session with everyone firing, judged for anything that reads as shared between panes, and the ceiling for many-player builds (at 16 players the default root wants 19 and gets 16). The instrument is `AnimRuntime.PoolRecycles` and the `anim: effect pool for '<name>' recycled slot` DEBUG line in the log file sink; the sizes staged print on the world-effects build line. ⚠ Raise only a root that logs a recycle, never the default; the three gun roots stay at 1; a root sized 0 clamps to 1. Each slot copies the root's subtree (155 templates at 1 player, 263 at 4).
  *Cross-refs:* `BL-535` (the per-burst re-reset cost measured under the same instrument), `BL-296`/`BL-299` (the other splitscreen-scoped items).
- `BL-538` `[Bug]` **A dark band on the large buildings at the distance the templates clutter fades out.** Reported at the controls in C5 with the authored `far_fade_range` applied: in the original the fade reaches the other buildings as well as downtown, and in the remake the larger (gamez) buildings show a dark area at the range where the downtown clutter vanishes, with buildings nearer and farther than that band reading brighter. Not the dither itself, which reads as the original's fade in motion.
  *Where to look:* whether the collapsed clutter cards still write depth or a dark fragment behind the band (the `csky_clutter_fade` cutout keeps a card in the pass until `step(d, far)` culls it, and a card collapsed to zero size should contribute nothing), whether the fog-volume clutter's own `far_fade` and the templates fade overlap at that range, and whether the gamez buildings carry a `far_fade_range` of their own the remake ignores (`FUN_004d5de0` applies the scaled test to every type-5 scene node, not only clutter). A C5 screenshot pair at the band distance with `graphics.clutterFarFade` on and off separates the two.
  *Cross-refs:* `BL-337` (closed; the fade), `docs/org/clutter.md`.

- `BL-512` `[Bug]` **CM04 (C3/M03): the Barracuda jumps while driving into the bay.**
  *Evidence:* the `sub_movement` def is read out
  (`extracted/C3/M03/mis_anim/barracuda-sub_movement.json`, main sequence): the drive is an
  `ObjectMotionFromTo` rise from `(-12032,-38,-13197.5)` to `(-12032,-6,-13197.5)` over 10 s, then
  three `ObjectMotion` events on `barracuda` (accelerate over 2 s, cruise 40 s, decelerate over
  2 s), then a second `ObjectMotionFromTo` at the absolute `(-12032,-6,-11516.288)` for the final
  6 s surfacing. Reading `translation.delta` as acceleration, as `MotionRuntime` does, makes the
  three speeds continuous and the travel 1680 m, which lands within 1.2 m of that absolute
  placement, so the authored choreography is smooth and our `delta` semantics are right. The jump
  is `rnd_xz`: all three events carry `(8.742278e-08, 0, 1.0)`, which is exactly the normalized
  `initial`/`delta` direction rather than a random amplitude, and `MotionRuntime` adds
  `RandSym() * rnd_xz` to each event's start velocity. That is +/-1 unit/s drawn independently
  three times, up to +/-44 m of accumulated travel, and the closing absolute `ObjectMotionFromTo`
  snaps it away in one frame. The final heading is NOT part of this: no event in the def carries a
  rotation term, `barracuda`'s gamez transform is `Initial`, and the hull's local -Z (its take-off
  run, `BL-522`) is correct as built. *Fix shape:* settle whether `rnd_xz` is a random spread at
  all, or a cached unit direction the original reads back; it is non-zero on far more than this
  def, so the change belongs to `ObjectMotion` as a whole (`docs/org/objectMotion.md`), not to the
  submarine. At the controls, on the landed launch decode, the Barracuda still reads wrong on all
  three counts: it appears where the bay approach is not, no surfacing is seen near the bay, and it
  then stands in the bay in one step (the closing absolute `ObjectMotionFromTo` snap after the
  `rnd_xz` drift), and its hull still faces the wrong way for the bay. The heading disproof above
  rests on the def carrying no rotation term and the gamez transform being `Initial`; the report at
  the controls stands over it, so the next pass compares the hull's local -Z against the bay
  opening and the `bauda_aip*` take-off path direction in the built world, not against the def
  alone. *⚠ Traps:* do not special-case the submarine, and do not "fix" the 1.2 m residual,
  which is authored. *Cross-refs:* `BL-515`, `BL-522`, `docs/org/objectMotion.md`.

- `BL-546` `[Bug]` **A nitro engage produces none of its visuals: no prop swap, no exhaust smoke.**
  *Evidence:* reported at the controls on a nitrous build whose boost accelerates the aircraft and
  whose dial now reads correctly: nothing on the airframe changes. Every part of the
  wiring is present, which is what makes this worth an item rather than a feature request. The
  `nitro_boost` def ships in `plane_props.zrd` as `LOCAL_NODES_ONLY` / `ACTIVATION ON_CALL` /
  `AUTO_RESET_NODE_STATES OFF`, and its sequences set `OBJECT_ACTIVE_STATE nitropropN ACTIVE`,
  ramp `OBJECT_OPACITY_FROM_TO` 0→1 on the same discs, spin them through `spin_nitrorotorN`, and
  play `snd_nitrostart AT_NODE nitroprop1`; `nitro_decay` reverses it. The airframes carry 34
  `nitropropN` nodes between them. `PlaneBuilder` classifies the disc and builds it hidden for
  that def (`PlaneBuilder.cs:275-277`, `PropParts.cs:25`), `EffectCatalogue.NitroAnims` binds both
  defs, and `FlightController` plays them off the `NitroSystem` edges
  (`FlightController.cs:1727-1741`). So the data is authored, the node is built, the def is bound
  and the call site fires; the break is between the call and the frame.
  *Fix shape:* establish first which half fails. Engage the boost with `--debug-anim` and see
  whether `nitro_boost` starts at all. If it does not, the suspects are `CrashRuntime` or
  `PlaneModel` being null on the human flight path, or `PlayWithin` failing to resolve
  `nitropropN` inside `PlaneModel`. If it does start, the disc is being activated and then drawn
  invisible, which points at `OBJECT_OPACITY_FROM_TO` against a material with no transparency, or
  at the hidden build state surviving the `ACTIVE` event. Settle the prop first: the smoke is a
  second question and the prop is the one whose whole chain is already readable.
  *⚠ Traps:* ⚠ **The absence of a `nitro engaged` line proves nothing** — `FlightController.cs:1733`
  logs through `Log.Debug("flight", …)`, which the file sink does not take. Do not conclude the
  edge never fired from a quiet log. The `ai_nitro_boost` / `ai_nitro_decay` wrappers in the same
  file are retargeting shims the executable never references, so do not wire the AI to them while
  chasing this. `NitroSystem`'s own state machine is decoded and confirmed working (the boost does
  accelerate the aircraft), so the defect is downstream of the edge, not in the tank or the arm.
  *Cross-refs:* `BL-447` (the AI's `medium_aishake` and `snd_nitro` blip on an engage, and the
  decay lockout that `_nitroDecayLeftS` stands in for), `docs/org/flightModel.md` "Nitro",
  `docs/formats/hud.md` "Cockpit gauges" for the dial half, which is settled.

- `BL-555` `[Feature]` `[Divergence]` **A held key fast-forwards a mid-mission cutscene instead of
  skipping it: the definition plays at a raised rate that spools up while the key is held and
  spools back down on release, with its sound pitched up to match.** The original arms a skip only
  on callback 20 (the intros), so CM02's capture and CM01's drop-off play out in full and
  `CutsceneController.Skippable` now declines the key there. A fast-forward keeps every authored
  code in order (913/914, 967, 951 and the `Loop{1000}` active-state re-assertions all fire at
  their authored beats, only sooner) while letting the player through a scene they have seen; the
  spool is a short ramp on the rate, not a jump. *Fix shape:* a rate multiplier on the cutscene
  clock (`AnimRuntime`'s advance takes the definition's dt; the held world, `GameClock.SimHeld`,
  stays held) ramped over a fraction of a second toward a target such as 4x while the skip key or
  gamepad A is down and back to 1x on release; the definition's own sounds (`SoundNode`,
  `OBJECT_MOTION` engine notes) take the same multiplier as a pitch scale, as the original does
  nothing of the kind so the values are a design choice. Advanced: the rate has to reach every
  channel a definition drives (pose, camera, sound, callbacks, the `RESET_STATE` timeline) or the
  channels drift apart. *⚠ Traps:* ⚠ A deliberate divergence, so document it on the cutscenes
  page beside the livery one; do not present it as the original's skip. ⚠ Do not raise the rate
  on an intro that arms a real skip, where the original's own force-stop is the behaviour. ⚠
  Realtime flown sessions and the parent-driven probe clock step differently; the multiplier
  belongs on the definition's dt, not on the session's `PhysicsDt`. *Cross-refs:*
  `docs/formats/anim-definitions/cutscenes.md` "Handoff and skip"; `docs/plans/PLAN-M5-polish-2.md`
  E24 (the decode that made the two scenes unskippable).

## Audio

- `BL-455` `[Feature]` **There is no audio options menu, so the music level is a hard-coded
  stand-in.** *Evidence:* at the controls the music drowned the briefing narration, so
  `MusicPlayer.ChannelLevel` now mixes the channel at 0.2 of the master bus. That constant is a
  stand-in for a control, not a tuned value: the original mixes music against a user setting, and
  with no options menu there is nothing to read. *Fix shape:* an options menu carrying at least a
  music level, then delete `ChannelLevel` and read the setting. *⚠ Traps:* do not re-tune
  `ChannelLevel` as if it were a fidelity constant; it is a placeholder and its own doc comment says
  to remove rather than adjust it. `MusicPlayer.Gain` deliberately stays the fade's own 0..1 value
  so the decoded ramp assertions still read what the decode describes. *Cross-refs:*
  `docs/org/music.md` for the fade rates the level does not affect.

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

- `BL-459` `[Feature]` **The damaged engine's re-arm delay is not ported.** When the engine slot's
  handle is not playing and the airframe is damaged, `FUN_004b18a0` does not restart the damaged
  loop at once: it accumulates the frame time into the airframe DEFINITION's field at `def+0x88`
  and only starts a loop once that total passes a threshold redrawn each frame as
  `3.0 + 2·rand()/32767` seconds, resetting the field to zero as it does
  (`docs/formats/vehicle.md`, "What makes an airframe damaged"). CSVM restarts the loop the frame
  the swap is decided, so a damaged aircraft coming back inside `AiEngineAudio`'s 2000 m cull is
  audible three to five seconds earlier than the original's would be.
  *How you would know:* an AI plane damaged below a quarter health, flown out past 2000 m and back,
  logs its `slot 0 -> snd_damagedengine` line three to five seconds after the `audible` line rather
  than beside it.
  ⚠ Traps: the timer sits on the **definition**, not the instance, so every aircraft sharing an
  airframe def shares one counter and one draw. Port that sharing or record why not, but do not
  quietly give each aircraft its own. A looped `snd_damagedengine` that is still playing never
  reaches the timer, so this is only reachable through the cull (or a stream that ends), which is
  also why it is small. Rejected: treating the delay as a crossfade; the transition is a hard cut.

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
  question with no reference recording behind it, to be judged at the controls rather than derived. ⚠ Judge only after `CAP-27` decides
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
  the same method `BL-269` already used for this signal chain.

- `BL-424` `[Research]` **What else a choked engine does besides swap its loop.** The swap itself is
  decoded and ported: the choker raises bit `0x2` of the disabled-systems mask, the engine-audio
  routine tests the whole mask for nonzero, so a choked aircraft plays `snd_damagedengine` at a
  drawn pitch exactly as a badly hurt one does (`docs/formats/vehicle.md`, "What makes an airframe
  damaged"). Both the own-ship and the AI arm read the same gate. What is left is the pair of
  functions the mask's bit-`0x2` edges call, `FUN_004b15c0` and `FUN_004b1630`, which
  `docs/org/ordnanceTypes.md` names the engine stop and restart without either having been opened:
  if they also cut a prop loop or fire a one-shot, a choke sounds like more than a definition swap.
  *Fix shape:* open both functions, then port whatever they do beyond the swap.
  ⚠ Traps: do not re-decode the swap, and do not read "engine stop" as an audio call on the
  strength of its name, since it sits on the thrust path's flag and may touch no sound at all.
  Rejected: gating the loop on `FlightController`'s engine-dead timer as a bespoke rule; the timer
  reaches the audio through the decoded mask test and needs no second path.
  *Cross-refs:* `BL-421` (closed; it confirmed the engine-audio model at the controls), `BL-285`
  (the loop's start/stop inputs), `BL-406` (closed; the choke itself landed there).

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

- `BL-260` `[Feature]` **The decoded death and flyby cameras are not implemented; shared static-camera world clearance is unwired.**
  `CamParams.cs` parses the fields, and the executable now settles every consumer and trigger
  ([`docs/formats/camparam.md`](docs/formats/camparam.md), [`docs/org/cameraViews.md`](docs/org/cameraViews.md)).
  **Death:** callback event `0x0f` enters mode 8 for the destroyed player; one fixed world point is
  chosen from a random `death_x` radius, a third local component of
  `−(speed·death_interval + death_z)`, `death_alt`, and `death_min_alt`, then held while the camera
  re-aims every frame
  (`FUN_0042e0b0`, callbacks `00470912`–`0047093c` / `0048072a`–`00480794`).
  **Flyby:** F7 enters mode 9; each re-site randomizes azimuth/radius, uses
  `speed·flyby_interval + flyby_z` for longitudinal placement, then waits the randomized watch time
  and re-sites on the frame after distance exceeds the randomized switch threshold
  (`FUN_0042e1f0`, `FUN_0042db40`). **Shared clearance:** `crash_chord_y` starts a vertical terrain
  world-collision ray that far above the candidate, and the field the executable names
  `crash_min_elev` (extracted
  as `crash_elev`) holds crash, death, and flyby at least that far above the highest hit
  (`FUN_0042c390`). *Fix shape:* implement modes 8/9 and one shared clearance helper; do not create
  independent hand-authored offsets. A capture after implementation judges presentation only.
  *⚠ Traps:* both `*_interval` fields are seconds multiplied by speed into distance, not re-frame
  timers. Preserve the decoded basis transform rather than assigning physical axes from field names.

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
  - (b) the impact sources' `magnitude_factor` constants are decoded (`bullet_impact` 5e-4,
    `missile_impact` 1e-3 plus `he_factor` 2.0 on HE rounds, both read at `FUN_004b9bc0`'s block
    1/2 kicks), but what each multiplies is not: the per-event drive quantity `FUN_004b9bc0` passes
    alongside `magnitude_factor` is undecoded, so CSVM's stand-ins (an incoming gun round reusing
    the caliber law, a rocket's armor damage doubled by `he_factor`) stay TUNE. A being-hit capture
    pins them; a further decode of `FUN_004b9bc0`'s own multiplicand would settle it without one.
    The `explosion` source's magnitude term is decoded as never filling in the original: the parser
    reads the key `max_magnitude` into block 3's slot while `shakes.zrd` authors `magnitude_factor`
    instead, so the retail explosion shake is zero regardless of blast damage
    ([`docs/org/shakes.md`](docs/org/shakes.md)). `PlaneShake.ExplosionAt` still kicks the authored
    `magnitude_factor` against a damage stand-in, so the port and the decode now disagree here;
    matching the original means zeroing that kick, not tuning it.
  - (c) the `ON_CALL` `small/medium/large` `damage_shakes` defs stay unwired — unknown caller,
    likely script/set-piece.
  - **(d) `high_speed` drives the same random-walk accumulator as the gun, decoded; the engine
    port is owed.** The per-frame player updater `FUN_0048c470` reads block 4's `min_speed`/
    `magnitude_quotient` fields and calls the identical `FUN_0042c070`/`FUN_0042be10` dispatcher the
    `fire_bullet` path uses, just on component index 4 (`camera+0xd4/+0xd8/+0xdc`) instead of 0,
    every frame the excess-over-gate law (`(speedRatio − min_speed)/magnitude_quotient`,
    `PlaneShake.SetSpeedRatio`) is positive. CSVM's `_speed` oscillator is a deterministic damped
    sawtooth, a different mechanism from the original's random walk, and next to the gun buzz now
    ported to its own random-walk step, the sawtooth dive rattle reads muted; in the Cockpit view
    at full speed it is too small to see at all, while the firing wobble reads. **Open/fidelity
    action:** give `_speed` a random-walk accumulator on the pattern of `_fire` (a
    `GunBuzzKickScale`-equivalent tune knob), fed by the existing `SetSpeedRatio` law, then playtest
    the dive against the original clip to judge the ported magnitude. Trace:
    `analysis/gun-wobble-shake/FINDINGS.md` (high_speed section) and
    [`docs/org/shakes.md`](docs/org/shakes.md).
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
- `BL-420` `[Research]` **Decode the original's per-view base FOV from `crimson.exe` and record it under `docs/org/` — the engine holds a single 62° assumption that the binary refutes.** The original's camera projection has **exactly two base horizontal FOVs, 60° and 80°, both stored in radians as half-angle constants** (`1.0471976` = `92 0a 86 3f` and `1.3962634`), and **which one applies is gated per-camera-mode** (live mode at `camera+0x14c`, selected in `FUN_0042b660`): mode **6** → 80° (`FUN_006024d9`), every other mode (0–5, 7, 8, 9) → 60° (`FUN_00602508`). Modes 6 and 7 are the only two first-person views (both set the `DAT_009fd17c` first-person flag via `FUN_004e7100`, both route through the first-person placement `FUN_0042d980`, neither uses chase-position math — `FUN_0042dc20`/`FUN_0042c5c0` dispatch). So the three named views resolve definitively: **3rd Person / chase = 60°; Cockpit view = mode 6 = 80°; Nose view = mode 7 = 60°**. The cockpit/nose assignment is pinned by a direct render gate: `FUN_0049fb00` (the per-frame player render, sole caller `FUN_004a0220` = main tick) draws the cockpit interior model `cockpit1` (`DAT_0071c314`) **only when mode == 6**, so mode 6 is the interior cockpit view (80°), and mode 7 is the no-interior forward view (60°). The two first-person views also share the **same camera position** — both place the camera at the plane's `cockpit_camera` marker (`DAT_0071c328/32c/330`), so there is **no separate nose-camera offset**; mode 7 differs only in hiding the interior/hull, keeping player head-look without autohead, and using 60°. The constants are **horizontal**; `FUN_006024d9`/`FUN_00602508` aspect-correct to stored vertical via `atan(tan(H/2) · (16:9)/(4:3))` → 60°→46.8° vertical, 80°→64.4° vertical. The project's current single **62° vertical assumption does not exist in the binary** — the 62°-in-radians constant `1.082104` (`63 82 8a 3f`) is absent, so the assumed number is unsupported and the correct base is 60°.
  *Evidence:* ghidra-mcp read of the open `crimson.exe` (`/crimson.exe`): `FUN_0049fb00` (player render; draws `cockpit1` `DAT_0071c314` only when mode==6 via `FUN_004cca30(x,1/0)` around the interior draw), `FUN_0042b660` (mode gate), `FUN_00602508` (60° H-FOV; writes `_DAT_00a1eff0`/`_DAT_00a1eff4`), `FUN_006024d9` (80° H-FOV, mode 6), `FUN_0042b570` (frustum/projection, contains `0.5235987755982` = 30° = 60°/2), plus the 60°/80°/50.0/2.5 constants side-by-side at the data table `0060409c`. FOV is stored in radians (anim loader `FUN_00502da0` converts degrees→radians via `0.017453292`). The `0x3f860a92` 60° literal is also used by `FUN_0049d940` (player aim camera) and `FUN_004a0220`. Camera object is `DAT_0064ef78`. Placing the camera: both first-person modes run the same placement `FUN_0042d980`, which sets the camera to `plane_pos + plane_rot · (DAT_0071c328,32c,330)`, i.e. the plane's `cockpit_camera` marker offset (bound in `FUN_00473480` from the `cockpit_camera` node; default fallback `DAT_0075d1b8/bc/c0` = `(0,0,0)`). Plane-model `cockpit_camera` node translations (decoded from `extracted/C1/... planes/nodes.json`) put the camera on the fuselage centerline a bit above the local origin — default fighter `player_pfighter`: `(0, +0.75, −0.2)` — with +Y up, ±X the wingspan (ailerons at ±63, elevators/tail at −Z ≈ −37), so +Z = nose/forward and the marker is centered, ~0.75 up, marginally aft of the origin. There is **no `nose_camera` node or per-mode offset** — mode 7 reuses the cockpit_camera point. The `cam_anim` ZAN cockpit sequence (`player-gi_1stperson`) carries no FOV (it shows the interior/hides the plane via `cockpit1`/`camera1`), so the base FOV is not authored in `.ani` data.
  *Fix shape:* **the decoded facts landed as [`docs/org/cameraViews.md`](org/cameraViews.md) (2026-08-18), and the mode-6/mode-7 first-person half of the model landed in code** (`PLAN-cockpit-view.md` A3): `CameraController.HorizontalToVerticalFovDeg` renders Cockpit at 80° H and Nose at 60° H, both aspect-corrected off the live viewport, deliberately scoped to those two new modes only (Decision 3, "new modes only") and never touching the engine's 62° global. **What remains is the EXTERNAL half.** `GameSession.cs:475`/`:2624` and `Launcher.cs:490` still write the single 62° vertical global to every chase/fixed-numpad/back/pad-look/crash camera. Migrating those three sites to the decoded 60° horizontal base (with the aspect-corrected 46.8° vertical this page already pins) is the remaining work, and it unsettles two judgements made against the current 62°: `PLAN-overcast-match.md:1463`'s overcast sky match and `docs/org/tracers.md:258`'s tracer calibration. Carry that warning into whichever session does the migration — both need re-judging after the base FOV moves, not just re-measuring against the same footage.
  *⚠ Traps:* (i) **The two `CAMERA_STATE`/`CAMERA_FROM_TO` functions (`FUN_00502da0`, `FUN_00503e70`) are animated/in-script FOV changes only (`.ani` H/V_FOV events) — not the base per-view FOV; do not wire the engine's base FOV to them.** (ii) **The 80° is attached to camera mode 6 specifically, not "first person" generally** — mode 7 is also first-person but is 60°, so gating on "is first person" alone would read the mode-7 number wrong. (iii) The `Virtual Cockpit` string is a HUD/perf/zoning label (`FUN_0059c340`), not a view — ruled out. (iv) ~~Which of cockpit vs nose is mode 6 (80°) vs mode 7 (60°) was not pinned~~ — **resolved**: the `FUN_0049fb00` render gate (`cockpit1` drawn only when mode==6) pins mode 6 = Cockpit (80°) and mode 7 = Nose (60°). The remaining subtlety is that **both modes share the same `cockpit_camera` position** (no separate nose offset exists), so "nose" is a render/head-look/FOV variant of the same camera point, not a physically different marker. (v) "62°" invariants elsewhere are the assumption being corrected, not corroboration.
  *Cross-refs:* `docs/org/cameraViews.md` (the landed Nose view shares the Cockpit camera point but uses the 60° base), `BL-150` (numpad fixed-view FOV calibration is still missing — a documented 60°/80° base + the aspect conversion is the calibration input it needs), `docs/formats/camparam.md` (chase/tuning only; does not cover FOV), `PLAN-overcast-match.md:1463` and `docs/org/tracers.md:258` (the 62° assumption to correct), `PLAN-cockpit-view.md` A3 (landed the mode-6/7 half of this model).

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

- `BL-496` `[Feature]` **The aiv `ace` flag reaches the entity and nothing is known about what it
  does there.** *Evidence:* found by G75 while binding the pilot name. Slot 67 `ace` is read by the
  block reader into `CCEVeh+0xa4` and carried by the spawn path into entity `+0x988`. That field has
  four touches program-wide: the constructor default, that write, a read at `0x0047cde2` sitting
  immediately ahead of the skill block, and a read in a runtime function that was not chased. So the
  flag gates something in skill interpolation, and 26 blocks across the campaign carry it. CSVM
  parses the slot and uses it for nothing. *Fix shape:* decode the `0x0047cde2` branch and the
  runtime read before changing any rating, since what an ace gets is the question and "it is flagged"
  is only the input. *⚠ Traps:* the flag is narrower than a complete skill vector (26 blocks against
  29), so the two are not interchangeable and neither is a proxy for the other. Do not give aces a
  blanket rating bonus on the strength of the flag alone; the branch may scale an interpolation
  rather than add to it. *Cross-refs:* `PLAN-M5-polish.md` G75.

- `BL-113` `[Tuning]` `[Owed-playtest]` **Compass tape** — `TileOverscan` / `RimGain` / the nearest-tick look remain TUNE
  (north = −Z is now confirmed against the original, 2026-07-30 — do not reopen).

- `BL-181` `[Tuning]` `[Blocked: a shared type scale]` **Marker HUD + scoreboard layout is a provisional pass, not a
  fidelity sign-off.** Playtested 2026-07-30
  (`./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`): `MarkerHud`/`StuntScoreboard` placement,
  fonts and distance units "work for now." The verdict is explicitly contingent: it says these read
  acceptably in isolation, and a fidelity sign-off needs them read against the chrome the rest of the
  game's UI uses, which does not exist yet. ⚠ **The composed campaign boards do not discharge this,
  and should not be read as doing so.** They are painted original artwork positioned at authored
  pixels with a per-background ink palette (`BoardPalette`), so they carry no type scale, no distance
  units and no shared font choice for an in-flight overlay to match. What this waits on is a UI
  surface that defines those three things for chrome the original did not paint, which is what the
  menu-hub milestone was standing in for. Blocked on that surface, not on data.
  *Fix shape:* re-review `MarkerHud.cs`/`StuntScoreboard.cs` placement once such a type scale exists,
  against it rather than in isolation. *Cross-refs:* `BL-449`, whose landing prompted this wording.

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

- `BL-431` `[Feature]` **The screen-space `GaugeCluster` doubles up over the driven 3D panel in
  first person, and whether it should is undecided.** The drive itself has landed:
  `CockpitGauges` (`src/Flight/CockpitGauges.cs`) binds the needle nodes, the artificial-horizon
  ball, the two warning lamps, the belt lights, the damage zones and the character readouts inside
  the pilot's own `cockpit1` and writes them each frame from the readings `GaugeCluster` already
  holds, so the authored panel and the screen-space dials cannot disagree.
  `FlightController` applies it on exactly the frames `CockpitVisibility` puts the interior on the
  screen. The lamps stay parked by `PlaneBuilder.ParkInteriorStates` at build, which is still right:
  a build with no rig driving it (every lab and suite) must render a pristine cockpit.
  *What remains:* the choice this item was filed with, now the only open part. In Cockpit the 3D
  panel and the flat dials both read live, one over the other. Retire the screen-space cluster in
  first person, move it, or keep the doubling. Nothing in the original settles it (see below), so
  it is a judgement at the controls.
  ⚠ *The `POSITION_1ST` half of this item is answered and carries no work.* It was filed asking
  whether `GaugeCluster` should adopt a first-person layout from `hud_v2.zrd`'s
  `POSITION_1ST`/`POSITION_3RD` keys; `FUN_00454e70` reads those into a per-section debug text
  column (`AIR_SPEED`, `ALTIMETER`, `GUNS`, `MISSILES`, `HEALTH`, `NITRO`, one x at 0.02 spacing
  in y, written only under `DAT_00624df0`), not into dial placement. There is no per-view gauge
  layout in the original to port, so the retire-or-double-up choice above is CSVM's own call
  (`docs/formats/hud.md`, "Cockpit gauges").
  *The decoded laws are in the code and in* `docs/formats/hud.md`, "Cockpit gauges", which carries
  every address and condition. Both lamps changed on the way in: LOW ALT now lights below 60.0 m
  AGL rather than a guessed 50 m, and its blink ramps (`0.14 + 0.006·agl_m`) rather than sitting at
  a fixed 400 ms; STALL gates on available load factor under 2.35 g rather than a speed fraction,
  with a half-period bounded to (0.100, 0.400] s. The needle laws confirmed what was already
  shipping. ⚠ The lamps and the flight model share one dt in the original, so none of these takes a
  k = 1.390 conversion. The damage-dial blink (5 s, 0.32 s) is NOT in the gauge cluster and stays
  undecoded, an open TUNE.
  *Residue:* nothing is hidden for this, and nothing needs to be. The instruments were reported
  flickering, and the mechanism turned out to be the mount scale rather than the missing needle
  drive: every depth bias `SceneBuilder` emits is a fraction of VIEW DISTANCE, so mounting the
  interior at `PlaneBuilder.InteriorScale` (0.04) left one authored priority level worth about
  86 µm of depth at the panel's 0.42 m, below the float noise of a view-space transform computed
  at a chapter's world coordinates. The bezel rings that ring each instrument (`horizn` at
  priority 1 over the `dash` panel at 0) swapped winner with the panel from frame to frame. The
  interior now builds on its own `SceneBuilder` carrying `DepthBiasScale = 1/InteriorScale`, which
  restores the absolute separation the authoring assumed; the airframe, the world and all four
  plane-bearing goldens are untouched by it. The per-instrument jitter that survived that fix was
  float32 rounding of the interior's world transform at chapter-scale coordinates, and the
  interior now draws in its own origin-relative pass (`Flight/CockpitOverlay`,
  `--no-cockpit-pass` opts out). One observation stays: pitched up toward the sun, the compass
  drum's upper face reads as a bright bar inside the window, with and without the pass, so it is
  the authored geometry under the sun rather than a render defect; whether the original shows it
  is unchecked.
  *Cross-refs:* `PLAN-cockpit-view.md` B11 (parked the states; also settles that the `gauges` child
  itself must stay visible — it is not a needle overlay). The windshield bullet-hole decals
  (`bullet1`-`bullet5`) share the same parked-state mechanism but are driven by the unrelated
  `cockpit_bulletholes` anim-def family (`docs/plans/PLAN-m3-polish-5.md:453`), not this item.
  Neither that family nor either other `PLAYER_1ST_PERSON` def (`muzzle_burst`, `player-1`'s
  `pdpanel4`/`pdpanel6`) targets any node inside `gauges`, and no runtime binds a plane's own
  subtree apart from the crash rig's narrow subset — so nothing animates the panel per frame.

- `BL-510` `[Feature]` **The auto-land prompt shows a placeholder line instead of the original's
  `langui` string.** *Evidence:* the original lights message `0xb5` (or `0xb6` for a pad binding)
  when the approach table's `auto` row passes (`FUN_0045e120`). `ExtractRof.ps1` already produces
  `extracted/rof/ui_strings.json` from `langui.dll`'s STRINGTABLE (1247 rows), but ids `181` and
  `182` are not present under that table's numbering, so `FlightHud.AutoLandPrompt` ships a
  plain-English stand-in marked as such. *Fix shape:* find how the exe maps a message id onto the
  STRINGTABLE (an offset or a second table), resolve `0xb5`/`0xb6`, and draw the resolved string
  through `UiStrings`. *⚠ Traps:* ⚠ Do not guess the wording; the placeholder stays until the
  mapping is decoded. ⚠ The binding is `F9` / left-stick click, not the original's `A`
  (`docs/controls.md`), so a resolved string that names the key needs the port's key substituted.
  *Cross-refs:* `docs/plans/PLAN-M5-polish-2.md` C10.

## Splitscreen

Our splitscreen mode (2–4 players) has no counterpart in the original, so every rule it authored
against "the player" or "the camera" needs an explicit splitscreen verdict: generalise it, take a
nearest/union rule, or record it as deliberately single/global. This theme collects that work
(sweep of 2026-08-15). **Route fixes through the two existing seams instead of minting new ones:**
`GameSession.PlayerPositions` (nearest human) for gameplay rules that say "the player", and the
viewer set behind `ProjectilePool.Viewers` / `ScreenSize.NearestFloor` for draw rules that say
"the camera". Sim state stays global — the mission wind is the worked example
(`Session/WeatherRig.Tick`, stepped once per frame outside the per-rig loop on purpose). Splitscreen-scoped items that live with
their own system: `BL-537` (the 4-player pool judgement), `BL-296` (per-player ActionMap), `BL-299`
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

- `BL-501` `[Feature]` **Nothing exercises avoid-crash probing between aircraft flying one net in
  formation.** *Evidence:* flagged by G77, which fixed the branch draw that split CM02's three
  bombers and then measured them holding 82 m to 219 m apart on one route. That suite builds its
  world with collision off, so the probing three aircraft at roughly a hundred metres would do to
  each other is never run, and that spacing is exactly the geometry that can arm it. *Fix shape:*
  drive the same formation in a collision world and see whether avoid-crash promotes, and if it
  does, whether it takes an aircraft off its route. *⚠ Traps:* do not widen the formation to quiet
  a probe. The separation is what the net and the cross-track carry produce from authored data, and
  a formation that holds only because its members are far apart is not the one the original flies.
  *Cross-refs:* `PLAN-M5-polish.md` G77, whose `campaign-bomber-formation` suite is the harness to
  extend.

- `BL-502` `[Feature]` **`SET_AI_NET` and `SET_AI_TEAM` reach no zeppelin.** *Evidence:* found by
  G80, which wired both clauses for roster-spawned aircraft and could not carry the same lookup to
  airships. Six clauses across four missions name one: `blackswanzep` (C1C/M01), `blackhatzep`
  (C4/M05), `piratezep` (C5/M04) and `cargozep2`/`cargozep3` (C2/M05 and C4/M05). None is in CM02,
  so no mission the player is currently trying to finish depends on this. `ZeppelinMotion` holds its
  net follower and `LiveZeppelin.Team` read-only, so this is a change to `ZeppelinRuntime` rather
  than to the director's lookup. Those names surface through the `Gap` line today, so a mission
  hitting this says so. *Fix shape:* give `ZeppelinRuntime` the same two writes the aircraft arm
  got, re-seating the follower from the airship's current position rather than restarting its route.
  *⚠ Traps:* a zeppelin is not a vehicle in the original and does not run the nose-aligned edge pick
  (`PLAN-M5-polish.md` G77), so a re-seat here keeps the nearest-node rule and must not inherit the
  aircraft path's heading argument. The record's own team fans across the whole airship including
  its guns, so a script-side team write has to fan the same way or half the hull keeps the old side.
  *Cross-refs:* `PLAN-M5-polish.md` G80, which landed the aircraft arm.

- `BL-469` `[Feature]` **An escort cannot hold station on a leader using nitro, and nothing measures
  the case.** *Evidence:* the two injectors are independent switches, so the asymmetry is reachable
  in a real game: a wingman gets one only when its own `aiv` block authors `nitro` slot 34
  (`AiFlightAssembler.cs:112` through `AiSkills.RosterNitro`, and only three shipped blocks author
  1), while the player's comes from their own customised aircraft
  (`HumanFlightAdapter.cs:138`, `CustomPlaneBuild.HasNitrous`). A player who has bought an injector,
  escorted by a wingman whose block authors none, gives the leader a thrust term the escort cannot
  command at any lever, so no desired-speed ceiling can keep it in place. `wingman-station` measures
  the symmetric no-nitro case on both sides, which is the right default and the one the reported
  symptom came from, but its comment does not say so. *Fix shape:* a second `[flown]` leg with the
  leader's `Nitro.Installed` true and the wingman's false, and then a decision about what the escort
  should do when it cannot match its leader at all. *⚠ Traps:* do not fold this into `BL-457`: that
  item's numbers are the symmetric case and are sound; this is a different pairing. Do not "fix" it
  by installing nitro on every wingman, which would contradict the roster data.


- `BL-545` `[Bug]` `[Owed-playtest]` **The landing animation plays with no hook, the aeroplane too
  high, and unfolded wings.** Seen at the controls on CM02's auto-land: the landing hook was not
  deployed, the aeroplane sat too high on the trapeze, and a Balmoral folds its wings in the
  original's landing cutscenes. *Fix shape:* three separate reads of the hookup definition and the
  airframe's own nodes (the hook and the wing-fold are per-airframe animated parts, the height is
  the `AT_NODE` pose's offset). *Cross-refs:* `BL-544`; `docs/plans/PLAN-M5-polish-2.md` C10 and C12.

- `BL-426` `[Bug]` **A failed stunt mission records and announces a new best time.** Seen at the
  controls: losing an Instant Action stunt run still shows NEW BEST on the wrap-up.
  **The mechanism.** `InstantActionDirector.StuntSummaryFor` (`Session/InstantActionDirector.cs:761-764`) calls
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

- `BL-446` `[Feature]` **The MPG movie cinemas do not play.** *Evidence:* Decision 2 of
  `docs/PLAN-M5-campaign.md` put them out of scope for the campaign milestone: plain MPG playback
  is a codec and container problem orthogonal to the campaign flow, and the loop reaches the cabin
  and the mission without one. **The decision this entry was waiting on is made and recorded in
  [`docs/formats/cinemas.md`](docs/formats/cinemas.md); what remains is the work itself.** The ten
  shipped files are MPEG-1 system streams, MPEG-1 video 320x240 at 856 to 1500 kbps with MPEG-1
  audio layer II at 44.1 kHz, and Godot 4.7 compiles in exactly one video decoder, Ogg Theora, so
  they cannot play as they ship. The decision is to transcode at extract time to `.ogv` and play
  through a stock `VideoStreamPlayer`, with a C# `VideoStreamPlayback` subclass recorded as the
  reversible alternative. *Fix shape:* the transcode step in the extraction pipeline and the player.
  *⚠ Traps:* two files break the otherwise uniform profile and a reader must not assume one
  (`msopen1.mpg` is 29.97 fps at 1500 kbps, `crimflag.mpg` is mono). `fmv.zrd`'s `PLAYAVI` actions
  name `MSopen1.mpg`, `zipper.mpg` and `Chap0.mpg` in a case the on-disk names do not have, so a
  case-sensitive lookup fails on all three. The chapter array at `0x0061e68c` names `chap1.mpg`
  through `chap6.mpg` and `chap6.mpg` has no file in the install. Nobody has judged a transcode at
  the controls, which is a presentation call and not a technical one.
  *Cross-refs:* `docs/PLAN-M5-campaign.md` Decision 2, which filed it; `docs/formats/cinemas.md`.

- `BL-463` `[Feature]` **The cabin ships without Change Memento.** *Evidence:* Decision 3 of
  `docs/PLAN-M5-campaign.md` deferred it: the function is cosmetic and rests on the undecoded
  snapshot flow, so the cabin's other rows shipped without it rather than waiting.
  *Cross-refs:* `BL-256` is the adjacent snapshot work; `docs/PLAN-M5-campaign.md` Decision 3.

- `BL-521` `[Bug]` **CM04 (C3/M03)'s barrage balloons should already be destroyed at mission start.**
  *Evidence (two leads from the sortie log, still open):* at the controls the balloons stand at
  mission open. The log shows the AI gunners engaging them as live turrets on the first frames
  (`ai gunner: shooter 100 targets MSG_TUR_DEFENSE_BALLOON@b_turret3 at 1027 m`, then `b_turret5`
  and `b_turret4`), although `support\c3\m03.gw` switches `bont1..6`/`b_turret1..6` fully OFF
  (`NodeSetActive off`): either that switch does not reach those nodes' turret registration, or it
  does not reach the nodes at all, and the balloons the report names are these. Later in the run,
  `anim: WAIT_FOR_COMPLETION on 'balloon_downa*' had nothing to hold — no live callee instance`:
  something in M03's running world calls the `balloon_downa*` defs, which live in
  `data\common\zrdr\turrets\balloon_down.zrd`, a reader def compiled only into M02's `mis_anim`
  (`bont1-balloon_downa1.json` and siblings) and superseded in M03 by the mission's compiled
  manifest, so the call reaches nothing. *Fix shape:* first confirm which nodes the `.gw` OFF switch
  reaches (`mission setup: ... 36 node(s) deactivated`) and whether a deactivated `b_turret*` stays
  in the turret target pool with its balloon visible; then find the caller of `balloon_downa*` in
  M03 (a chapter-scope reader def or the turret def itself) and settle whether the original resolves
  that call against the chapter's reader set where CSVM's manifest supersession drops it.
  `AnimRuntime.SyncDestructiblePool` applies once the trigger is a role-named swap. *⚠ Traps:*
  `PLAN-c3-balloon-kill-chain.md` settled the balloons' kill chain for C3/M02; that is the live kill
  path and not this mission's start state, so do not reopen it. *Cross-refs:* `BL-348`'s plan,
  `docs/formats/anim-definitions.md` (the `bont*`/`balloon_t*`/`tether*` state events).

- `BL-522` `[Bug]` **A surface generator's launch does not fly the take-off run it is placed on.**
  *Evidence:* the launch pose is decoded and landed (`docs/formats/mission-entities/enemy-generators.md`
  "Launching from a surface host"): a launch starts on `<base>_aip0` plus 0.2 m, nose on
  `<base>_aip1`, at zero velocity with the throttle open. The original then keeps the path on the
  aircraft at `+0xc8` with the flag at `+0xcc` and flies the remaining points as a take-off run,
  which also suppresses the net-nearest-node snap an ordinary activation makes (`FUN_004b0f40`).
  CSVM hands the aircraft straight to its patrol net from that pose instead, so an aircraft that
  starts at rest on a deck or a runway has no authored way to reach flying speed. Affects all three
  surface hosts: `barracuda` (C3, a 4-point path climbing 2.5 m to 10.8 m over 130 m), `eairg31`
  and `eairg32` (C1, 5-point flat ground rolls). *Fix shape:* decode the consumer of `+0xc8`/`+0xcc`
  on the aircraft record and fly the path, releasing to the patrol net at its last point. *⚠ Traps:*
  the zeppelin launch-altitude gate is a decoded rule for airships and not a general one; do not
  lift the launch by borrowing it, and do not add a spawn-height offset beyond the decoded 0.2 m.
  Do not give the aircraft a starting speed instead: the zero velocity is decoded. At the controls,
  on the landed launch pose, the fighters still die at once: `britpeace_eg0..3` launch in turn at
  `(-12028,8,-11597)` on the `bauda_aip*` path and each is destroyed on the same frame (`AI ram into
  sub_doors/col — destroyed outright`, then `sub_runway/col` twice and terrain `g627/col`,
  `CRASH ... spd=75..78 m/s`), so a launch placed at rest on the deck is killed by the AI ram rule
  before any take-off run could begin, and the wrecks' crash damage is what destroys the Barracuda
  with almost no fire from the player (`BL-515`). The take-off run is the fix; while it is unbuilt,
  a launched aircraft standing on its own host's colliders must not count as a ram.
  *Cross-refs:* `BL-512`, `BL-515`, `BL-527` (CM07's second patrol is a launch off `eag31`/`eag32`,
  so it is re-judged against this), `docs/org/flightModel.md`'s scripted-path follower (CM09's
  ground airfields launch off the same take-off paths, `eag31`/`eag32` in C1/M04).

- `BL-523` `[Bug]` **The AI's patrol/pursue/lay-off cycle does not match the original: CM05's
  second patrol never pursues, and CM09's enemies fly up to 80 km away.** *Evidence:* two
  symptoms of one mode machine, reported at the controls. In CM05 (C3/M04) the second enemy patrol
  stays on its net around the Pandora with the player in range and never engages; in CM09 enemy
  aircraft leave the mission area and end up tens of kilometres out. `AiModeMachine` promotes
  `Patrol` to `Pursue` on its own gates (`AiModeMachine.cs:314`); a patrol that never leaves the
  mode either never sees the player as a candidate (team, rating bias, range) or has its promotion
  gated by a net flag. The fly-away is the other end of the cycle: a pursuit that overshoots and
  never lays off, or a fly-away on losing its target; the original's AI has a return rule that
  ours lacks. *Fix shape:* run both
  missions headless with the AI trace on; for CM05 read the second patrol's mode transitions and
  candidate scan against the first patrol, which does engage; for CM09 log the far aircraft's mode
  and target over the run. Then decode the promotion gate and the distance or lost-target rule in
  `AiModeMachine`'s source functions. *⚠ Traps:* `PLAN-M5-polish.md` 3450 recorded the
  never-pursues impression for wingmen and it closed on a different cause; check the log rather
  than reusing that answer. Do not add a leash constant; `docs/org/aiPilot.md` records that patrol
  is built on a spawn table, and the return rule has to come from the decode.
  `BL-524`'s half of the fly-away is settled and is not a lay-off question. Neither CM05 nor CM07
  has a netless friendly patrol, and the original has no netless-patrol and no leaderless-wingman
  branch at all: `FUN_0041d1f0` indexes -1 on an unresolved net with no guard, `FUN_0041e760`
  dereferences its leader at `+0x2fc` with no null check, and `FUN_0049c880`, which would release a
  wingman onto the chapter's first net, has no callers. The remaining fly-away path in CSVM is
  `AiPilot.FlyPatrol`'s netless arm reading `TargetHeadingDeg` and `TargetAltitude` after
  `FlyPursuit` has overwritten them, so a pilot with no net and no leader holds the last bearing to
  a dead target.
  *Cross-refs:* `BL-524`, `docs/org/aiPilot.md`.

- `BL-550` `[Feature]` **The AI's altitude floor is enforced at one site in CSVM and at three in
  the original: the manoeuvre veto and the mode-5 global disable are both missing.** *Evidence:*
  decoded from `crimson.exe` while reading the cockpit lamps for `BL-431`. `AiModeMachine`'s
  `AltitudeFloorM` (20 m world Y, `DAT_0071c3f0`) and `ProbeCeilingM` (8000 m, `DAT_0071c3f4`) are
  the right constants and the climb-out that arms below the floor is the right behaviour, but the
  original reads those two globals at **four** sites, not one. We have the reactive arm
  (`AiModeMachine.cs:529`) and the goal clamp (`FUN_0041b560` at `0041b5c5`, ours as
  `AiControlLaw.AimAltitudeFloor`). Missing: (1) the **manoeuvre veto**, `FUN_004201a0` at
  `00420405`, which rejects a candidate manoeuvre whose PREDICTED end point falls below the floor
  and takes a separate branch at `0042042d` when it lands above the ceiling, so the original never
  *starts* a programme that would fly the aircraft into the ground; ours culls only on natural
  touch and signature weight (`AiModeMachine.PickManeuver`). (2) `FUN_004216e0`, which sets mode 5
  after writing `DAT_0071c3f0 = -FLT_MAX` at `004216f5` and restoring it at `00421775`. Since the
  floor is a **global**, that suspends it for every AI aircraft in the mission for the duration,
  not just the one entering the mode. *Why it matters at the controls:* an AI that commits to a
  split-S at 60 m flies it into the terrain and is saved only by the reactive climb-out, which
  abandons the manoeuvre mid-programme. The visible difference is enemies that pick sane
  manoeuvres near the deck rather than starting doomed ones and yanking out. *Fix shape:* add the
  predicted-end-point test to `PickManeuver`'s cull, reusing the `Maneuver` programme's own end
  pose rather than inventing a predictor. The mode-5 disable needs the floor to stop being a
  per-machine constant, so decide first whether CSVM models it as a session-scoped value or
  declines the global scope deliberately. *⚠ Traps:* the floor is flat world Y and NOT a terrain
  follow, so it saves an aircraft over water and does nothing over a ridge; the forward probe is
  what handles terrain, and neither is a substitute for the other. Do not fold the veto into the
  reactive arm, which is a different mechanism at a different moment. ⚠ Whether the original's
  mode 5 is worth reproducing at all is open: a global floor disable reads like a deliberate
  licence for one scripted manoeuvre, and porting it faithfully means every other AI aircraft
  loses its floor at the same time. *Cross-refs:* `BL-523` (the same mode machine's
  patrol/pursue cycle), `docs/org/aiPilot.md`, `BL-431` (the decode session that found this).

- `BL-524` `[Bug]` **CM05 (C3/M04) and CM07 (C1/M02): a friendly wingman whose leader leaves play
  flies away after its first fight and never returns to escort.** *Evidence:* reported at the
  controls in two missions: once the first enemy patrol is destroyed, friendly aircraft continue on
  their last heading out of the mission instead of rejoining. The filing blamed a missing net and
  both halves of that are disproven. Neither mission has a netless friendly patrol: the netless
  friendly blocks are `wingman_1/2/3` (CM05) and `wingman_2/3/4` (CM07), every one a `mode wingman`
  naming a leader in `primary_target`, and every friendly that flies a route is netted on an id its
  chapter carries (C3 ids 11 `M4Bravo` and 12 `M4Charlie`, C1 ids 25 `M2Charlie` and 26 `M2Bravo`),
  so the spawner drops nothing and `BL-453` is not the cause. There is also no lay-off return rule
  to decode: `lay off` is derived rather than stored, the netless follower path `FUN_0041d1f0`
  indexes -1 with no guard, the escort law `FUN_0041e760` dereferences its leader at `+0x2fc` with
  no null check, and the release routine that would hand a wingman the chapter's first net
  (`FUN_0049c920`, reached only from `FUN_0049c880`) has no callers in the image. What remains is
  CSVM's own invented fallback: `AiPilot` drops an escort whose `Leader.InPlay` goes false to
  `FlyPatrol`, and with no net that arm projects `TargetHeadingDeg` and `TargetAltitude`, which
  `FlyPursuit` overwrote with the bearing to its quarry, so the pilot holds the last bearing to a
  dead enemy for the rest of the mission. Both missions flown headless confirm the roster half
  (CM05 spawns 3 escorts on `player`/`devastator_1`/`devastator_2` with the leaders netted
  `M4Bravo#11` and `M4Charlie#12`, CM07 the same shape on `M2Charlie#25` and `M2Bravo#26`, nothing
  dropped) and the trigger (`devastator_1` is shot down in CM05, leaving `wingman_2` netless and
  leaderless); the drift itself did not show, because that wingman held a target until it too was
  shot down. *Fix shape:* reproduce the last step, a leaderless netless pilot that also loses its
  target, then decide what such a wingman does, which is a decision the binary cannot make for us
  since the engine's own answer to "a wingman stops escorting" is unreachable code. *⚠ Traps:* do not give
  them the player's escort law as a default; `BL-457` shows the escort hand-off is itself
  unsettled. Do not add a leash constant. *Cross-refs:* `BL-457`, `BL-502` (`SET_AI_NET` reach),
  `BL-523`, `docs/org/aiPilot.md`.

- `BL-525` `[Bug]` **CM06 (C1C/M01): the second docking at the Workers' Voyage (to collect Dr. Fassenbender)
  completes without docking.** *Evidence (traced):* `objectives.zrd`'s two docking objectives
  (OBJECTIVE11, first hook, `ANIM_STATE wv_drop_copilot RUNNING`; OBJECTIVE15, second hook,
  `ANIM_STATE wv_pickup_copilot EXECUTED`) both gate on an animation the hook node's own script
  (`extracted\C1C\M01\zrdr\wv_tailhook.zrd.json`) runs. That script's `wv_initiate_hookup` sequence
  `CALL_ANIMATION`s `wv_drop_copilot`, `wv_pickup_fassenb` and `wv_pickup_copilot` unconditionally,
  every time the player docks; only each definition's own `ACTIVATION_PREREQUISITE`
  (`REQUIRED [OBJECT_ACTIVE_LIST [[wv_tailhook, dropoff_node]]]` /
  `[[wv_tailhook, pickup_node]]`) is authored to keep the wrong leg from running. CSVM drops that
  shape entirely: the reader parse (`AnimDefs.cs`, the `ACTIVATION_PREREQUISITE` case) only reads
  `OPTIONS [MINIMUM_TO_SATISFY, ANIMATION_LIST]` (the zeppelin hull-death form), and the compiled
  parse (`CompiledAnim.cs Parse`, `activ_prereqs`) only reads entries shaped `{"Animation": ...}`,
  silently dropping the `{"Parent": ...}` / `{"Object": ...}` node-active-state shape every dock,
  pickup and panel prerequisite in the extracted data actually carries (the same shape censuses on
  roughly 50 files: zeppelin gasbag panel finishers, `chuteman`'s drop-direction gate,
  `pzep_cargo_point`'s cargo stop). `ZeppelinRuntime.cs` is the only consumer of the parsed
  `PrereqAnims`/`PrereqMinToSatisfy` fields, so nothing enforces the node-active form anywhere. Net
  effect: `wv_pickup_copilot` reaches `EXECUTED` on the FIRST docking already (`dropoff_node` active,
  `pickup_node` not), so `OBJECTIVE15`'s `ANIM_STATE` condition is already true the moment it wakes,
  on the second-docking nap chain, well before any real second hook-up. At the controls the same
  unconditional calls show as a docking that ends too early and, a few seconds after the player
  has released and flown off, a teleport back onto the hook: the wrong leg's own hook-up
  choreography (`wv_pickup_*`, which puts the flown airframe back on the trapeze) runs on the
  first docking because nothing enforces its `pickup_node` prerequisite.
  *Fix shape:* parse the node-active `ACTIVATION_PREREQUISITE` shape on both paths (reader
  `REQUIRED [OBJECT_ACTIVE_LIST [[path...]]]`; compiled `Parent`+`Object` entry runs, the `Object`
  leaf's `active` field the required state) into a path/required-state list on `AnimDefinition`, and
  enforce it generically at `AnimRuntime`'s `CALL_ANIMATION`/`Start` dispatch (silent skip when
  unmet, mirroring the existing hull-death gate's silence). `ObjectiveGraph`'s own reading of
  `ANIM_STATE` is correct against the decode and needs no change; the gap is entirely
  `AnimRuntime`/`CompiledAnim`'s, and its blast radius (~50 defs across several chapters) makes it
  its own item rather than a docking-local patch. *⚠ Traps:* `ObjectiveGraph.ScanForCompletion`
  resolves one objective per tick round-robin (`BL-458`), so a completion can land frames after its
  cause; that round-robin is not this bug's mechanism. Do not hardcode a CM06-specific exception in
  `AnimRuntime`: the prerequisite is data-authored and general, and a docking-only patch would leave
  the gasbag/cargo/chute defs carrying the same shape unfixed. *Cross-refs:* `BL-458`.

- `BL-527` `[Bug]` **CM07 (C1/M02): the second patrol, a single Peacemaker, spawns under the ground.**
  *Evidence:* reported at the controls: the aircraft appears below the terrain. Not a roster
  placement: `aiv.zrd`'s four enabled `blakepeace_2_1`..`_4` blocks are the whole formation-roster
  spawn, and a headless run (spawn log now on `CampaignDirector.BuildRoster`'s `campaign: roster`
  line) places all four ~122 m above the measured terrain height, so that path is clean. The
  mission's only other Peacemaker-def blocks are `blakepeace_2_5`/`_2_6`, both authored `enabled 0`
  (`docs/formats/ai-rosters.md`'s generator-parameter-template slot), so the second patrol is a
  generator launch, not a roster one, the same class of bug as `BL-522`'s Barracuda fighters. CM07's
  `egen.zrd` runs two live generators (`eairg31`/`eairg32`) whose `vehicle.params` names the
  disabled AIV block a fresh spawn is configured from; that join is built
  (`CampaignRosterPlan.GeneratorTemplates`, asserted by the `generator-roster-params` suite; both
  C1/M02 records point at `Eairg31_params`, so both hosts launch `blakepeace_2_5` by the data's own
  doing), and the `player_bhawk` on the egen load line is the fallback-plane label, not the launch.
  The launch pose is decoded and landed (`BL-522`): it now sits on `eag31_aip0`/`eag32_aip0` plus
  0.2 m instead of the host origin, and the generators are gated behind four `WAKEUP_GENERATOR`
  credits, so no headless run launches there without playing the mission. Re-judged at the
  controls on the landed launch pose: the launches now come out of the hangar in the air but crash
  at once. The sortie log shows `blakepeace_2_eg0..eg4` launching in turn and `eg1`, `eg2`, `eg3`
  and `eg4` each ending on `AI ram into a5/col — destroyed outright` at about `(-5940,165,-4164)`,
  `spd=104 m/s`; `eg0` survived long enough to pursue. So the residual is `BL-522`'s take-off run:
  handed to the patrol net from the launch pose, the aircraft flies through `a5` (a structure
  beside `eag31`'s path) instead of along the authored run that clears it. *Fix shape:* `BL-522`,
  not a `CampaignRoster.cs` change. *⚠ Traps:* do not add a
  blanket spawn lift; `BL-457` shows authored spawns are otherwise exact, and the roster-spawned
  formation here is one more confirmation of that.
  *Cross-refs:* `BL-522`, `BL-457`, `docs/formats/ai-rosters.md`,
  `docs/formats/mission-entities/enemy-generators.md`.


- `BL-558` `[Research]` **A damaged AI flies a full evasive maneuver where the original may only set a
  flag.** *Evidence:* [`docs/org/aiControlLaw.md`](docs/org/aiControlLaw.md) records `obj+0xBA` as an
  **evade flag**, set to 1 by the damage handler `FUN_004b9bc0` when the steady-hand test fails
  ("Absorbed %f damage; steady hand test failed. Evading."), cleared in `FUN_0041d9f0` once the
  pursuer's nose alignment on this aircraft drops below 0.85, and while set it suppresses the lay-off
  branch and the voice callouts. `AiModeMachine.NotifyDamage` instead picks a maneuver and transitions
  to `EvasiveManeuver`, falling back to an explicitly invented eight-second plain evade with random
  60 to 120 degree heading scrambles when no maneuver is eligible. Observed at runtime: a Fury takes
  its first 40-calibre hit, fails the roll, enters `scissors` immediately and leaves the player's
  6-degree assist cone (`analysis/aim-assist-ttk/FINDINGS.md`). If the decode is complete, CSVM is
  manufacturing a break-off the original does not have, and it costs hit rate on every first hit.
  *What to settle:* whether anything else in the executable reads `+0xBA`, in particular whether the
  mode field is written anywhere on the damage path, before deciding the flag is the whole story.
  *⚠ Traps:* `NotifyDamage`'s own doc comment claims a decoded basis for the maneuver behaviour, so
  two readings of the same path are in the tree and one is stale; reconcile them before touching the
  code. Removing evasive maneuvers on damage is a large behavioural change to make on one line of a
  decode page, and the steady-hand roll itself is not in question, only what a failed roll does.
  *Cross-refs:* `BL-557` (the other open TTK cause), `docs/org/aiControlLaw.md`.

- `BL-564` `[Bug]` **CM12 (C2/M01): the `eshipg31` generator launches Bloodhawks at the world
  origin instead of patrol boats at the pirate ship.** *Evidence:* a flown CM12 session
  (`.scratch/logs/menu-20260827-215135.log`): the wave
  arrives as `ai17_player_bhawk`, `ai18_player_bhawk`, `ai19_player_bhawk`, tracked by the target
  HUD at 7.6 km from the player, and two of the three ram terrain `g34586` within seconds at
  `pos=(5,5,-109)`, the world origin; the third patrols `M2Patrol1`, a water net, and is shot down
  later. Two causes. (1) The generator's `vehicle.params` label `Eshipg31_params` resolves to the
  roster block `patrolboat_eg0` (def 4, `patrolboat`, a surface vehicle), which
  `CampaignRosterPlan.Build` reports in `Skipped` rather than planning, so `GeneratorTemplates`
  has no entry and `GameSession.SpawnFromGenerator` falls back to `SessionSpec.GeneratorsPlane`,
  `player_bhawk`. The mission's boats are the `patrolboat_eg0..5` that OBJECTIVE58-63 and
  OBJECTIVE70 move between `M2GoosePatrol` and `M2PatrolStop`. (2) The host node `eshipg31` is a
  model-less group node with a zero local translation whose geometry sits at its node bbox,
  about (-5892, 10, -4412); `AiGeneratorRuntime.Spawn` drops at `Host.GlobalPosition`, which is
  (0, 0, 0). *Fix shape:* decode the original's launch position for a generator whose host has no
  model (`FUN_00452450`: the node's world matrix, or its bbox centre) and use that; then decide
  what a surface-vehicle launch is in CSVM (a boat on a water net, not an aircraft), or at least
  refuse the fallback airframe for a surface def so a boat generator launches nothing rather than
  fighters. *⚠ Traps:* the three Bloodhawks are group 3 and never count toward "Destroy all
  enemy fighters" (`DEDG [1, 0]`); killing them is not progress. Do not "fix" (1) by handing the
  generator a fighter def. *Cross-refs:* `BL-522` (launch placement from a surface host),
  `BL-527`, `docs/formats/mission-entities/enemy-generators.md`.

- `BL-565` `[Fidelity]` **`DEDG`'s decoded side effect, widening every counted member's engagement
  volume to 9,000 m, is not applied.** *Evidence:* `docs/formats/objectives.md`'s `DEDG` row and
  `FUN_00465850`: each tick an awake `DEDG` objective raises every live member of the watched
  group to a 9,000 m activation radius and a ±9,000 m altitude band, so a watched group never
  disengages by distance and comes to the player from anywhere on the map. CSVM's `DedgMet` only
  counts; the members keep `AiModeMachine.ActivationRange` at the 2,000 m `min_ai_active_dist`
  floor and drop back to patrol at "target lost" / "beyond return range", which is how a
  survivor of a wave sits on its net 8 km away while the objective waits on it. *Fix shape:*
  have `GroupLiveCount` (or a sibling the graph calls per awake DEDG) apply the widening to each
  counted member's machine: `ActivationRange = max(ActivationRange, 9000)`, and the altitude bands
  once they have a consumer. *⚠ Traps:* the widening is per awake objective per tick, so a
  napped or killed `DEDG` stops widening but the original never shrinks the volume back; match
  that (set, never reset). *Cross-refs:* `BL-523` (the patrol/pursue cycle).

- `BL-566` `[Bug]` **CM12 (C2/M01): the ace `hkfirebrand_9` flies under the terrain after its wake,
  and nothing stops it until it rams a tile from below.** *Evidence:* reported at the controls
  (the ace seen under the ground, not crashing, not surfacing) and the session
  `.scratch/logs/menu-20260828-001548.log`: once OBJECTIVE67 wakes it, the
  ace alternates `pursue -> avoid crash (below the 20 m floor)` and back a dozen times, with a few
  `obstacle inside NNN m (tagged/col)` probes, and dies minutes later as `AI ram into tagged/col`
  (no shooter). The floor test is the decoded absolute one, `pos.Y < 20` world metres, not height
  above ground, so over land it says nothing about terrain; the ace's authored spawn (-4518, 150,
  -6233) and the fight sit beside terrain tile `tagged` (x -5120..-4096, z -6144..-5120, rising
  to 215 m), and an aircraft repeatedly under world Y 20 m there is inside the hills. Its wake
  places it at the authored roster position (`WakeupEnemies` calls `rig.Activate(plan.Position,
  …)`), and the original does the same: `FUN_004b0f40`'s activation only re-derives the position
  through `FUN_00432010`, the trailer-offset rule, and net 30 `M2Dummy` has no trailer. So the
  spawn itself is not decoded to be lifted. *What to settle:* (1) where the ace goes under: whether
  the authored 150 m at (-4518, -6233) is already below our terrain there (the adjacent tile
  south of `tagged`; sample it with `--freecam`), or whether it dives through a tile while
  pursuing a low player, which would mean the terrain contact test misses a steep fast crossing;
  (2) why a plane under a tile flies on: terrain colliders are single-sided so a crossing from
  below is silent, and the ram rule only fires on the way back up. *Fix shape:* answer (1) first;
  if the spawn is under ground it is BL-527's question again (a decoded ground rule at spawn, or
  none), and if it is a crossing, the contact test at the crossing is the bug, not the floor.
  *⚠ Traps:* do not replace the absolute 20 m floor with an AGL floor; it is decoded
  (`DAT_0071c3f0`) and the original has no AGL floor either. Do not add a spawn lift. The ace did
  count for "Destroy all enemy fighters" in the end (its ram death dropped group 2 to zero and
  primary 3 completed), so this is not an objective bug. *Cross-refs:* `BL-527` (CM07's Peacemaker
  under the ground), `docs/org/aiPilot.md` (the activation primitive and
  `FUN_00432010`).

- `BL-568` `[Bug]` **CM04 (C3/M03): the Pandora starts moored in the dry dock instead of flying in
  over the mission's first minute.** *Evidence:* at the controls the Pandora is already in the dry
  dock while the cargo zeppelin is still moving out. M03's `NEW_GAME_START` list runs
  `pzep_todrydock` (`extracted/C3/M03/mis_anim/piratezep-pzep_todrydock.json`, `OnCall`, one
  `ObjectMotionSiScript` on `piratezep`), whose SI script
  (`data-c3-m03-zrdr-zeps-pzep_todrydock-piratezep.zan.json`, 185 frames, 0 to 61.65 s) carries the
  airship from `(-11314,554,-13697)` to `(-12401,150,-10355)`. The zeppelin record in
  `zeppelins.zrd` seats `piratezep` at `(-12400.9,150.3,-10355.2)`, yaw -180, on net `M3PirateZep`,
  which is the script's END pose. That reads as: the record seats the airship at its destination
  and the start anim's script owns the pose from its first frame; CSVM spawns the zeppelin at the
  record position and the script never takes its pose (or the net follower writes over it), so it
  stands in the dock from the first frame. *Fix shape:* an `ObjectMotionSiScript` on a zeppelin node
  owns that zeppelin's pose for the script's duration, starting at frame 0's base, with the net
  follower parked and resuming from the script's last frame. Check `ZeppelinRuntime`'s placement
  against the scripted-path snap (`BL-531`'s fix) and the dead-end hold (`BL-529`'s fix) first,
  since both touched placement; neither should apply to a scripted motion. *⚠ Traps:* do not move
  the record's position to the path start; the record's seat is data and the script is what flies
  it. *Cross-refs:* `BL-529`, `docs/org/objectMotion.md`, `docs/formats/anim-definitions.md`.

- `BL-569` `[Bug]` **CM04 (C3/M03)'s opening cutscene does not play: `calldestroy_the_cargozep`'s
  camera never takes the view.** *Evidence:* at the controls the mission opens in the cockpit with
  no cutscene. `NEW_GAME_START` runs `calldestroy_the_cargozep`, which calls
  `destroy_the_cargozep` (the cargo zeppelin's scripted destruction, with `snd_IntrosceneHAch4`)
  and `cgzep_camera` (`player-cgzep_camera.json`, `OnCall`, objects `player`, `cockpit1`,
  `camera1`: a cutscene camera over the player's aircraft). The sortie log has no cutscene line for
  this mission, and `one-shot SOUND 'snd_IntrosceneHAch4' positioned by out-of-tree ancestor
  composition ... (world root not parented at bootstrap)` says the chain fired during bootstrap,
  before the world was in the tree, rather than as the mission's opening scene. `generic_intro` is
  not in M03's start list, so the cutscene path `BL-548` and the trigger latch (`BL-583`) exercise
  is never entered here. *Fix shape:* settle how the original runs a `camera1`-object cutscene
  called from a start anim (the registration sites `docs/formats/anim-definitions/cutscenes.md`
  lists) and route `cgzep_camera` through the cutscene runner with the world held, so the player
  watches the cargo zeppelin go down and the skip works. *⚠ Traps:* the destruction itself already
  runs (fireballs, `tntbox`/`gasbag` deactivation); do not run it a second time under the camera.
  *Cross-refs:* `BL-548`, `BL-583`, `docs/formats/anim-definitions/cutscenes.md`.

- `BL-571` `[Bug]` **A carried turret's death fire, and the turret itself, stay in the air where
  the turret died while the zeppelin moves on.** *Evidence (traced for the fire, lead-only for the
  turret):* reported at the controls in CM06 (C1C/M01) after a Workers' Voyage gun ring died: a
  persistent flame hangs at the death position, and the turret is seen stuck there too. The fire
  is `large_30sec_fire`, which the ring's destroy def (`doublecannon4-*doublecannon4-healthy.json`)
  calls with `WithNode doublecannon4 (0, 2, 0)`. `AnimRuntime`'s `CallAnimation` case hands a
  death's effect call to the world-effects runtime through `ExternalEffect` with a world POSITION
  snapshot (`VisualOriginOf(callAnchor) + basis * offset`), and `PlayEffectAt` stages the template
  root at that point with `TopLevel = true`; the site node rides along only as the callee's
  `INPUT_NODE` for its condition gate, not as its parent. So the flame is world-anchored at the
  death position for its authored 30 s while the hull flies away (the log's `WAIT_FOR_COMPLETION
  on 'large_30sec_fire' not held — the callee is routed to the world-effects runtime` is that
  hand-off). The earlier disproof (`BL-514`, closed as "the fire rides the hull") examined the
  `PUFFER_STATE` path on the world runtime, which does re-read the host each frame; the
  death-call path is this one. Whether the turret model itself is held back the same way (a
  destroyed-role template placed through the same `PlayEffectAt`) or by something else is not
  traced. *Fix shape:* an effect called `WithNode` on a node that moves (a carried site, any
  vehicle sub-part) must follow that node: parent the staged template root under the site node,
  or feed `EmitterDirector` the site's live transform each tick, keeping the `TopLevel` placement
  for world-fixed sites. Then read how the ring's destroyed pose is placed and give it the same
  rule. *⚠ Traps:* `trail-world-anchor` settled the opposite case (an emitter that must NOT ride
  its host); keep both. Do not reopen `BL-514`'s `PUFFER_STATE` reading, it is correct for that
  path. *Cross-refs:* `BL-514`'s closing commit (`git log --grep=BL-514`),
  `docs/formats/anim-definitions.md` (`CALL_ANIMATION` `WithNode`), `docs/formats/effects.md`,
  `docs/org/puffer.md`.

- `BL-572` `[Fidelity]` **A campaign objective's marker labels the raw node name in the team
  colour (`peoplehook`, `pzhookpoint`, `workersvoyagezep`) instead of the original's objective
  marker.** *Evidence (lead-only):* reported at the controls in CM06 (C1C/M01): objective markers
  read as green node names. The log's `targeting hud: P1 brackets on peoplehook at 999 m` /
  `pzhookpoint` / `workersvoyagezep` lines show `TargetRef.DisplayName` carrying the node name for
  an objective target, `TargetHud.LabelLines` drawing it as the second label line, and the colour
  rule (`HudGreen` for a non-hostile team) applying as to any friendly vehicle. The original's
  marker, from the user's recollection of this mission: a red `Zeppelin [Disable] Worker's
  Voyage` for the first objective, then a blue `[Dock] Worker's Voyage Docking Hook` for the hook,
  so a category, an action verb in brackets, the target's proper name, and a colour by the
  action rather than the team. The strings exist in `extracted/messages.json`: `MSG_OBJ_DOCK`
  (8003, "Dock"), `MSG_OBJ_DISABLE` (8006, "Disable"), `MSG_OBJ_DISABLEENG` ("Disable Engines"),
  `MSG_OBJ_WVOYAGE` (8025, "Worker's Voyage"), `MSG_OBJ_WVOYAGEHOOK` (8027, "Worker's Voyage
  Docking Hook"), `MSG_OBJ_KLONDIKEHOOK` (8017, "Pandora Docking Hook"). `objectives.zrd` only
  names the node (`ADD_OBJECTIVE_TARGET [workersvoyagezep]`, `[pzhookpoint]`,
  `[[wv_tailhook, peoplehook]]`, `docs/formats/objectives.md` 173), so the node-to-name and the
  node-to-verb maps live elsewhere (a vehicle or zeppelin record field, or a table `crimson.exe`
  indexes by node name) and are the decode. *Fix shape:* decode where the objective marker's verb
  and proper name come from for a target node and what sets the marker colour, give `TargetRef`
  an objective display line built from those message ids, and keep the node name for the debug
  tag only. *⚠ Traps:* `BL-397` is the marker's bracket
  range rule and not this. *Cross-refs:* `BL-397`, `docs/formats/markers.md`, `docs/org/targeting.md`.

- `BL-573` `[Bug]` **CM07 (C1/M02): the AA guns damage themselves, one blowing itself up while
  firing at the barrier in front of it.** *Evidence (lead-only):* reported at the controls: an AA
  gun exploded on its own while shooting at a barrier. The sortie log shows `aagun32` taking four
  hits in a row with no player round near it (`damage: -10 on aagun32 HP 30→20 [stage 0]`,
  `-9.58`, `-9.2`, `-10 ... DESTROYED — death sequence run`), then `aagun33`, `aagun34` and
  `aagun36` taking the same `-10`, `-9.58` pair; the identical decrements across four guns read as
  one weapon's rounds, the guns' own, bursting on the obstruction and splashing the shooter. Not
  traced: whether the flak's burst damage excludes its own shooter in `crimson.exe`, and whether
  the original's gun fires at all when a structure blocks its line. *Fix shape:* trace which
  shooter id lands those hits (`--debug` hit logging on the turret pool), then decode the flak
  burst's damage application for a self-exclusion and the turret fire gate for a line-of-fire
  test; apply what the decode says. *⚠ Traps:* do not exclude turrets from splash wholesale, a
  rocket into a gun pit must still kill it. *Cross-refs:* `BL-516` (turret waking), `BL-514`,
  `docs/org/weaponImpact.md`.

- `BL-574` `[Bug]` **CM07 (C1/M02): the hangar hand-over gives the player a stock Bloodhawk in the
  ordinary player paint and without nitro, where the original hands over the Blue Streak in Blake
  Aviation livery.** *Evidence (traced to the swap, lead-only on the livery):* at the controls the
  aircraft flown out of the hangar has the Fortune Hunters livery and no nitro, and the profile's
  Blue Streak has nitro only after the mission (`BL-528`'s award half works). The log shows the
  swap: `EXECUTION_BY_RANGE reached - starting hangar_drop at 67 m`, then `airframe swap: P1 is
  now flying 'player_bhawk'`. `AirframeSwapCodes` maps code 965 to `pbloodhawk`/`player_bhawk`
  and `FlightRoster.SwapPlayerAirframe` assembles that node through the ordinary player build
  with the shared paint stream, so it is the stock airframe. `BL-528`'s in-mission half was closed
  on the `aiv.zrd` `player` block authoring no aircraft, which is true, but the mission's aircraft
  hand-over is this swap, not the roster, and it was not examined. The Blue Streak template
  (`docs/org/hangar.md`, `0x0061a9b8`) carries engine 4 (nitrous), twin 40 and twin 30 guns and
  1/1 hardpoints; its paint is the shared custom scheme, so where the Blake Aviation livery the
  user remembers comes from (the swap code's own skin set, `blake*` in the faction table, or the
  cutscene's captured rig) is the decode. *Fix shape:* decode what swap code 965 builds in the
  original (the template at `0x0061a9b8` or a stock def) and which skin it draws; then have the
  hand-over assemble the Blue Streak build (`CustomPlaneBuild` from `CampaignProgression.AwardBuild`'s
  template, `Nitro.Installed` true) in that livery. *⚠ Traps:* do not give the stock Bloodhawk
  nitro, and do not touch the post-mission grant, which is correct. *Cross-refs:* `BL-528`,
  `docs/org/hangar.md`, `docs/formats/anim-definitions/cutscenes.md` (the swap codes),
  `docs/formats/paint.md`.

- `BL-575` `[Bug]` **CM07 (C1/M02): the hangar cutscene plays with none of its animations.**
  *Evidence (lead-only):* reported at the controls: during the hangar hand-over cutscene nothing
  animates. The log shows `'hangar_drop' armed by call at 1167 m, waiting for EXECUTION_BY_RANGE
  (75 m)`, `EXECUTION_BY_RANGE reached - starting hangar_drop at 67 m (range 75 m)`, the airframe
  swap, and `4 call(s) retargeted onto a named node`, but no line for the hangar's own motion
  (doors, lift, the aircraft's drop) and no cutscene hold. Not traced: which defs `hangar_drop`
  calls, whether they are among the `431 reader def(s) superseded by this mission's compiled
  manifest` (the same drop `BL-521`'s `balloon_downa*` shows) or run on nodes the cutscene
  reparents. *Fix shape:* read `hangar_drop`'s call list from `extracted/C1/M02/mis_anim`, run
  the mission headless with `--debug-anim` to the hangar, and trace the first callee that does not
  start. *⚠ Traps:* the swap itself works and must stay; the missing part is the choreography
  around it. *Cross-refs:* `BL-574`, `BL-521`, `docs/formats/anim-definitions/cutscenes.md`.

- `BL-576` `[Bug]` **CM08 (C1B/M03): the Pandora pitches steeply up and down along the Klondike
  net, following every altitude step of the route at full pitch.** *Evidence (seen at the
  controls, mechanism lead-only):* the screenshot
  `Screenshots/crimsonskies_2026-08-27_23-47-46-050.png` (plan worktree) shows `piratezep` nose
  down about 30 degrees, diving along the green `Klondike1` segment toward a lower node with the
  ai-nets overlay on. `Klondike1` is a 13-node open chain whose nodes swing between about 400 m
  and 93 m, and the record's pitch band is -30 to 30 degrees at `max_rate_pitch` 5. The dead-end
  shuttle `BL-529` fixed was one cause of the porpoising report and the hold at the far end is
  confirmed; the up-and-down along the route itself is still there and was the report's first
  half. *Fix shape:* read `FUN_004bf9d0`'s pitch term against `ZeppelinMotion`: whether the
  original steers pitch at the node's altitude difference directly, clamps it under a smaller
  authored limit for route following, or eases altitude over the edge length, and whether the
  net's node altitudes are the airship's targets at all (a zeppelin net may carry its own altitude
  field). Then match. *⚠ Traps:* the initial-pitch clamp that never fires (`ZeppelinMotion.cs`)
  is decoded verbatim and stays; do not "fix" it as part of this. Do not flatten the net.
  *Cross-refs:* `BL-529`'s closing commit (`git log --grep=BL-529`), `docs/org/flightModel.md`,
  `docs/formats/mission-entities.md` "Route ends and stop points".

- `BL-577` `[Bug]` **CM08 (C1B/M03): the patrol boats never spawn.** *Evidence (traced):*
  reported at the controls: no boats. `aiv.zrd` carries four enabled roster blocks
  `patrolboat_1..4` (defs 37 to 39, positions at `y = 0` such as `(-7614.8, 0, -5556.5)`,
  `-90` yaw, nets `Patrolboat1..4`), and `objectives.zrd` wakes them by `WAKEUP_ENEMIES
  [patrolboat_1, patrolboat_2, patrolboat_3, patrolboat_4]` (line 164) and moves them between
  nets (lines 241 to 253). `CampaignRosterPlan.Build` reports a surface-vehicle block in
  `Skipped` (`'{def}' ({mode}) has no player airframe`, `CampaignRoster.cs`) and never spawns it,
  so the wake finds nothing; CSVM has no runtime for a roster surface vehicle at all. The same gap
  is what makes C2/M01's boat generator launch fighters (`BL-564`). *Fix shape:* a surface
  vehicle runtime for roster blocks: spawn the def on its net at water height, drive it along the
  net with the scripted-path follower's law (`docs/org/flightModel.md`), and give it the turret
  and destructible wiring the `patrolboat-*` mis_anim defs (`ptboat_50damage`, `ptboat_75damage`,
  `emit_ptsplash*`) expect; then the generator case in `BL-564` is the same runtime launched.
  *⚠ Traps:* do not spawn a boat as an aircraft with a low ceiling. *Cross-refs:* `BL-564`,
  `BL-531` (the scripted-path follower), `docs/formats/ai-rosters.md`.

- `BL-578` `[Bug]` **CM08 (C1B/M03): the tanker jumps and sits at the wrong position.**
  *Evidence (lead-only):* reported at the controls: the tanker makes a jump and is not where it
  should be. Its motion is authored as `ObjectMotion`: `freighter-freightercruise.json` drives
  `freighter` (with its wakes, hold and hold doors), and `freighter-freighterwavemotion.json`
  loops two `ObjectMotion` events on `tanker`, both in M03's `NEW_GAME_START` list, and the
  mission's anim census reports `ObjectMotion×1` not yet acted on. Two leads: the `rnd_xz` start
  velocity drift that `BL-512` traced on the Barracuda (a normalized direction read as a random
  amplitude, snapped away at the next absolute placement) applies to every `ObjectMotion`, so a
  jump on the tanker is the same defect; and the tanker is `Russian`'s net trailer
  (`ai nets: #28 'Russian' nodes=9 edges=8 trailer=tanker@node8`), so a net or scripted-path
  placement (`BL-531`'s waypoint-0 snap) may be writing its pose against the motion. Whether the
  Pandora and the tanker play the cargo-crane choreography is untested and belongs to this item's
  check. *Fix shape:* `--anim-lab --node=tanker` on C1B, play `freightercruise`/`freighterwavemotion`
  and log the pose at each event boundary; if the discontinuity is `rnd_xz`, fold it into `BL-512`'s
  `ObjectMotion` fix; if a follower writes the pose, exclude a node an `ObjectMotion` owns.
  *⚠ Traps:* do not special-case the tanker; the `ObjectMotion` semantics are shared with the
  Barracuda and the airships. *Cross-refs:* `BL-512`, `BL-531`, `BL-568` (the Pandora's own
  scripted motion), `docs/org/objectMotion.md`.

- `BL-579` `[Bug]` **CM09 (C1/M04): the intro cutscene does not play correctly.** *Evidence
  (lead-only):* reported at the controls. The mission's `NEW_GAME_START` list runs
  `mission_intro_animation` (`camera1-mission_intro_animation.json`, `OnCall`, root `camera1`,
  objects `player`, `piratezep`, `cockpit1`, `interior`, the hangar `front_door_*` and five
  `bullet*` nodes: a hangar-interior camera scene with the Pandora), and the log shows it in the
  start list (`start anims [pzep_engines_start, train_on_track, mission_intro_animation],
  undefined here: [pure_panic]`) but no cutscene hold or handoff line for it, and the scene's
  one-shot `snd_scene1` is `positioned by out-of-tree ancestor composition at (0, 0, 0) (world
  root not parented at bootstrap)`, so at least its sound fires during bootstrap rather than in
  the scene. `pure_panic` is a C1/M02 hangar def the list names and this mission does not
  compile; whether the original plays it here is part of the question. What the original shows,
  from the user's recollection: the standard generic intro first, then the player's aircraft and
  the wingmen flying down out of the sky to the mission's start point, which is what
  `mission_intro_animation`'s `player` object motion over the `bullet*` path nodes authors; CSVM
  opens at the start point with neither. *Fix shape:* run `--campaign=<CM09>` headless with `--debug-anim` and read
  `mission_intro_animation`'s event log against the def; `BL-569` (C3/M03's opening scene fired
  at bootstrap with no camera) is the same class and may be the same fix. *⚠ Traps:* `BL-548`'s
  deferred `--pos=` handoff is for `generic_intro`; this mission's intro is its own def.
  *Cross-refs:* `BL-569`, `BL-548`, `docs/formats/anim-definitions/cutscenes.md`.

- `BL-581` `[Bug]` **CM09 (C1/M04): with the radio tower down and every aircraft killed, the
  Pandora's Defend marker clears and the mission does not go on to the docking.** *Evidence
  (chain decoded, stall cause open):* reported at the controls. The "Defend rock_zeppelin"
  marker is authored: `OBJECTIVE23` (live from the start) completes when one of the Promised
  Land's `destroy_hkzep_*broad*` anims goes `INVALID`, adds `[piratezep, rock_zeppelin]` as the
  objective target with help label `MSG_OBJ_DEFEND` ("Defend"), naps `OBJECTIVE24` (25 s, wakes
  `blakebloodhawk_9..13`), and `OBJECTIVE25` (`DEDG [2, 0]`) removes that target once group 2 is
  dead; the node name in the label is `BL-572`. The radio tower is `OBJECTIVE15`/`33`
  (`INACTIVE1 rtwr_healthy`); destroyed in time it kills `OBJECTIVE16` and routes through 17 to
  19 (90 s) to `OBJECTIVE20`, otherwise 16 routes through 18 (30 s) to the same 20, which wakes
  `blakebloodhawk_1/2/3/8`: the squad arrives either way, only later with the tower down. The
  docking (`OBJECTIVE31`, primary 2, `pzhookpoint`) is reached only through `OBJECTIVE30`
  (primary 1, `INACTIVE1 [lkgasbag05, panelleft1]`, the Promised Land destroyed) waking
  `OBJECTIVE42` (`DEDG [1, 0]`, ticking on 40), then 43 (`DEDG [2, 0]`), then 44 (`DEDG [5, 0]`).
  The Promised Land was destroyed in both flights, so primary 1 is met and the stall is a `DEDG`
  group never reading empty, and the flight that stalled is the one where the Paladin Blake
  squad (`blakebloodhawk_1/2/3/8`, group 1, woken only by `OBJECTIVE20`) never arrived: with
  four deactivated members, `DEDG [1, 0]` cannot complete (`BL-563` makes a deactivated member
  count as alive, and even without that the four are alive-but-parked). The squad's two routes
  differ only in timing: with the tower still up, `OBJECTIVE16` (15 s after 14) kills 15 and 17,
  wakes 18, and 18 naps 20 for 30 s; with the tower down inside that window, 15 kills 16, naps
  17 (16 s), 17 naps 19 (3 s), and 19 naps 20 for 90 s. Both 18 and 19 carry
  `TICK_DEPENDS_ON_OBJ 29`, and `ObjectiveGraph.Ticks` holds such an objective entirely while
  29 is not `Awake`; 29 is dormant until 28 (`DEDG [2, 2]`, woken by 24) wakes it. So the
  90 s leg through 19 is where to look: whether a nap timer set on 19 while 29 was still
  dormant is dropped rather than resumed, or whether 19's own nap of 20 never starts because
  19 is gated when 17 wakes it. Objective transitions are not in the file log at all (no
  `[campaign]` objective line exists in the sink), which is why this cannot be settled from the
  sortie. *Fix shape:* first route the graph's wake, nap, complete and kill transitions through
  `Log.Info("campaign", ...)` so a sortie log carries them; then reproduce headless: complete 15
  inside 16's window, then 23/24/28 (a Promised Land hatch down, group 2 to two), and assert 20
  wakes 90 s after 19; fix `Ticks`' interaction with a nap that lands on a gated objective per
  the decode (`docs/formats/objectives.md`'s `TICK_DEPENDS_ON_OBJ` row). `BL-563` stays a
  separate fix. *⚠ Traps:* the Defend marker clearing is correct behaviour (`OBJECTIVE25`), not
  the bug.
  *Cross-refs:* `BL-563`, `BL-565`, `BL-572`, `docs/formats/objectives.md`.

## Tooling, platform & docs

- `BL-033` `[Cleanup]` `[Blocked: SDL >= 3.4.4]` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

- `BL-584` `[Bug]` **`PerfSampleTests.AScopeAllocatesNothing` is not same-build stable inside the
  parallel unit stage.** *Evidence (seen once, mechanism lead-only):* the full `RunTests.ps1` unit
  stage reported it red once (`Expected: 0, Actual: 3984` bytes) on a tree whose only difference
  from six green runs was PowerShell and documentation edits; it then passed three times alone and
  on every later complete run. An allocation assertion measured with `GC.GetAllocatedBytesForCurrentThread`
  or similar shares a process with fourteen concurrent test classes, so another class's work on
  the same thread pool thread, or a tiered-JIT recompile landing mid-scope, can charge bytes to
  it. *Fix shape:* pin the measurement to the current thread and warm the scope once before the
  asserted call, or move the test to a non-parallel collection and say why. *⚠ Traps:* do not
  widen the assertion to a tolerance; zero allocations is the contract `PerfSample` makes, and a
  tolerance would hide a real regression. *Cross-refs:* `docs/verification.md` PERF rules,
  `docs/plans/PLAN-fast-verification.md` C23.

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
