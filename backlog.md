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
  scales the yaw `eff`, so a change in one moves others; the `flight-envelope` suite asserts six
  measured scenarios and will fail if a change breaks one. `ThrustConst` and
  `PitchTune`/`YawTune`/`RollTune` are pinned measurements, not knobs — a fix that needs one of
  them to move needs a new measurement first. ⚠ **They re-pin when a decoded mechanism changes the
  steady rate they hold, and only then**: `YawTune` 1.32 → 1.33 with the authored yaw curve (C21),
  `PitchTune` 0.75 → 0.89 and `YawTune` 1.33 → 1.57 with the weathervane torque (C23), each against
  the same stopwatch/video figures as before. Chasing a *transient* or a feel report through them is
  the forbidden move, not re-pinning a steady rate something else moved.

## Damage & destruction

- `BL-346` `[Tuning]` `[Owed-playtest]` **Re-judge the debris arc at the controls now that
    `OBJECT_MOTION` is a decode, not a tuned look.** `BL-022`'s retired `DebrisTune.LaunchScale =
    0.65` was a footage fit laid over the wrong spherical `translation_range` reading; the constant
    and the whole class are deleted (`PLAN-object-motion-decode` D10), and the launch direction,
    speed and `RUN_TIME` ceiling the executable actually authors now drive the arc unscaled. `PT-46`
    (d)'s observation that some original pieces pass through terrain or vanish stands — only its
    mechanism attribution was wrong, and `PT-46` is re-opened against the corrected one (the default
    ground-column tier, `NO_ALTITUDE`'s opt-out, `DO_INTERSECTIONS`'s sweep upgrade).
    *To settle:* fly the C1 `m_build03` destruction and compare the arc and landings against
    `OriginalScreenshots/Videos/m_build03 destruction.mp4`.
    ⚠ **Traps.** Do not reintroduce a compensating scalar to chase the old look — a mismatch is a
    further decode or a newly filed item, per this plan's milestone boundary. Distinguish an
    unflagged pass-through (faithful — `do_intersections: false` bodies keep sinking through terrain
    the original also sinks them through) from a flagged body that should now land.

- `BL-348` `[Bug]` **C3's balloon-battery kill chain (`bontN`/`tbaseN`/`b_turretN`, M02) doesn't
    match the authored data on any of its four death paths — over-triggers, drags the wrong node,
    and drops calls silently.** Each of the six balloons is three independently-`WeaponHit`
    destructibles — `tbaseN` (ground tether anchor), `bontN`/`ball_kaboomN` (the balloon body) and
    `b_turretN` (its slung AI turret) — wired by three authored, exact-numbered `CallAnimation`s:
    killing `tbaseN` calls `balloon_upN` (24s tether burn → 512m rise → `ball_kaboomN`); killing
    `b_turretN` waits 2s then also calls `ball_kaboomN`; `ball_kaboomN` itself calls
    `balloont_dieN` back. Every one of these names its target with an exact digit (`balloon_up1`,
    never a wildcard), and all three defs ship `local_nodes_only: false`.
    User-observed at the controls (2026-08-13), against `--chapter=C3 --mission=M02`:
    (a) killing ONE `tbaseN` raises all six balloons, not just the matching one;
    (b) the killed `tbaseN`'s own body drags upward with its balloon (visible tether
    stretching/ripping) instead of staying grounded — its own death sequence never moves
    `tbaseN`, only its three debris chunks;
    (c) the five wrongly-triggered balloons do play their own `balloon_upN` rise, but never
    detonate at the top — no `ball_kaboomN`, they just disappear instead of exploding;
    (d) killing `b_turretN` swaps the balloon straight to its `b_destroyed`/`f_destroyed` skin
    but leaves it hanging motionless in the air — `ball_kaboomN`'s debris/fireball/sound/
    `balloon_downaN` fall never runs.
    *To settle:* trace `CallAnimation` dispatch and `NameResolver`'s three-tier scope chain
    (`docs/architecture.md`, `Anim/NameResolver.cs`) for a cross-instance name collision — all
    three defs reuse generic node names (`healthy`/`destroyed`/`part1..3`/`dbase`) across all six
    numbered instances, and the tier-3 global fallback (`local_nodes_only: false`) is a candidate
    for (a): an over-trigger, five defs launching that a single, exact-numbered `CallAnimation`
    never named. (c) is the opposite shape — the wrongly-launched balloons *do* run their own
    correctly-numbered `balloon_upN`, but its own trailing `CallAnimation(ball_kaboomN)` never
    fires, which looks more like a dropped/truncated sequence-completion than a name leak; trace
    it separately rather than assuming (a)'s cause explains it. (b) needs the `tbaseN`↔`bontN`
    transform/parenting checked too, since nothing in either def's `ObjectMotion*` targets the
    other's node.
    ⚠ **Traps.** (i) A prior report of "shooting the tether instantly explodes the balloon
    instead of raising it" turned out not to be a bug: `tether1` has no separate destructible —
    it's a plain node inside `bontN`'s own def, so a hit there resolves to `bontN`'s own
    independent `ball_kaboomN` pool (health 30, immediate detonation by design) rather than to
    `tbaseN`. Confirmed at the F5/`--debug-damage` damage lab: killing the tether node's pool
    resolves to the parent (`bontN`), not a phantom tether entity — don't re-file this. (ii) Do
    not assume one fix covers all four symptoms — (a) is an over-trigger, (c) is a dropped
    trailing call on an otherwise-correct trigger, (d) looks like a stalled/dropped sequence on
    yet another trigger path, and (b) is unproven to share any of their causes. Verify each
    independently before closing.

- `BL-343` `[Feature]` **A shot-down plane's wreck should leave along the plane's own velocity, and
    ours drops vertically.** `IMPACT_FORCE` is velocity inheritance, it fires in the original, and this
    engine has neither half of it. The gate is fully decoded (2026-08-13); what is left is building it.
    *Evidence:* [`docs/org/objectMotion.md`](docs/org/objectMotion.md), section "`IMPACT_FORCE` (bit
    `0x2`), and the callback that feeds it". The update's first-tick init requires three things
    (`FUN_004e8fa0`): the bit (`004e925e`), a velocity parked on the **anim instance** at `+0xc0..0xc8`
    with `+0x9c` bit `0x80` set (`004e9268`), and the moving node having **exactly one parent**
    (`004e9275`, the parent count `node+0x54` that `FUN_004cef20` reads). It then transforms that
    world velocity into the body's parent frame through the transposed parent matrix and adds it to the
    **live velocity** `+0x58..0x60`, once. The velocity gets there only when the animation runs a
    **`CALLBACK 16`** event (kind 35, `FUN_004ec5e0`) whose handler reads the owning object's velocity
    into the slot (`FUN_004ee0e0`, ignoring anything under 0.01). Census over 8 chapters: **182 events /
    25 shapes / 15 defs**, of which **120 events / 15 shapes fire** (the eleven airframes'
    `MAIN_ROOT_NODE`, `player`'s four pieces, all of which author `CALLBACK 16` in `destroy_craft`
    before calling the motion's sequence) and **62 are inert** for lack of any `CALLBACK` event
    (`player_crash_default`, `agyrobus`, `drop_smokescreen_canister`). ⚠ On all eleven airframes the
    authored `translation.initial` and `delta` are **(0,0,0)**, so the inherited velocity is the wreck's
    *only* horizontal motion. `CSVM/src/Mech3/` handles no `CALLBACK` event and reads `impact_force`
    nowhere.
    *Fix shape:* three pieces, in order. (1) A per-anim-instance velocity slot plus an "is set" flag,
    cleared at anim start, set only when a component exceeds 0.01; (2) a `Callback` event handler that on
    value 16 reads the owning object's world velocity into it (15 is the airframe-hide notification, 0 is
    the anim-stop notification, 3 is `player`'s own unrelated code); (3) in the motion's first-tick init,
    when the bit is set and the flag is set and the node has exactly one parent, add
    `parentWorldBasis⁻¹ · v` to the live velocity.
    ⚠ **Traps.** (a) **`BL-008` is closed (`1f09c2d`) on "the original does not inherit velocity into world
    debris", and that closure is still right for world debris.** Not one world destructible authors this
    flag; every carrier is aircraft wreckage, which is the population the closure never looked at. Do not
    reopen `BL-008`; this is the part of the question it did not answer. (b) **The flag alone is not the
    trigger.** Building the add without the `CALLBACK` half gives every carrier inheritance, including the
    62 events the original leaves inert. (c) **The feared conflict with the `pNhit` rule is resolved, not
    open.** `player_crash_dirt` authors `impact_force: false` on all eight of its motions, so the judged "a motion
    continuing a contact landing inherits none of the aircraft's momentum"
    (`docs/formats/destructibles.md`) is a different def and does not collide. `player_crash_default`'s
    `p1hit`/`p2hit` do carry the flag and are inert. (d) `BL-122`'s crash-debris look was signed off on
    *direction* only (`CAP-16`), never magnitude, so it is not evidence either way here. (e) A replicated
    anim start can carry a velocity directly (`FUN_004eddd0`, network path only); irrelevant single
    player, remember it if network parity ever matters.
    *Why now:* the Game AI milestone puts shot-down aircraft on screen in quantity, which is exactly the
    population this fires on.

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
  here starts from the authored arc, with no compensating scalar; `BL-346` / `PT-46` re-judges the
  resulting look at the controls. What stays TUNE is the `fly_trailN` anchor being invisible so that
  only the trail shows; and the DISTANCE interval hides behind an inverted flag
  (`has_interval_value` false, key off `interval_type`).

- `BL-121` `[Tuning]` `[Owed-playtest]` **Damage (Run-2 item 10)** — `CrashSpeed` 25, graze friction + attitude kick,
  `GrazeStopSpeed`, breakup scatter, and whether the 10c panel-flip and smoke-trail look right in
  real flight. Rendering at real spawns is verified (the `TopLevel` anchor fix, `docs/HISTORY.md`
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
  any more; the heavy stage plays `player_damage_trail` at `prop1`. *When* is settled (`BL-246`,
  decoded 2026-08-15): the whole-vehicle health fraction at 10%, which a graze that leaves the hull
  healthy never reaches — so this clip showing no whole-plane trail is expected, not a puzzle.

- `BL-122` `[Tuning]` `[Owed-playtest]` **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)** — several playtest-gated TUNEs,
  all needing the original at the controls: `WreckMomentum` **0.4** (`FlightController.cs` — the
  fraction of impact velocity the wreck pieces inherit, so they scatter along travel vs. pop straight
  up); the **debris-arc trajectory** (the executable decode is settled — `translation_range` gives
  `dirY = elevation/90` and horizontal `1 − |elevation|/90`, `initial` the launch speed, `delta` an
  acceleration, `PLAN-object-motion-decode`, 2026-08-13; the former `DebrisTune.LaunchScale` footage
  fit is deleted with no replacement scalar, and only the arc's judged *look* stays open, under
  `BL-346` / `PT-46`; the `fly_trailN` anchor being invisible means only the trail's rough scale
  reads); the **overall crash intensity** (the fireball, the cluster, the debris
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
  - **`WreckMomentum` — direction confirmed, magnitude not pinned.** The same panel travels on a
    straight shallow down-and-forward path along the flight direction (t = 6.13, 6.27); it does not
    pop upward. At t = 12.50 the burning chunks lie **scattered laterally on the ground**, at rest,
    either side of the impact — no lofted arcs. Consistent with a substantial travel-velocity
    inheritance, i.e. 0.4 is the right *shape*; the footage cannot pin the fraction without a known
    impact speed and piece velocity, so 0.4 stays a TUNE.
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
    - **2026-08-09, weighting this correctly:** the 9.7 m figure is a *video frame measurement*, and
      those are weak evidence in this project — they have misled it repeatedly (standing author
      instruction). Note that the sim disagreed with it at the old `×1` default too (16.6 m vs
      9.7 m), so **no value of `SizeScaleDefault` ever reconciled the two** — the footage was never
      evidence about this constant. Treat the gap as a note about sprite alpha or particle density,
      not as an open question hanging over the decode, and do **not** re-open A1 on the strength of
      it. Qualitative reads from the clip (is there a fireball, does it persist) remain useful; a
      measured span from it does not.
    - Limit: the ruler only exists near ignition (the airframe is gone within ~0.5 s and no known
      length survives in frame), so this is an *early-frame* comparison. Our burst is dead by ~1.0 s
      while the original is still at full intensity 9 sim-s later — that gap is the hold time above,
      a separate matter from size.
  *Residual.* The player's **own** crash can never show the burn-out — the game cuts to menu — so do
  not re-film one hoping for it. The one untried vantage is an **enemy** plane crashing while the
  player stays alive, which would keep the scene up; worth a capture only if the hold time above
  turns out to be the binding constraint when tuning. Otherwise what remains is the **A/B against
  our build at the controls**, with the reference numbers above to judge against.

- `BL-246` `[Research]` **ANSWERED 2026-08-15 — when the exe fires the heavy smoke/fire trail.**
  *Answer:* `FUN_004b3800`, the def-level `injure_anims` driver, divides `[inst+0x2d0] / [inst+0x2cc]`
  — **whole-vehicle health current over health max**, armour excluded — and starts the entry's anim
  when that fraction falls to or below the threshold, stopping it again when the fraction rises back
  above (reversible, not a latch). So `player_smoketrail`'s 0.10 entry means **"the hull is at 10%"**,
  not "some zone is at 10%". Nothing is rare or special-cased about it; the write-up with the entry
  layout is `docs/org/vehicleDamage.md` ("Damage staging"). The original question — *when*, not
  where — is closed, and this entry is a close candidate.
  *The premise it was filed under is void.* It was minted 2026-08-03 reasoning that the only damage
  source was a terrain graze (`GrazeMaxDamage` 18 behind `_damageCooldown`, `FlightController.cs`)
  against 15–25 HP parts, with a critical part reaching 0 killing the plane outright, so the 0.10
  window took several survivable grazes on the same part. Both halves have since gone: the player
  can be shot by other players, enemy AI and turrets, and the decoded whole-vehicle health pool
  (`FUN_004b9bc0`, `PlaneDamage.IsDestroyed`) lets parts reach 0% without death. The hull sitting at
  10% is now an ordinary late-fight state, so **the tier needs no loosening** — and never did.
  *What actually remains is a fix, not a question:* our implementation drives the def-level list off
  a per-part combined armour+health fraction and latches it. Three deltas, all in `BL-384`.
  *Retained findings.* `CAP-15` (analysed 2026-08-05, `playtest/CAP-15/`): in the original ONE
  survivable graze puts on the panel-level show — per-panel `short_firetrail` fire for ~12 wall-s
  then sputtering black smoke past 31 wall-s, plus the charred-wing skin swap — with no
  nose-anchored whole-plane trail across the clip. That is consistent with the decode rather than
  evidence against it: a graze that leaves the hull healthy crosses per-part stages only, so the
  clip simply never reached hull-at-10%. **The anchor half is landed (`BL-259`, 2026-08-05):** the
  build plays `player_damage_trail` (`short_firetrail` at `prop1` + the `fire_lt` light) at the
  ≤ 0.10 tier. The corpus check at that landing corrected an earlier claim: the data's own ≤ 0.10
  entries (`player_smoketrail` / `player_firetrail`) DO call `dense_firetrail` at `prop1` — CAP-15
  favours the `short_firetrail` shape and the mapping is one pinned string in
  `DamageVisuals.RigAnimFor` if ever revisited.

- `BL-291` `[Feature]` **A way to spawn/damage a zeppelin — the thin harness that finishes `BL-239`'s in-game
  verification** (PT-36, 2026-08-06). Splash damage reads right at the controls, but nothing in
  C1 shows damage registering on the zeppelin, so `BL-239`'s one unverified picture — a gasbag
  taking blast damage from a hit well off its centre — still has nowhere to be seen. Wanted: a
  `--damage-test`-style spawn of a damageable `hk_zep`, or a debug damage readout on the existing
  C1 one — just enough to watch blast numbers score to the gasbag. Acceptance test: a TORPEDO
  (`wep_14`) into one END of a gasbag, away from dead centre, damages it (the
  nearest-collision-shape falloff, landed 2026-08-05). Reworded 2026-08-14 from "a rocket": the
  decoded `DAMAGES_ZEPPELIN` gate (M4 F18) admits only `wep_14` (the aerial torpedo, the designed
  zeppelin killer) and `wep_28` (the broadside cannonball) — rockets bounce off gasbags by
  design. No stock loadout carries `wep_14` (checked 2026-08-14), but no loadout knob is needed:
  `--weapon-lab=wep_14 --chapter=C1 --mission=M04 --zeppelins` is the harness — the held plane
  fires torpedoes at the moored `hk_zep` with F18's per-zone pools live. Explicitly out of scope: the authored destruction sequence (`breakupzep` →
  13 `break*`, the crash-sink motions) — that is its own M4-sized feature for when zeppelins
  matter to gameplay, not this item.

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
  (`PartState.Fraction`) was why panels tore early; **fixed 2026-08-15** as `BL-384` items (1) and
  (2), re-basing the per-part loop on `PartState.HealthFraction` and splitting the hull loop out
  onto `SummaryHealthFraction`. Owed at the controls with `BL-384`'s playtest line.
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
  *Fix shape:* no code change is owned here. Symptom (2) is `BL-384` item (2). Symptoms (1) and (3)
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

- `BL-302` `[Bug]` **Every destructible takes collision damage in the original — `ACTIVATION` gates the
  *plane's* fate, not the object's. Refutes M3 Decision 6's behavioural reading.** We currently damage
  only the 44 `WeaponOrCollideHit` defs on contact and leave every `WeaponHit` destructible untouched
  by a ram ("ramming a water tower kills *you*, and the tower is untouched" —
  `docs/plans/PLAN-M3-weapons.md`, wave item C27). That reading was inferred from the activation
  census alone and never tested against the original. **Three original-game tests (user, C1,
  2026-08-07) overturn it:**
  1. **Full ram into a C1 hangar: the plane crash AND the hangar's destruction both play.** No C1 def
     is in the 44-def collide set (all are C2 facades / C5 windows / C5 `agyrobus`,
     `docs/formats/destructibles.md`), so the hangar is a plain `WeaponHit` destructible — and the
     collision still killed it.
  2. **A survivable graze along a hangar advanced it to its stage-2 smoke-and-burn damage stage**
     while the plane flew on — so collision damage is *severity-scaled* and flows through the ordinary
     `DAMAGE_SEQUENCE` thresholds, not an instant kill.
  3. **Crashing on bare ground next to a destructible damages nothing** — the plane's death explosion
     has no blast radius (consistent with the data: rockets are the only blast-radius carriers), so
     test 1's hangar died from the *contact*, not the boom.
  **What stands:** the 2,565 `WeaponHit` / 44 `WeaponOrCollideHit` census is a data fact. **What the
  enum actually means:** `WeaponOrCollideHit` = the object breaks and the plane flies *through*
  unharmed (fly-through set dressing); `WeaponHit` = the object is solid — the plane grazes or
  crashes on it — but it still takes the collision damage.
  *Fix shape:* apply collision damage to ANY struck destructible (`FlightController.cs:882-894`
  currently consults `CollideDamageSink` and `AnimRuntime.CollideDamageAt:1257-1269` rejects
  non-`WeaponOrCollideHit` defs — lift that gate for the damage half), keeping the fly-through-on-break
  behaviour gated on `WeaponOrCollideHit` exactly as now, and dealing the damage on the crash branch
  too, not only the graze/fly-through one.
  *Playtest after fix:* ram + graze a C1 hangar in our build and A/B the damage stages against the
  original.
  ⚠ **Traps.** (a) Do **not** add a crash blast radius — test 3 refuted it directly. (b)
  `CollideDamagePerVn` 8 (`FlightController.cs:313`) has only ever fed 0.01-HP set dressing, where any
  value shatters it; once 40–60 HP buildings take collision damage the constant is live and untuned —
  test 2 (a graze reaching stage 2, not death) is the first calibration point, once the struck
  hangar's def and its HP/thresholds are identified (the C1 building def is not `hangar3` — that
  zrdr def is the ON_CALL *doors* anim; one lookup owed during the fix). (c) The `--damage-hd`
  `collide[✓/✗]` gate (`Probes.cs:683`) *asserts the old semantics* — WeaponHit towers ignoring
  collision is its ✗ leg — and must flip with the code, or it will fail green. (d) Plane-vs-plane
  ram damage is NOT this item — no original evidence yet; the struck plane taking damage stays
  unowned. *Supersedes:* the "decision 6 upheld" caveat (`PLAN-M3-weapons.md:1281`,
  `docs/HISTORY.md:5295` records the old behaviour landing) and `docs/formats/destructibles.md`'s
  `ACTIVATION` reading (⚠-noted in place).

- `BL-384` `[Bug]` `[Owed-playtest]` **Our `injure_anims` staging latches one-way where the original
  retracts.** Filed 2026-08-15 from `BL-246`'s decode; the original's rules are
  `docs/org/vehicleDamage.md` ("Damage staging"). **Items (1) scope and (2) pool landed 2026-08-15**
  (`DamageVisuals.OnHullDamage` split out from `OnPartDamage`, the two call sites in
  `FlightController.cs` and both damage-lab targets moved onto the decoded quotients, engine suite
  `damage-staging-pool`); what remains is item (3), and what is owed is the flight A/B below.
  *Evidence (decode, `crimson.exe`, 2026-08-15).* `FUN_004b3800` drives the **def-level** list off
  `[inst+0x2d0] / [inst+0x2cc]` — whole-vehicle health current over health max. `FUN_004b3d70`
  drives the **per-part** list off `[part+0x30] / [part+0x2c]` — that part's health over its max.
  Both start an entry's anim at `fraction <= threshold` and **stop it again** at
  `threshold < fraction`, keeping one handle per entry (`inst+0x890`, `part+0x4c`).
  Three deltas were filed; two are closed.
  (1) **Scope — LANDED 2026-08-15.** The def-level list was walked against whatever per-part
  fraction the caller passed, commented "any part qualifies", so one wing at 10% lit
  `player_damage_trail` with the hull untouched. `DamageVisuals.OnHullDamage` is now its own call
  site off `PlaneDamage.SummaryHealthFraction`, the quotient `FUN_004b3800` computes, and it runs
  even on a zone-less hit.
  (2) **Pool — LANDED 2026-08-15.** Both levels used the combined armour+health progression
  (`PartState.Fraction`). The original divides health only at both levels, armour never entering
  either quotient, so the per-part loop now takes `PartState.HealthFraction`. Because
  `FUN_004b7f80` blocks health damage outright while a part's armour covers the hit, an armoured
  part now crosses nothing at all, including the 0.99 spark shim.
  (3) **Lifetime — OPEN.** The `_applied` set (`DamageVisuals.cs`) latches every stage one-way. The
  original retracts: heal back above a threshold and the anim stops and its handle clears.
  *Fix shape (item 3):* replace `_applied` with a per-entry handle the stop path can clear,
  mirroring the two `+0x890` / `+0x4c` arrays.
  *⚠ Traps.* (a) The combined armour+health fraction is **correct** for the gauge dial
  (`docs/architecture.md:3780`) — fix the staging inputs without touching `GaugeCluster`'s scale.
  (b) Do not change the shipped 0.10 / 0.85 / 0.99 thresholds; they are authored data and they are
  right — only what is divided is wrong. (c) `docs/architecture.md` around the `injure_anims` bullet
  carried the old defect note; it moved with items (1) and (2) and now records the open retraction
  gap instead. (d) Retraction makes the F5 damage lab's repair path visibly un-stage, which is
  faithful, not a regression — `DamageVisuals.Reset` stays for respawn.
  *Playtest after fix (items 1 and 2, owed):* take sustained fire and confirm nothing at all shows
  on a zone while its armour still absorbs, that a panel tears only once that zone's HEALTH crosses
  its threshold, and that `player_damage_trail` starts when the hull gauge reads ~10% and not
  before. The 0.99 spark shim going quiet on early hits is the most visible change.
  *Playtest after fix (item 3):* repair in the F5 lab and watch the stage retract.
  *Cross-refs:* `BL-246` (the decode that produced this), `BL-259` (landed the anchor/staging this
  mis-keys), `BL-297` (the panel-damage semantics re-test — `pdpanelN` thresholds are on the same
  health-only scale, so panels currently tear earlier than the original tears them; its 2026-08-15
  decode confirms this and hands the fix to item (2) here).
  *⚠ One more trap, from `BL-297`'s decode:* item (3)'s replacement of `_applied` must keep
  ONCE-per-downward-crossing. The original's slot is cleared on the upward crossing alone, never
  when the anim ends (`FUN_004b3e20` / `FUN_004b8180` are the repair wipe/restage), so a stage that
  re-fires whenever the fraction stays below its threshold is a different bug, not the fix.

- `BL-385` `[Bug]` **Enemy and wingman aircraft show no damage at all — the whole progressive-damage
  layer is wired for the player only, and their crash is silent.** User at the controls 2026-08-15:
  "the destruction animation only plays for the player but not enemies or wingmen". An AI plane
  flies pristine until the frame it explodes.
  *Evidence (code, 2026-08-15).* `controller.Visuals` is assigned at exactly two sites —
  `FlightRigAssembler.cs:294` (the player rig) and `GameSession.cs:1477` (the parked damage lab).
  `AiAircraftSpawner.Spawn` never assigns it, so `Visuals` is null on every AI controller, and all
  three call sites are null-conditional (`FlightController.cs:972` projectile hit, `:2408` graze,
  `:798` respawn reset). The `DamageEffectSink`/`DamageEffectStop` wiring is likewise inside
  `if (controller.Visuals != null && …)` at `FlightRigAssembler.cs:452-488`. AI planes DO get
  `Damage` (`AiAircraftSpawner.cs:102`) and DO get a rig runtime with the damage-stage defs bound
  (`AiAircraftSpawner.cs:153-158`, `EffectCatalogue.cs:228-235`) — the stages are staged and
  playable, just never triggered.
  *Evidence (data, 2026-08-15) — the original authors a separate AI trail and we play none of it.*
  `basic_airplane` carries a def-level `injure_anims` of **`[[0.5, "pfsmoketrail"]]`**
  (`extracted/zrdr/vehicle.zrd.json:4108-4114`), inherited by every non-player airframe
  (`bloodhawk`, `avenger`, `fury`, `brigand`, `devastator`, `autogyro`, `peacemaker`, the `r*`
  variants, and `bswingman` — wingmen included). The def ships in every chapter as
  `extracted/<ch>/cam_anim/piratefighter-pfsmoketrail.json`: a `smokepuffer` + `firepuffer` pair at
  `prop1`. So in the original **an enemy starts trailing smoke at half hull health** — a combat read
  the player uses to tell a hurt bandit from a fresh one — and it is one stage, not the player's
  two (no `pfsmoketrail` counterpart to `player_fuelleak`).
  *⚠ The threshold is whole-vehicle health, not a part fraction* — same decoded driver as the
  player's (`FUN_004b3800`, `docs/org/vehicleDamage.md`), so this item lands on top of `BL-384`'s
  correction rather than beside it. Doing this one first would wire the AI list to the same wrong
  input.
  *Fix shape:* give `AiAircraftSpawner.Spawn` the `DamageVisuals` construction and the sink/stop
  pair that `FlightRigAssembler.cs:284-299,452-488` build, and a `FlightAudio` (see the audio half
  below). Both blocks are near-verbatim; the shared shape wants extracting rather than copying.
  *The audio half.* `FlightAudio` is built only at `FlightRigAssembler.cs:304-309`; the AI spawner
  never sets `controller.Audio`, so `PlayCrashBoom` (`FlightController.cs:2027-2048`) and
  `OnEngineStop` never run for an AI kill — **an AI fireball is completely silent** — even though
  the `ai_crash_dirt` / `ai_crash_water` arms already exist in that switch. Note the crash
  CHOREOGRAPHY is not missing: `Crash()` is one path for everyone and
  `EffectCatalogue.CrashDefTableFor` (`WorldEffectsFactory.cs:311-319`) keys off `IsHumanPiloted`
  onto the original's own `ai_crash_*` family. If an AI kill shows no fireball either, suspect that
  family and its meshless `kestrel` scaffold anchor, not a missing call.
  *⚠ Traps.* (a) Per-AI-plane panel pairing and puffer pools at spawn time is a real cost on a
  chapter holding many aircraft — measure before wiring it unconditionally, and consider gating the
  panel-flip half on distance or aircraft count. The trail half is one puffer pair and is cheap.
  (b) `pfsmoketrail`'s `anim_root_name` is `piratefighter`; whether it retargets onto every AI
  airframe through the runtime's root fallback (the `player_pfighter` shape noted at
  `FlightRigAssembler.cs:458-459`) or needs an explicit OPERAND_NODE retarget is **unverified** —
  check before assuming the player path's resolution carries over. (c) Do not give AI planes the
  `player_*` stage menu; their data names one stage and a different anim.
  *Open question, not part of this item:* an AI wreck never leaves. `Crash` arms `_autoRespawnIn`
  but the respawn gate (`FlightController.cs:1090-1107`) needs a key press or
  `HoldSegments`/`AutoRespawnAfter`, none of which the spawner sets, and nothing calls `QueueFree`
  on an AI controller — so wrecks accumulate for the session. Whether the original also leaves them
  is unchecked; do not "fix" it without that check.
  *Playtest after fix:* shoot down a wingman and an enemy in C1 — smoke should start around half
  health and the fireball should be audible.
  *Cross-refs:* `BL-384` (the fraction correction this depends on), `BL-246` (the decode),
  `BL-343` (wreck momentum — the other AI-wreck item).

## Weapons & combat

- `BL-066` `[Feature]` **M3-deferred — ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- `BL-067` `[Feature]` **The gun/hardpoint configurator UI** (deferred from M3, decision 9 — M3 flies stock loadouts
  only, but its loadout model is data-driven so this drops in without rework). The original's
  screens are `GUNS.SCRIPT` (4 gun slots, `gn_d_gun0..3`, engine callbacks 2249/2250) and
  `HARDPOINTS.SCRIPT` (2 hardpoint slots, `hp_d_point0..1`, callback 2245), plus
  `PLANECONSTRUCTION.SCRIPT` / `PURCHASE.SCRIPT`. Both are **pure UI layout** — per-plane slot
  counts, weapon costs and the economy are all executable-resident, so the *buying* half would
  have to be invented. The mount names are data (`IDS_AIRFRAMEGUNGROUPNAMES`, ui_strings
  3060–3079) and the per-plane stock table is authored, so the *placing* half is real.

- `BL-213` `[Research]` **Does the original splash when gun rounds range-expire over water?** Needs a CAP of the
  original (fire out to sea from altitude, watch the 1000 m expiry point). Until answered, our rounds
  expire silently, which METHOD-18 documents as correct-per-data.

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

- `BL-227` `[Tuning]` `[Owed-playtest]` **Rocket blast falloff + knockback magnitude (D10, 2026-08-01).** The radius and full
  health damage are authored (`IMPACT_PROXIMITY`, `HEALTH_DAMAGE`), but the shipped data does not
  encode a falloff curve or impulse. D10 uses linear falloff to zero at the edge and
  `BlastImpulsePerDamage = 1 N·s` on a directly struck rigid body. Judge clustered-object damage
  and physical push against the original before changing either.
  ⚠ Traps: do not retune the authored radius or fuse distance; `DAMAGE 0` specials carry large
  effect radii and are deliberately excluded from blast damage.

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
  ⚠ Traps: (a) the six plain rockets author `DETONATION_DISTANCE == IMPACT_PROXIMITY`, so a
  first-entry-into-range fuse always detonates exactly where the linear blast falls to zero and
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
  ⚠ **The six `DirtDebris*` constants left this entry 2026-08-07: `BL-313` deletes the effect they
  tune, so there is nothing to A/B.** What remains here is the building ricochet:
  `RicochetSparks` **8**, `RicochetSparkSize` **0.55 m**,
  `RicochetSparkLife` **0.55 s**, `RicochetSparkSpeed` **22 m/s**, `RicochetSpreadDeg` **90°** (a
  stand-in — both authored assets are missing from the install). The water-splash column width
  is settled and out of this entry: `SplashColumnWidthScale` **8×** confirmed at the controls
  2026-08-06 with the fades in (`BL-265` closed — the authored quad is 5 cm wide, sub-pixel past
  ~30 m; the reference ticks measure ~0.35 m, which 8× matches). A/B the rest against
  `Dirt Splash.png` at the controls; the splash *height/timing* curves are authored data, not TUNE.

- `BL-313` `[Bug]` **Our gun hits on dirt fling `bit01`–`bit04` chips the original never draws — delete
  the sprite half of `SpawnDirtDebris`** (`PT-27` at the controls + `OriginalScreenshots/Videos/70 DD
  Dirt.mp4`, 2026-08-07). Three independent lines agree, which is why this is a deletion and not a
  tune:
  - **The data, read literally.** The slug gunhit defs fling `bit1`/`bit2`/`bit3`/`chunk` at
    `PLAYER_RANGE 200`, and **`bit1`–`bit3` carry 0 vertices in this install** (measured C1/C2)
    while **`chunk` has one 4-vertex quad** (`docs/formats/weapon-effects.md`). Read as written,
    that draws the chunk quad — the perforated `gun_barrel` shroud band — and nothing else.
  - **The footage.** `70 DD Dirt.mp4`: a 70-slug burst into dirt shows **only the chunk and one
    very faint black puff**, no chips. The puff is the authored slug `blacksmokepuffer`
    (`TIME_INTERVAL` 1.1 s, one puff per hit), so both visible elements are accounted for.
  - **The inference's motive is answered elsewhere.** `architecture.md` recorded the leap as *"the
    def's bit1–3 gamez nodes carry no geometry, the textures ARE the chips"* — motivated by the
    `bit01`–`bit04` textures shipping in every chapter archive. They are **not** orphans, but their
    consumer is **`zep_skin_fire3`/`zepskinfire_3`** (user, 2026-08-07), a pooled template whose
    siblings each carry a real child mesh index (`gamez.md:48–53`). The textures earn their place
    without the zero-vertex gunhit nodes drawing anything, so the leap has nothing left holding it
    up.
  *Fix shape:* delete `SpawnDirtDebris`'s sprite emission and `Projectile.cs:245`'s
  `DirtDebrisTextures`; the `chunk` quad stays exactly as it draws today (`CAP-25` retired
  2026-08-07 having confirmed the original shows it). Retract the inference in
  `docs/architecture.md` and `docs/formats/weapon-effects.md` rather than silently overwriting it.
  `BL-289`'s six `DirtDebris*` constants go with the effect.
  ⚠ Trap: **do not generalise "zero-vertex node ⇒ draw its texture as a sprite" anywhere else** —
  that is the reading this item retracts. If another effect is found relying on it, it needs its
  own evidence, not this precedent.

- `BL-290` `[Bug]` `[Blocked: CAP-28]` **Torpedo flight dynamics: the original's torpedo has a max/cruise speed and
  visibly slows after firing; ours flies the generic projectile model** (PT-38, 2026-08-06). Data
  check done: the decoded weapon block carries only `VELOCITY` (muzzle/flyout speed) and
  `ACCELERATION` (0 = constant velocity) — no drag or speed-cap field (`docs/formats/weapons.md`) —
  so this is engine behaviour to measure, not data to consume. Hypothesis recorded, not evidence:
  the torpedo may inherit the launching plane's speed and decay toward its own authored
  `VELOCITY`; a fast launch would then visibly slow, as a drop-torpedo physically should. `CAP-28`
  films it; measurement can reuse `analysis/video-flight-calibration` if the HUD is in frame.
  `wep_14` is mountable via `--rocket=wep_14` (no stock loadout carries it).

- `BL-357` `[Feature]` **The hardpoint selector steps one way only; the original cycles in both
  directions.** *Evidence:* the user at the controls of the original, 2026-08-14: the player selects
  an individual hardpoint (the half that settled `BL-062`, closed the same day), and the selection
  can be stepped clockwise *and* counter-clockwise. Ours has exactly one selector input per weapon,
  `H` / D-pad Right for pylons and `G` / D-pad Left for gun groups
  (`FlightController.cs:1456`, `docs/controls.md`), and `WeaponCursor.NextSelectable`
  (`WeaponCursor.cs:34`) only ever scans forward. *Fix shape:* a `PrevSelectable` backward scan
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
  *Cross-refs:* `BL-067` (the configurator, where mixed loadouts finally make the direction
  matter), `BL-296` (ActionMap/rebinding seam), `git log --grep=BL-062` for what settled the
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

- `BL-378` `[Bug]` **Our AI has no terrain avoidance, so a net authored below a ridge flies AI into
  it. DECODED 2026-08-15; what is left is implementation.** *Evidence:* the user at the controls,
  2026-08-15, on the build that made anchored nets ride their target (`BL-377`, closed): C1's
  patrol nets sit at an authored 400 m (`M4ReinfAce`) and 350 m (`M2Ace`), which clashes with
  elevated terrain, and the nets now ride the player into any part of the map.
  ⚠ **The two natural fixes are both wrong, and the binary says so outright.** A net node's
  altitude is neither above-ground nor target-relative: `FUN_00432010` returns `out.y = node.y`
  verbatim, with the trailer offset applied to X and Z only
  ([`docs/org/aiPilot.md`](docs/org/aiPilot.md) "The trailer"). There is no terrain sample anywhere
  in the node read, and none in the patrol follower `FUN_0041d1f0` either. Making our Y
  terrain-relative or player-relative would put every AI on a different route from the original's,
  on all 222 nets, to paper over a missing behaviour.
  **The behaviour that is actually missing is a crash-avoidance MODE.** `FUN_0041f810` is a
  per-plane ground-proximity check that writes the AI substate at vehicle `+0x358`:
  - below the global altitude floor `DAT_0071c3f0` (**20.0** in the image, the only unconditional
    store) it sets state **3** outright;
  - between that floor and `DAT_0071c3f4` (**8000.0**) it casts a ray **4.5 ×** the vector the
    vehicle's virtual `+0x04` accessor returns (velocity by shape and use, so ~4.5 s of travel,
    which is the one inferred step here) through `FUN_004c8f70`, and sets state 3 on a hit;
  - above 8000 m it runs no check and CLEARS state 3 back to 0.
  The re-check is throttled per plane to the game clock plus `0.5–1.0 s`, drawn from
  `rand()/32767`, so it is not a per-frame cast. State 3 is then handled by the follower's own
  switch (`FUN_0041d1f0` case 3): the steering target becomes the plane's own position with
  **Y + 1000**, flown through parameter block `DAT_0061fb48` (throttle band 0.6–1.3) instead of
  patrol's `DAT_0061fb68` (0.8–1.1). `FUN_0041b560`, the shared steering law, reads the same
  `DAT_0071c3f0` floor directly, and `FUN_004216e0`'s maneuver suspends it (writing −FLT_MAX, with
  a paired per-vehicle ceiling at `+0x314` set to +FLT_MAX) for its duration.
  *Fix shape:* a mode in `AiModeMachine`, not a change to `AiNetFollower`. `AiPilot` currently sets
  `TargetAltitude = patrol.CurrentTarget.Y` and its only altitude leash handles being too HIGH
  (`_altRecovering`, `AltLeashEnterM`), so there is no floor and no lookahead at all. The ray needs
  a world collision query; `GameSession.GroundSampler()` is a height sampler, not a ray, so decide
  which of the two to use rather than assuming the sampler is enough on a cliff face.
  ⚠ *Traps.* (a) **Do not clamp the net.** The climb-out is a state that overrides steering and
  then releases; a clamped node altitude would permanently move the route. (b) The 20 m floor is a
  flat world-Y floor, NOT terrain-following, so it does not by itself save a plane over a 600 m
  ridge; the raycast is what does. (c) The +1000 m target is relative to the PLANE, not to the
  terrain or the net. (d) `ZeppelinMotion` shares the follower but is a different actor with no
  flight model; do not fold zeppelins into an aircraft crash-avoid mode without checking whether
  the original runs one for them. (e) The throttle-band swap is part of the behaviour, not
  decoration: a climb-out at patrol throttle is a slower climb than the original's.
  *Playtest after fix:* fly Instant Action in C1 over the high ground east of the spawn with F13 up
  and `--debug-markers`, and watch a netted enemy cross a ridge that sits above the net's authored
  400 m: it should pitch up and climb out of the state on its own rather than fly into the slope,
  and it should return to the graph afterwards rather than stay in the climb.
  *Cross-refs:* `BL-364`,
  [`docs/org/aiPilot.md`](docs/org/aiPilot.md) "The trailer" (the anchored-net ride that carries a
  net over any terrain and so made this visible; closed as `BL-377`,
  `git log --grep=BL-377`).

## Flight model & collision physics

- `BL-089` `[Feature]` **Nitro booster — scoped, low priority (the user's standing call).** Recorded because the data is
  complete and waiting, not as a discovery. Shipped: `MSG_CMD_NITROUS` ("Use Nitro-Booster") is a
  bindable command and `MSG_HUD_NITRO` ("Nitrous: boost: %1 charge: %2") its two-value readout;
  `nitrogauge` is in `instruments.zrd.json`'s cockpit layout and all 11 planes carry
  `nitrogauge` / `nitro_backplate` / `nitro_boost` / `nitro_charge` gauge nodes in planes.zbd;
  `nitro_boost` / `nitro_decay` are ON_CALL anim defs in `plane_props.zrd.json` (with `ai_nitro_*`
  wrappers) firing `snd_nitrostart` / `snd_nitrostop` at `nitroprop1` and driving `nitropuff1-4`
  plus `spin_nitrorotor1-3`; `snd_nitro` is a LOOPED 3D loop (`RANGE [30,400]`). `PropParts`
  already classifies `nitropropN`, and both `PlaneBuilder` and each plane's own `RESET_STATE` ship
  it INACTIVE — so the visual half is one `Kind.Nitro` unhide away.
  ⚠ **The numeric tuning stats did not ship.** No nitro key exists in `vehicle.json` or
  `player.json` — boost magnitude, charge capacity, burn rate, recharge rate and the speed cap are
  executable-resident, so this needs a hand-tuned balance pass A/B'd against the original, not a
  data port. (`rof/ui_strings.json` carries "NITRO-BOOST: %4!s!" on the purchase screen and the
  buyable engines come in plain and "… nitro" variants, so the engine choice is what grants it.)

- `BL-095` `[Research]` **`player.json`'s physics block — DECODED END TO END. What is left is
  implementation, not research.** Every key is now read out of `crimson.exe`, with the write-up in
  [`docs/org/flightModel.md`](docs/org/flightModel.md): the flight block on 2026-08-09, ground blow
  and `bounce_factor` on 2026-08-14. Units come off the conversion the parser applies, never from
  inference: **speeds are MPH** (`× 0.44704` on load), **`liftAOAs`/`maxAOA` are degrees** (the
  parser takes their cosine), and **`highGs`/`lowGs`, `groundblow_*`, `ai_groundblow` and
  `bounce_factor` are raw scalars**. World lengths are **metres**, identified positively from
  `nom_gravity`'s 9.82 reference divisor.

  | Key (authored) | Where it stands |
  |---|---|
  | `yaw_low_speed 0.0625` / `yaw_high_speed 0.17` / `yaw_fade_in 10` / `yaw_max 50` / `yaw_fade_out 400` | Decoded and **implemented** (`PLAN-flight-model-rewrite` C21), retiring `BL-108`'s interim `eff` |
  | `turn_fade_in 10` / `turn_fade_out 50` | Decoded: the roll/pitch base ramp from airspeed alone. **Unimplemented, owned by `BL-330`** |
  | `maxAOA 46.0` / `liftAOAs [5,9]` / `lift_accel_rate 0.75` | Decoded. `liftAOAs` is an airflow blend, **not** a load-factor ramp. Consumed by `PLAN-flight-drag-lift` B12 as a **hypothesis under test, not a decode**. ⚠ That plan justifies its lift re-key partly on "the aircraft must hold 100° of bank, which needs `1/\|cos 100°\|` = 5.8 g" (`PLAN-flight-drag-lift.md:486-487`, `:7`, `:32`, `:200`, `:549`). `CAP-33` showed the ADI reads airframe **attitude**, not the turn's bank, so the ~100° attitude is real but the load factor does not follow from it that way — `CAP-01`'s turn banked 58.7°, ~2 g. The re-key is not challenged; its stated arithmetic is |
  | `high_speed_pitch_fade [1000,1001]` / `highGs [9,15]` / `lowGs [-6,-9]` | Decoded as **authored unreachable** (`C24`, `D33`). Nothing implemented, which is the correct outcome |
  | `drag_factor 1.5` (global) / `drag_fade_speed 40` | **Dead in the executable** (B14): parsed, then read by nothing. The per-plane `drag_factor` is the only drag scale |
  | `groundblow_elev 400` / `groundblow_mag 10` / `ai_groundblow 0.5` | Decoded 2026-08-14, emitter set settled 2026-08-15, **implemented and flown 2026-08-15** (`git log --grep=BL-359`). `ai_groundblow` is loaded and deliberately unread: the AI path is a different law |
  | `bounce_factor 0.6` | Decoded 2026-08-14. Units settled; implementation owned by `BL-172` |
  | `nom_gravity 20.0` / `stall_mag 1.25` | Already consumed |

  ⚠ **Three keys are authored so the feature they gate never fires. That is a finding, not a gap to
  fill: do not implement them as missing features.** `high_speed_pitch_fade` is beyond any attainable
  dive speed (the highest of the eleven ceilings is the Bloodhawk's 528.5 mph against an authored
  1000, `C24`), and `highGs`/`lowGs` sit past the hard ±5/9 G lift clamp (peak demand 2.13–5.01 G
  against a threshold of 9, peak α 8.9–25.6° against `maxAOA` 46°, all eleven airframes, `D33`).
  `lowGs` is unreachable twice over, since the demand is a vector LENGTH that never goes negative.
  Tables in [`docs/org/flightModel.md`](docs/org/flightModel.md) and
  [`POST-B14.md`](analysis/flight-model-baseline/POST-B14.md); pinned by
  `CSVM.Tests/ControlLimiterTests.cs`, which asserts each airframe against its OWN loaded thresholds,
  so a per-plane override or a data edit that brings either into reach fails the suite.
  ⚠ The Bloodhawk's 5.01 G peak is 0.2 % **past** the executable's fallback `highGs[0] = 5`, so under
  the fallbacks the limiter would fire, barely. The disproof rests on the authored 9.

  **Ground blow: DECODED from the binary 2026-08-14, IMPLEMENTED and flown 2026-08-15**
  (`git log --grep=BL-359`). Mechanism, constants, gates and emitter rule are in
  [`docs/org/flightModel.md`](docs/org/flightModel.md); `CAP-02` closed 2026-08-07 and the GDD's
  §4.1.7 *mechanism* is confirmed by the code, its emitter *list* replaced by the rule that produces
  it. In short: `FUN_0048bf60` casts a ray of
  `groundblow_elev` **metres** along the nose, and `FUN_0048c220` adds a rotation away from the hit
  surface into the **same accumulator the stick writes to**, one call after the stick terms in
  `FUN_0048c470`. It is a bias on control response, not an applied force. A command *into* the
  obstacle is met with `0.05 × groundblow_mag`, so it is halved and never reversed; a command *away*
  is amplified by up to `1 + 10·S²`; and on a dead-on approach the bias axis `n × b` collapses to
  zero, so a head-on gets no help at all. The AI path is a different law, not a scaled one: a fixed
  push of `ai_groundblow × groundblow_mag × S`, independent of what the AI commanded, linear in
  proximity, and not `dt`-scaled. **The AI half is not built** — the player term is, in
  `FlightModel.GroundBlowTerm` off `FlightController.ProbeGroundBlow`.

  ⚠ **`groundblow_elev` 400 is 400 METRES of ray length, not a 400 ft trigger range, and the
  footage agreement was a coincidence of digits.** The `CAP-02` onset bracketed at 427 → 376 ft sits
  well inside a 1,312 ft ray at roughly 70 % strength, so the ray never explains an onset there and
  whatever timed that pull was not this threshold. The footage record that carried this reading,
  `analysis/video-flight-calibration/FINDINGS.md`, was deleted 2026-08-14 as superseded.
  ⚠ **`groundblow_mag` 10 is not a TUNE** and must not be fitted to the cliff clip's 17° step. It is
  an authored constant with a traced consumer, 6.7× the engine's own fallback of 1.5.
  ⚠ **The proximity power is `S²`, settled at `0x0048c30f` on 2026-08-15**: `FUN_0048bf60` hands
  back the axis ALREADY scaled (`A·S`) plus `S` as its return value, and the caller uses that one
  scaled vector twice, once in the dot and once in the add. Reading the write-up's `p` as
  `dot(accum, A·S)` *and* keeping an `S²` in the add counts it three times. `CSVM.Tests`'
  `GroundBlowTests` pins the quadratic against the linear reading.
  One `CAP-02` anomaly stands unexplained and is now moot for implementing: `Up Down` recovery #1
  reads 2.30× free air with 28–30 % of samples gated at γ ≈ −63°, suspected estimator artifact, and
  every clip designed to reproduce it came back flat.

  **The original's 1.6×-slower banked turn has no authored candidate left, and this block is not
  where it will be found.** All three died: the hardcoded bank coupling moves the banked rate the
  wrong way by construction (`C22`), `highGs` is a limiter that never fires (`D33`), and
  `turn_fade_*` is an airspeed ramp that is saturated at 1 everywhere the gap appears (`C21` era
  correction, 2026-08-09). What remains is the measurement's own interpretation: `CAP-01`'s
  18.95 °/sim-s at 222.94 mph implies **58.7° of bank** (`atan(V·ω / nom_gravity)`, and it is
  `nom_gravity` 20 m/s² in that formula, not 9.81). `CAP-33` (2026-08-15) settled that the ~100° its
  ADI shows is not a bank at all: flown with the pilot holding a known 60–70°, the ADI sky centroid
  read a mean 105.1° while `V·ω / nom_gravity` read 62.2°, and the ADI's 46° swing tracked the pitch
  cycle (`r = +0.886` against climb rate) rather than the heading rate (`r = −0.091`). An ADI shows
  airframe attitude, which in a high-α pull sits tens of degrees off the bank of the turn. So the
  "measured bank" half of this disagreement was never real, the original **is** flying coordinated,
  and 58.7° is `CAP-01`'s actual bank. The rate gap itself is untouched by this and stays open.
  (`git log --grep=BL-307` for the closing record.)
  ⚠ **Do not fill the hole by inventing a rate limiter from field names.** A naive speed/bank
  coupling that quietly costs pitch authority is exactly the wrong-mechanism fix `BL-124`'s history
  warns about, and `maxAOA`/`liftAOAs` were consumed as a hypothesis under test rather than as a
  decode (`docs/plans/PLAN-flight-drag-lift.md` B12).
  ⚠ **Do not re-open `drag_factor` as a name collision.** The global one is dead in the executable,
  so `vehicle.json`'s per-plane `drag_factor` is the only drag scale and there is nothing to
  reconcile.

  **Still to do, all of it downstream of this entry:**
  1. ~~Implement ground blow.~~ **Landed 2026-08-15** (`BL-359`, closed; `git log --grep=BL-359`),
     player path only and confirmed at the controls. The AI law is unbuilt and is a different one.
  2. **Implement the roll/pitch base ramp** (`BL-330`) and **the `bounce_factor` restitution**
     (`BL-172`).
  3. ~~Confirm the zeppelin emitter on any zeppelin mission.~~ **Answered 2026-08-15 from the binary
     and the shipped data, no capture needed.** The emitter test keys on whether a hit node carries
     the spawn mark `0x40000000` and, if so, whether its registry entity is on a scripted path; never
     on vehicle type. IA1's zeppelin is a plain gamez node with no spawn mark, so it repels exactly
     like terrain. Written up in
     [`docs/org/flightModel.md`](docs/org/flightModel.md)'s "Ground blow"; the path mechanism it
     surfaced is `BL-361`. In the build, the rule is the `CollisionLayers.World` mask on the probe,
     which is why an aeroplane does not repel and the zeppelin does. (`CAP-33` was flown 2026-08-15;
     it settled the ADI-vs-implied-bank question, not the banked-turn rate gap.)

  When those are homed elsewhere or done, this entry retires: there is no research left in it.

- `BL-330` `[Feature]` **The low-speed control-authority ramp — decoded, corroborated at the
  controls, and not implemented.** `FUN_0048bdd0` scales **roll and pitch** authority by a base ramp
  taken from airspeed alone: 0 below `turn_fade_in` (**10 mph**), rising linearly to 1 at
  `turn_fade_out` (**50 mph** as authored here), flat at 1 above. So the slower the aircraft gets,
  the mushier it gets — and at 10 mph roll and pitch are gone entirely. Our `FlightModel` applies no
  such fade on either axis; the only thing that makes our aircraft feel unflyable when slow is the
  stall block taking the nose, which is a different mechanism. Write-up in
  [`docs/org/flightModel.md`](docs/org/flightModel.md)'s "Control authority vs speed".
  **Two-source support, which is why this is a `[Feature]` and not a `[Research]`:** the trace above,
  plus a player report from the controls (2026-08-09) that the original hampers the controls at stall
  speed — offered unprompted while flying the post-`D33` build, i.e. describing the original from
  memory rather than reading it off ours.
  ⚠ **Where 50 mph falls decides how visible this is, and it differs by airframe** (stall speeds from
  `PLAN-flight-model-rewrite` B15): nine of the eleven stall at 52–57 mph, i.e. *above* the ramp's
  top, so for them the fade only bites once already stalling and falling. The **Balmoral** (45.5)
  reaches its own stall at ≈89% authority. The **autogyro** (18.5) flies a long way inside the ramp
  and reaches stall at roughly **21%** of roll and pitch authority — near-inert controls while still
  flying, which for that airframe reads as deliberate rather than as a bug.
  ⚠ **Traps.** (a) This is *airspeed*-keyed, not stall-keyed — do not gate it on `isStalled()`, or
  the two mechanisms compound and the fade vanishes on the airframes whose stall sits above 50 mph.
  (b) It is roll and pitch **only**: yaw has its own, different curve, already landed (`C21`), and
  extending this ramp to the rudder would double-fade it. (c) `FlightModel.cs`'s rotation comment
  currently asserts "roll never fades in the original" — true of *high* speed, false of low, and the
  same misreading of this function that had `turn_fade_*` filed as a bank fade until 2026-08-09 (see
  `BL-095`). (d) It reaches into stall recovery and the ground handling the race grid sits on, so it
  wants a flown check, not only a probe row.

- `BL-096` `[Feature]` **Angle of attack is now fittable and is not modelled.** The ADI shows hysteresis against
  vertical speed round the loop — expected, since the ball shows attitude while `dh/dt` follows the
  flight path, and AoA is exactly what separates them. That hysteresis *is* the AoA signal, and it
  became fittable when the clock factor was pinned. Pairs with the `maxAOA`/`liftAOAs` data above.

- `BL-097` `[Research]` **The roll's spin-up shape is untested.** Mid-roll steady rate reads 240 °/wall-s while the whole
  360° averages 246, so the original's roll was **still accelerating when it finished**. Our
  `1/damp` spin-up reproduces the total time (1.98 s vs 2.05) — whether it reproduces the curve is
  unknown, and only a per-frame bank trace would say.
  ⚠ **Re-read this as a held-key step question** (2026-08-03, out of `BL-147`/`CAP-04`): the
  original's controls are digital, so there is no partial aileron deflection to spin up and a
  "moderate roll input" clip cannot be flown. The only measurable transient is the leading edge of a
  *held* key from steady flight — and at 30 fps that edge is unresolvable, exactly as it was for
  pitch. Any roll-transient capture needs ≥60 fps constant frame rate.

- `BL-115` `[Tuning]` `[Owed-playtest]` **Flight model** — `StallNoseRate`, `KnifeAlignFloor`
  (`LowSpeedDragBlend` closed 2026-08-07 by `PLAN-flight-drag-lift` A1; `ClimbGravityScale`
  **retired** 2026-08-09 by `PLAN-flight-model-rewrite` D32 — see its bullet below, which stays
  because the climb residual it uncovered is still open).
  **`PitchTune` / `YawTune` / `RollTune` / `ThrustConst` are not on this list**: all four are
  measured against the original frame by frame and asserted by the `flight-envelope` suite, so they
  are not TUNE knobs and a feel A/B cannot overrule them.
  ✅ **Flown 2026-08-09, straight off the completed `PLAN-flight-model-rewrite` (`3c8f5ae`): "feels
  a lot better overall, every manoeuvre."** The whole rewrite — the lift demand and its clamp, the
  Mach polar, the thrust curve, per-airframe stall, the authored yaw table, bank coupling, the
  weathervane, exponential damping, the retired knife-edge sag and the attitude-thrust climb — reads
  as an improvement at the controls, not only on the instruments. A **pass on the direction**, and
  the reason the two constants still listed above are not being tuned toward anything: nothing in the
  flown report points at them. It is **not** a pass on the three recorded conflicts (the zero-thrust
  drag deficit, the banked-turn rate, the climb plateau), none of which a feel report can settle. The
  same sortie produced the observation behind `BL-330`.
  **`CAP-05` decoded 2026-08-04** (four clips, all gating rigid) — three of the four are answered and
  one is not:
  - **`StallNoseRate` 1.0 rad/s is ~17× too fast.** In `CAP-05 Stall 0% Thrust no input` the nose
    holds **+4.2 ± 0.1°** through the whole deceleration, starts falling only at **76 mph = 0.25 fd**
    (minimum speed reached 69.8 mph = **0.232 fd**), then drops at **3.38 °/sim-s = 0.059 rad/sim-s**
    from +4.1° to −21.2°, and **stops at ≈−22°** once speed rebuilds past 0.40 fd. It does not chase
    world-down, so "rad/s toward world-down at full stall depth" is the wrong target as well as the
    wrong rate. The break is wings-level and clean: the compass turns **0.0°** across the whole
    24.8 sim s, no wing drop. ⚠ Note `StallSpeedFrac` **0.30** is implicated too — the original breaks
    at 0.25 fd, not 0.30 — but that constant is outside this entry; it was `BL-148`'s.
    **Closed 2026-08-04 by `CAP-06` and landed the same day (`BL-148`, polish-7 A2): the constant is
    `split`, not moved.** The original's *warning* lights at 0.299 fd (four clips) — 0.30 is correct
    there — while the *nose-drop* is at 0.25 fd, measured in the same frames of the same clip.
    `StallSpeedFrac` is now the nose-drop at **0.25** and `StallWarnFrac` the lamp at 0.30, so the
    rate/target question in this bullet is the only part of the stall model still open.
  - **`LowSpeedDragBlend` 0.35 gave 4–6× too much drag below cruise — closed 2026-08-07.** The same
    clip is a thrust-free drag probe: measured `D` is **0.36 / 1.11 / 2.82 / 3.74 m/s²** at
    x = 0.25 / 0.35 / 0.46 / 0.50 against the blend's **7.69 / 12.13 / 17.91 / 20.25**. The model-free
    form: engine off at 152.6 mph in a +5° climb the original decelerates at **6.24 m/s²** where ours
    took 21.6. `PLAN-flight-drag-lift` A1 replaced the blend with a piecewise power law
    (`DragExpLow` **3.278** / `DragExpHigh` **2.663**) fitted numerically against the real integrator
    and all four independent measurements at once (this clip, `accel-150-290`, `terminal-dive`, the
    1/8-throttle equilibrium) — within 4% of every point above. ⚠ The `x^2.67` figure once quoted
    here as an independent confirmation was **wrong**: it is an artifact of assuming thrust is linear
    in throttle (`0.459^2.67 = 0.125` exactly — the exponent that makes 1/8 throttle give 1/8 thrust
    by construction, not a drag measurement). Solving each of the four points above for its own
    exponent gives 3.69–4.00, which is what the fitted 3.278/2.663 piecewise curve actually
    reproduces.
    **`CAP-31` decoded 2026-08-07 — the 1/8 case, and it clears the curve rather than condemning
    it.** This is the capture that was owed against this bullet, and it is the discriminator
    `PLAN-flight-drag-lift` A1 asked for: at 1/8 throttle there is an equilibrium, so the 290 → 150
    time tests the drag curve's *shape* between x = 0.5 and 0.96 and not its scale. Gate `OK` (dial
    dx corr +1.00, freshly run), level throughout (+0.96 ft/sim-s over 38 sim s), entry plateau
    299.29 ± 0.13 mph. Measured **290 → 150 mph in 13.94 ± 0.29 sim s** against the model's published
    **12.10**, i.e. the original **coasts longer than we do**. ⚠ Both alternatives `playtest.md`
    offered were "12.1, or markedly faster"; neither happened, and the *faster* branch was the one
    that would have made the user's unmodelled-airbrake hypothesis live. It is **not supported** —
    the "feels like coasting" report is faithful reproduction. ⚠ **The chop is not instantaneous:**
    deceleration builds for 3.5 sim s after the speed leaves the plateau (peaking at 244 mph, which
    constant thrust and a monotone drag cannot produce), and the panel carries no throttle indicator,
    so pilot key cadence and engine spool-down are indistinguishable here. That makes 13.94 an upper
    bound on the instant chop the probe runs — estimated **12.8–13.3 sim s** two ways — while
    **240 → 150 mph = 11.33 ± 0.24 sim s** is transient-free and needs no extrapolation. ⚠ Separately,
    **`Probes.cs`'s `eighth-throttle-speed` target of 137.9 mph is ~2% high.** `CAP-31` is at
    134.84 mph and still falling at its last frame, and the `accel` clip's 1/8 entry leg — re-decoded
    the same day — is not a plateau either but a rise from 134.25 to 135.56 mph. The two bracket
    **≈135 mph (0.449 fd) ± 1**, and our 137.87 "match" was against a number no clip supports.
    Retargeting that row and running the model over the two new times is model work and is not done
    here (`FINDINGS.md`, `CAP-31`).
    ⚠ `CAP-31` says **nothing** about the three constants still open below — it is a level
    deceleration, so it carries no stall, no climb and no knife-edge.
  - **`KnifeAlignFloor` 0.35 — the last of the three, and the other two are gone.**
    ✅ **`KnifeNoseSag` / `KnifeNoseRate` retired 2026-08-09** (`PLAN-flight-model-rewrite` D31): the
    decoded bank→yaw coupling and the weathervane produce the sag, and produce its onset BETTER than
    the fitted step did — nose at +3 s **−4.94°** against the measured −4.9°, where the bounded step
    on top read −7.28°, and sink 12.7 → **5.7** ft/s against a measured 0.5. Playtest note (2) below
    is confirmed and fixed: the term keyed on `1 − |bodyUp·up|`, so it fought every wings-level pull
    at up to 11.5 °/s — `zoom-climb` moved toward its measured 936 ft on all eleven airframes and the
    Balmoral can now reach the altitude cap (4471 → 6572 ft). `KnifeEdgeTests` pins both the shape
    and the absence of the zero-bank leak; `Probes.KnifeEdge` is the re-runnable instrument.
    **`KnifeAlignFloor` stays at 0.35, on evidence rather than for want of a measurement** (D31): it
    is the ONE surviving use of `wingVert`, and the decode is silent about it (the nose-chase is the
    remake's own arcade term). Retiring it — chase floor 1.0, the bank-independent reading — collapses
    the nose–path gap 2.9° → 1.9° at +3 s against a measured 4.8° that GROWS, and raises the 36 s
    altitude loss 1087 → 1334 m against a measured 540. Lowering it to 0.10 moves every row toward
    the footage and still cannot reach it, while walking the knife-edge α to 5.36°, past
    `liftAOAs[0] = 5°`. ⚠ **What is left is not this constant**: the whole banked rotation runs
    ≈1.6× fast (knife-edge drift 1.09 °/s against 0.69–0.89, heading 1.68 against 0.68–1.13, the same
    ratio as `sustained-turn-rate`'s 32.80 against 18.95) and `BL-095`'s unconsumed
    `turn_fade_in`/`turn_fade_out` are the only authored fields shaped like it (`highGs` was the
    third until `D33` measured the G limiter inert on all eleven airframes). Retuning
    `KnifeAlignFloor` would hide a rotation error inside a chase constant.
    The original's knife-edge trajectory (`CAP-05`, both takes, 143/300 mph,
    agreeing to ~13%, so driven by time-since-roll-in, not airspeed):

    | time since roll-in | nose | path | sink |
    |---|---|---|---|
    | 0–3 s | −4° step, then drifting | ≈0° | **0.5 ft/sim-s — genuinely holds altitude** |
    | +12 s | −12.0° | −6.0° | 24 ft/sim-s |
    | +24 s | −20.0° | −12.8° | 60 ft/sim-s |
    | +36 s | −27.0° | −18.7° | 93 ft/sim-s, still steepening |

    The sag reads as an immediate ≈4° step followed by an **unbounded drift of 0.69–0.89 °/sim-s
    that a bounded sag cannot produce** — which is why the bounded pair is now retired rather than
    retuned. ⚠ For `KnifeAlignFloor` the observable is the path lagging the nose by 4.8° at +3 s /
    7.2° at +24 s / 8.3° at +36 s, and `CAP-05` cannot separate that from gravity pulling the path
    down over the same interval — **do not back an absolute align rate out of it.** The D31 argument
    above uses only the *direction* the number moves under an A/B on the same build, which the
    confound cannot reverse (gravity pulling the path down can only shrink the gap, so the inferred
    chase is an upper bound either way).
    *Playtest after fix:* (1) does the knife-edge sink feel like the original's; (2) does the
    stall-into-knife-edge recovery behave now that the sag term no longer compounds with the stall
    nose-drop (the two used to be gated apart from each other by an explicit `!stalled` check that
    no longer exists, because there is nothing left to gate).
  - **`ClimbGravityScale` is RETIRED — closed 2026-08-09 by `PLAN-flight-model-rewrite` D32, and it
    leaves this entry's TUNE list.** The degenerate `CAP-05` fit that this bullet warned about
    (`C` = 1.18 / 0.59 / 0.00 at `g` = 17 / 20 / 25, rms flat across the valley, so `C` ≈ 0.6 was a
    consistency and never a measurement) turned out to be flattering a constant that is not merely
    unmeasured but **wrong in sign**. Measured against the original's own sustained full-throttle
    climb (`Climp 90° 100% Thrust.mp4`, now clip key `climb90`; plateau 163.05 mph at a 56.3° path),
    the constant alone gives 276.66 mph and **removing it alone** gives 257.74 — it makes the climb
    faster than the original's, not slower. The original's climb penalty lives in THRUST, scaled by
    nose attitude, decoded and landed in the same item. ⚠ **The GDD §4.1.1 reading recorded here was
    design intent, not behaviour:** the shipped executable's gravity block
    (`0x48ff85`–`0x48ff9d`) reads no attitude at all, and the term that IS attitude-scaled runs the
    other way. Do not reintroduce a pitch-scaled gravity on the strength of the design document.
    *Still open, and it is the climb's magnitude rather than its mechanism:* the model settles 25%
    fast (204.04 against 163.05) and does not reproduce the footage's undershoot-and-recover. The
    leading candidate is the α the original held there — its clip is a 90° pull, and at a 90° nose
    with the measured 56° path the same decoded force path balances to −3.3%. `CAP-20` (a shallow,
    held climb with a **readable** ADI, which this bullet already asked for) is what would settle it;
    the 90° clip's ADI saturates above ≈+30° and cannot.

- `BL-120` `[Tuning]` `[Owed-playtest]` **Collision feel** — behaviour against building corners.

- `BL-147` `[Research]` **Pitch's transient shape. NARROWED to a ≈1.8× residual, and the named
  mechanism is now landed and measured rather than pending. Measured 2026-08-03 from `CAP-04`; the
  capture is discharged and retired. The A/B against our own build is DONE (`C23`): the original's
  1300 → 570 ms cadence roll-off is 42× against our 23.3× (was 19.6× before the weathervane), and
  the "3.5× steeper than one first-order lag permits" framing this entry was written on is
  superseded — see the landed-mechanism paragraph.**
  The item was written asking for a *moderate-deflection* pitch trace, on the assumption that a
  sub-full-deflection input exists to spin up. **It does not — the user flies the original's pitch on
  the keyboard, so every pitch command is full deflection gated on/off by the key** (confirmed by the
  user, 2026-08-03; note the numpad in the original is the *camera*, `CAP-07`/`CAP-08`, not the stick).
  A "~45° pull" is therefore a **tap cadence**, not a deflection, and there is no partial-deflection
  spin-up curve to fit.
  **What `CAP-04` actually measured** (both takes, `checkclip` `OK`, heading flat to 0.2° so the
  manoeuvre is genuinely wings-level pitch; decode noise 1.3–1.8 ft second-difference; altimeter band
  resolved 11.2× / 8.2×). Entry 299.4 / 300.4 mph level, then a tapped pull. Smoothed peak
  flight-path pitch rate **8.5 / 9.8 / 9.4 / 12.7 °/sim-s** over the four pull events = 26–30% of the
  33 °/sim-s full-deflection rate, i.e. the tap duty cycle; the instantaneous rate climbs to
  **20–24 °/sim-s** as the smoothing window shrinks, which is the individual taps showing through.
  Flight path peaked at **+33.5°** (take 1) and **+28.0°** (take 2) — the clip is *not* a 45° pull, and
  the pitch *attitude* is unreadable because the ADI ball saturates (sky fraction pinned at its 0.730
  ceiling) once the flight path passes ~+10°.
  **The step response was already in the 2026-07 loop clip**, which was flown by *holding* the key:
  from 4 s of dead-level 299.4 mph, key down at t = 4.10 s wall, the rate rises to an asymptote
  **R = 26–31 °/sim-s** (consistent with the published sustained 33) with a model-free 10–90% rise of
  **0.66 s sim**. The exponential **τ is not resolvable** — fitted τ tracks the smoothing window
  (0.73 → 0.19 s sim as the window tightens), so all the footage supports is an **upper bound
  τ ≲ 0.2 s sim**. Our model's held-stick spin-up is `1/ang_momentum_damp` = 1/5.0 = **0.2 s**
  (`FlightModel.cs`, `return_rate` adds only on release), which sits exactly at that bound.
  ⚠ **That "no mismatch is demonstrable" reading is SUPERSEDED by the cadence sweep below.** The
  bound above is only meaningful *if* the response is a single first-order lag, and the sweep shows it
  is not — so a τ derived from it describes a model the original does not obey. What survives from the
  loop clip is the asymptote R = 26–31 °/sim-s and the 0.66 s sim rise, both model-free.
  `PitchTune` 0.75 is still not implicated: it sets the steady rate, which continues to match.
  **The sluggishness is most likely the tap cadence, not the airframe.** At matched smoothing the held
  key reaches its rate in 0.66 s sim while `CAP-04`'s tapped pulls take 0.99 / 1.25 / 1.50 / 5.28 s —
  1.5× to 8× slower, and *not reproducible between takes*, which is the signature of a human hand
  rather than a flight model. Before touching any constant, check whether our key-to-input path
  ramps/filters where the original's is a bare on/off.
  **What remains open:** τ itself — but there is now a **named candidate mechanism**, which there was
  not before.

  **The weathervane mechanism is LANDED and it does not close this item — the gap is narrowed to
  ≈1.8×, and the entry's own arithmetic is corrected (`C23`, 2026-08-09).** The original applies
  `return_rate` as a **weathervane torque** along `cross(nose, velocity)` (the `−nose` this entry
  used to carry was a transcription error — the code's `−m[2]` *is* the nose) at **half** the
  misalignment angle, continuously, into the same accumulator as the stick. That is a spring-damper
  where the remake folded `return_rate` into the damping coefficient, and it is now implemented
  (`FlightModel.WeathervaneTorque`, decode in
  [`docs/org/flightModel.md`](docs/org/flightModel.md) "Weathervane centring — resolved").
  **The sweep now exists for our build too** — `CSVM.Tests/ZzCadenceSweep.cs` drives the identical
  square wave into the model and fits the ripple with the identical simultaneous cubic+sin+cos
  estimator, at both the sim and wall readings of the cadences (DET-11). Amplitude-for-amplitude, so
  the operating-point objection below does not apply and no transfer function is assumed on either
  side. **1300 → 570 ms roll-off: 19.6× before the change, 23.3× after, against the original's 42×**
  (wall reading: 20.7× → 22.4×). Right direction, about a sixth of the gap closed. Full table and
  per-cadence amplitudes: `analysis/flight-model-baseline/POST-B14.md`, "C23".
  ⚠ **The "3.5× steeper than any single first-order lag permits" figure above does NOT describe our
  build's deficit, and must stop being quoted as if it does.** That ceiling assumes the chain is
  double integration + one lag. Ours is not and never was: the flight path chases the nose through a
  **second** first-order lag (`lift_accel_rate`, τ = 1.33 s), so the pre-C23 build already rolled off
  19.6× — 1.65× past that ceiling — while `return_rate` was still pure damping. The amplitude
  comparison above is the statement of record.
  **What remains open:** the residual ≈1.8× of roll-off, with no named mechanism. Candidates not yet
  examined: the original's 0.5/s throttle slew (decoded, unimplemented — it contaminates the first
  seconds of any manoeuvre), the `liftAOAs` airflow blend's behaviour under a rapidly reversing
  demand, and the possibility that the original's 570 ms point (a 4.9× drop from 700 ms over a 1.23
  frequency ratio) is a resonance rather than a point on a smooth roll-off, which no monotone
  transfer function can produce and which the corpus cannot presently distinguish from noise at
  0.63 ± 0.13 ft.
  ⚠ `PitchTune` **did** move here, 0.75 → 0.89, and so did `YawTune`, 1.33 → 1.57 — *not* to chase
  this transient. A sustained full-stick manoeuvre holds a real misalignment (α ≈ 18° pulling), so
  the weathervane opposes the stick and dropped the **steady** rate to 28.35 °/s; the refit re-pins
  it to the same measured 33. Re-pinning a steady rate a new mechanism moved is what `*Tune` is for;
  chasing the roll-off through it remains forbidden and is still not the knob.
  **How the original's own footage was got — a fixed-cadence key macro. 30 fps is a floor, not a target; record at whatever
  rate the recorder gives and keep the bitrate high.** A single step
  edge is ~4 frames and unresolvable, but a *periodic* input is not: drive the pitch keys as a square
  wave and τ shows up as the **ripple amplitude** at a known frequency, which averages down over
  hundreds of cycles instead of living or dying on one edge. Alternate pitch-**up** and pitch-**down**
  (not a single key) so the mean rate is zero — the aircraft porpoises about level, speed and the aero
  gain stay put, altitude stays in one band and the ADI never saturates.
  Modelled ripple at τ = 0.2 s sim, V = 440 ft/s, against the 1.75 ft second-difference noise
  (conservative: that statistic implies only ~0.7 ft of independent per-frame noise):

  | period (wall) | frames/cycle | alt ripple | ripple at τ = 0.1 / 0.2 / 0.3 |
  |---|---|---|---|
  | 0.25 s | 7.5 | 0.57 ft | 1.06 / 0.57 / 0.39 |
  | 0.40 s | 12 | 2.25 ft | 3.75 / 2.25 / 1.56 |
  | 0.60 s | 18 | 6.99 ft | 10.22 / 6.99 / 5.06 |
  | 1.00 s | 30 | 26.3 ft | 32.5 / 26.3 / 20.9 |

  **FLOWN 2026-08-03 — seven clips, and the result is a refutation, not a τ.** Six alternating cadences
  (1300 / 930 / 700 / 570 / 370 / 230 ms) plus a 230 ms duty control. All gate `checkclip` **OK** and
  are the cleanest footage in the corpus (dial translation literally 0 px on the six; second-difference
  noise 0.59–2.64 ft). Cadence logs measured **1300.0 / 930.0 / 700.0 / 570.0 / 370.0 / 230.0 ms** with
  jitter sd 0.002–0.535 ms, so the input is known, not assumed.

  | period | f₀ | ripple amplitude | operating point |
  |---|---|---|---|
  | 1300 ms | 0.769 Hz | **26.31 ± 2.37 ft** | 246 mph mean |
  | 930 ms | 1.075 Hz | **7.07 ± 0.21 ft** | 206 mph |
  | 700 ms | 1.429 Hz | **3.09 ± 0.09 ft** | 259 mph |
  | 570 ms | 1.754 Hz | **0.63 ± 0.13 ft** | 213 mph |
  | 370 ms | 2.703 Hz | 0.065 — at the floor | 221 mph |
  | 230 ms | 4.348 Hz | 0.037 — at the floor | 227 mph |

  **The headline: from 1300 → 570 ms the response falls 42×, over a frequency ratio of only 2.28.**
  Double integration (rate → attitude → altitude) accounts for 5.2× of that. A single first-order lag
  can add at most another 2.28× — that is the τ → ∞ limit, not a fit. So the steepest possible
  one-lag model gives 12×, and the original delivers **42×: 3.5× more roll-off than any single
  first-order lag permits.** That margin is far outside the ±20% amplitude systematics and the ±12%
  spread in mean airspeed. **Our rotation model is exactly one first-order lag**
  (`BodyRates += (cmd - BodyRates*damp)*dt`), so on this axis the original is not that shape.
  **The duty control settles the alternative explanation.** 50% duty on the pull key alone (measured
  duty fraction 0.507 from the log, mean press 116.6 ms against a nominal 115) produced a large
  sustained pull — the aircraft climbed 1775 → 4892 ft and went over the top, speed bleeding to
  109 mph. So **115 ms presses unquestionably reach the game**, and the 370/230 ms nulls are the
  aircraft's own roll-off, not dropped input. The sliding-window amplitude plot shows this directly:
  the four detections hold a flat amplitude across the whole clip while 370/230 sit in the noise
  throughout — the cadence ran in every clip.
  ⚠ **This is still not a fitted τ, and must not be quoted as one.** The clips are not at a common
  operating point: mean airspeed runs 206–259 mph and the ripple itself spans 0.6–26 ft, so the low
  frequencies are a large-amplitude manoeuvre and the high ones a small perturbation. A transfer
  function fitted across that mixes regimes.
  ⚠ **Trap, and it cost a wrong figure before it was caught.** High-passing altitude with a sliding
  quadratic *before* fitting the sinusoid has real gain at f₀ — the 930 ms amplitude moved 4.87 → 6.85
  → 7.07 ft as the span changed. The correct estimator fits **polynomial + sin + cos simultaneously**
  inside a window of ≥8 periods, where a cubic can absorb almost none of the fundamental; that is
  stable to 3% on the strong clips. Never detrend and then fit.
  **DONE (C23, 2026-08-09) — the identical cadences now run through our build**, as
  `CSVM.Tests/ZzCadenceSweep.cs` driving the model directly rather than `--hold` driving a session
  (engine-free, so it is deterministic by construction and needs no clip decode). Same input, same
  estimator, same operating points, no transfer function assumed — results in the landed-mechanism
  paragraph above. Plot of the original's side: `playtest/CAP-04/cadence_response.png`.

  The driver is `analysis/video-flight-calibration/pitch_cadence.ahk` (AutoHotkey v2) — its header
  carries the rig rationale, the re-flight rules (non-integer periods, the busy-spin timing) and
  the verified timing figures; the edge log it writes (`cadence-logs/*.csv`) is the deliverable as
  much as the video is. ⚠ For a re-flight: `extract.py`'s `LAYOUTS` knows only 2560×720 and
  2560×1440 and raises on anything else — declare a new geometry *before* recording. The rig's
  beeps are in none of the clips (Game Bar records game audio only); find the cadence window by
  matched filter on altitude, which is what the analysis does.
  ⚠ **Traps.** (a) `PitchTune` **is a pinned measurement** (0.89 since `C23`; see the
  flight-constants standing note at the top of this file) — and the cadence sweep leaves it
  untouched: it sets the **steady** rate, which still matches, while what the sweep is about is the
  *transient shape*. It cannot move to fix a feel report — and it is emphatically not the knob for
  the roll-off mismatch. (Its `C23` move was in the other direction entirely: a decoded torque
  changed the steady rate, and the constant re-pinned it to the same measurement.) (b) **Do not quote a τ from `CAP-04`, from the
  loop clip, or from the cadence sweep.** The loop clip's fitted τ is smoothing-limited and falls
  monotonically as the window tightens (`FINDINGS.md`'s "a peak found by differentiating a smoothed
  signal is a smoothing artifact", in its exact form); the sweep's points are not at one operating
  point. The sweep refutes a *shape*; it does not fit a constant. (c) Don't fold this into `BL-097` — same shape of gap,
  different axis, and pitch's own coupling to speed (induced drag, modelled in `PLAN-flight-drag-lift`
  C21) makes conflating the two easy to get wrong. (d) The digital-input finding is not pitch-specific — it means **`BL-097`'s
  roll question has the same defect**: there is no partial aileron deflection either, so a "moderate
  roll input" clip cannot be flown, and `BL-097` should be re-read as a held-key step question too.

- `BL-172` `[Feature]` **Graze pushback is entirely unmodelled — and the shipped data already has the constant to
  bind it. Plan-sized — not a TUNE.** `FlightController.SurviveHit` (`FlightController.cs:1435-1535`)
  only ever does a friction-scaled tangential slide (`GrazeFriction`), a lever-arm attitude kick
  (`GrazeKick`), and a fixed `GrazePushOut` (0.15 m) off the surface — there is no
  restitution/repulsion term along the normal at all. Meanwhile `player.json`'s `crash` block ships
  **`bounce_factor 0.6`** alongside `armor_damage_range [50,300]` / `health_damage_range [50,300]`
  (`docs/formats/vehicle.md:65,90-149`), and nothing in `CSVM/src` reads any of the three (grep
  confirms zero hits for `bounce_factor` under `CSVM/src`; `BL-095` has the block's decode status).
  *Fix shape:* add a restitution impulse along the contact normal scaled by `bounce_factor`, alongside
  the existing tangential slide — this turns "invent a pushback mechanic" into "bind the shipped
  constant." *Blocks:* the collision-feel sign-off.

  **DECODED 2026-08-14 from `crimson.exe`; units settled and the fix shape confirmed.**
  `bounce_factor` is a **raw scalar** (global `0x0071c35c`, parser store `0x00473c38`, fallback 0.8),
  applied in the collision resolver `FUN_0048d7f0` as a normal-only impulse with no tangential or
  friction term. Write-up in [`docs/org/flightModel.md`](docs/org/flightModel.md), "Collision response
  and `bounce_factor`". Three things it changes here:
  - **Effective normal restitution is `f_lin × bounce_factor`, not `bounce_factor`**, where
    `f_lin = L/(L+A)` splits the impact between linear rebound and spin (`L = 2.25·|J|`,
    `A = |Δω|`). A short lever arm rebounds at up to 0.6; a wingtip or nose into a wall throws
    almost everything into rotation instead.
  - **There is no surface dependence in the code at all** — no verticality test, no per-surface
    table, no material lookup. The measured vertical-versus-flat split below is a lever-arm
    partition, and reading it as a per-surface coefficient would be wrong.
  - **Only the player bounces.** The impulse branch is entered only for the local player and only
    while not already crashed; AI aircraft get position correction and nothing else.
  ⚠ **The measured 0.75–0.86 on flat ground is above what `bounce_factor` can produce**, and the
  decode found why: the impulse is computed from the *contact point's* velocity with the rotational
  term **doubled**, then applied in full to the centre of mass with no reaction term
  (`n·v_after = −k·(n·v) − (1+k)·2·n·(ω × r)`, `k = f_lin·bounce_factor`). The second term is
  unbounded and is not restitution. Reproducing the original's feel needs that term, not a larger
  `bounce_factor`. Ruled out as sources, each traced: multiple contacts per frame, successive-frame
  stacking, a separate ground-support path, and gravity ordering.
  ⚠ **`bounce_factor` 0.6 IS a restitution along the contact normal — measured 2026-08-04 from
  `CAP-14`** (eight clips, `playtest/CAP-14/`; two airframes, Bloodhawk and a max-armour Balmoral;
  **seven** contacts, three surface orientations, 139–302 mph; every clip's altimeter and speedometer
  registering to the chase pooled median at dx = dy = 0, peaks 0.82–0.97; altitude d2 sd 0.34–0.84 ft).
  `v0` is the vertical speed at the contact instant from a free parabola over the N frames after it,
  `e = −v0/v_before`:

  | surface | normal | mph | v_before | v0 (N=9) | v0 (N=12) | accel | e |
  |---|---|---|---|---|---|---|---|
  | cliff face | **vertical** | 216 | −45.9 | +8.0 | +3.0 | −86 | **0.06–0.18** |
  | building wall | **vertical** | 145 | −21.7 | +1.4 | +1.8 | −107 | **0.06–0.08** |
  | flat, wingtip #1 | **flat** | 143 | −14.2 | +10.6 | +12.3 | −66 | **0.75–0.86** |
  | flat, belly (slide) | **flat** | 302 | −44.9 | +17.5 | +21.9 | −73 | **0.39–0.49** |
  | flat, wingtip #2 | flat | 139 | −5.4 | +1.7 | +0.4 | +8 | pull-up |
  | flat, nose | flat | 146 | −31.9 | −23.6 | −15.1 | +342 | pull-up |
  | flat, belly | flat | 302 | −39.7 | +80.3 | +83.4 | +88 | pull-up |

  (ft/wall-s; accel ft/wall-s².) **Vertical surfaces e = 0.10 ± 0.05; flat ground e = 0.62 ± 0.19,
  against a shipped 0.60.** That split is the signature of a normal-direction restitution and nothing
  else gives it: on a vertical wall the sink is *tangential*, so a normal bounce puts nothing into the
  altimeter — and the altimeter sees nothing; on flat ground the sink *is* the normal component, and
  it comes back at 0.6 of itself. So this item's original fix shape is **confirmed, not overturned**.
  **Speed loss is set by incidence, not speed** — 302 mph belly-flat costs **0.11 mph**; 216 mph along
  a cliff costs 11.64 in one frame; 145 mph along a building wall costs 23.15 in the contact frame and
  then keeps scraping to **−40% (144.5 → 86.7 mph over 0.47 s)**, the only multi-frame contact in the
  set — so an oblique wall scrape is a sustained several-tick event, not an impulse.
  **Buildings behave like terrain, and survival is geometry not speed:** the Balmoral grazed a C5
  skyscraper at 144.5 mph and flew on, and died against one at 144.2 mph; a Bloodhawk survived flat
  ground at 302 mph twice, once holding altitude within 2.6 ft for 0.40 s while sparking.
  *Fix shape, confirmed and sharpened:* add the restitution impulse along the contact normal scaled by
  `bounce_factor`, replacing the fixed 0.15 m `GrazePushOut`; leave tangential speed almost untouched
  for a flat skim; and make an oblique scrape a *sustained multi-tick* drag rather than a single
  impulse.
  *Playtest after fix:* grazes vs crashes should feel fair against the original, including behaviour
  against building corners (`CAP-14`).
  ⚠ **Traps.** (a) `bounce_factor`'s units are settled (raw scalar, decode above); what remains
  unverified is the footage. `CAP-14` supports reading it as a coefficient of restitution on the
  contact normal, but **0.6 is *consistent with* that footage, not
  measured from it.** Only two of five flat-ground contacts are readable at all; of those, the belly
  slide's `v0` still walks with the fit window (+3.2/+11.8/+17.5/+21.9 at N = 5/7/9/12, so e is really
  0.07–0.49) and only wingtip #1 is window-stable (+10.6…+12.3 over N = 7–15, residual 0.07–0.09 ft
  against 0.20 ft noise) — and it reads **above** 0.6, at 0.75–0.86. Two contacts bracketing 0.6 is
  agreement, not a measurement; do not quote ±0.19 as a precision.
  ⚠ (a2) **A post-contact climb is not evidence of a bounce; the SIGN of the post-contact acceleration
  is the discriminator.** In `CAP-14 Bloodhawk  Hard Graze.mp4` the climb rate keeps *growing* for a
  second (accel **+88 to +228** ft/wall-s², upward, nose visibly rising in the stills) and reads as
  e = 2.0 if fitted as restitution — impossible. A real rebound decays at −127 ft/wall-s² under
  `nom_gravity` 20. Read `e` only where the fitted acceleration is negative.
  ⚠ (a3) **Restitution alone will not reproduce the vertical-surface clips.** On *both* of them the
  sink is killed as well (−45.9 → +8.0, −21.7 → +1.4) even though on a vertical wall the sink is
  tangential — while the flat-ground contacts show tangential speed almost perfectly preserved
  (302 mph belly-flat costs 0.11 mph). Something removes vertical speed on contact regardless of the
  surface's orientation, on top of the normal-direction bounce, and it is unexplained. (b) The receiving side is no longer the
  blocker: `PLAN-armour-layer.md` (`docs/plans/`, complete 2026-08-04, `BL-173` closed as part of it)
  landed the two-pool `PlaneDamage.Apply(part, healthDamage, armorDamage)`, armour first with 1:1
  overflow — but it deliberately left the `crash` block itself (`armor_damage_range`/
  `health_damage_range`/`bounce_factor`) unconsumed on every axis (Decision 4), so this item still
  owns binding grazes/crashes through that `Apply` overload alongside the pushback, as one coupled
  change. (c) `GrazeStopSpeed`/`GrazeFriction`/`GrazeKick` were tuned against the *current* no-bounce
  slide — expect them to need re-tuning once a normal-direction impulse is added, not to survive
  unchanged.

- `BL-271` `[Tuning]` `[Owed-playtest]` **The survivable-graze and stop laws are invented physics with player-facing
  consequences** (`FlightController.cs:271-295,1652-1672`, header "all TUNE"): attitude kick
  `GrazeKick` 1.2 rad/s, `GrazeFriction` 0.35, quadratic severity damage, "sliding below
  `GrazeStopSpeed` 12 m/s = destroyed", "3 failed embed push-outs = explode". The original
  might throw the nose differently or let a plane belly-slide to a stop ("collecting 0-dmg
  kisses" is the user report that motivated the stop rule). `CAP-14`'s analysed graze
  (2026-08-04) already bounds part of this — the original's graze cost ~5% speed + sink with
  wings level, no visible attitude kick at that severity. Judge the kick and the stop rule
  against that footage and `BL-120`'s corner feel item before tuning further.

- `BL-309` `[Feature]` **Engine torque is a designed, one-sided turn assist — unmodelled.** GDD §4.1.8
  ("Engine Torque", Motion Model/Flight Dynamics → Simulated Elements; restated, no prose): torque
  is simulated selectively so the player never fights it — no effect in straight-and-level flight,
  no effect turning *against* the torque direction, but turning *with* it is **faster**. The
  behaviour to look for is a directional asymmetry in roll/turn rate that only ever assists.
  Nothing in `CSVM/src` models it, and no shipped key is known to carry it — the `dynamics` block's
  `pitch_torque`/`roll_torque`/`rudder_torque` are control torques, not this. A hint already on
  film: `CAP-02 Run 5`'s banked 45° right peaked at 123.8 °/s against left's 96.0 — but that pair
  was flown for heading calibration, the right take decodes badly outside heading, and the
  sustained rates point the other way; a lead to re-measure, not evidence. ⚠ The suite asserts
  direction-blind rates measured from single-direction captures (yaw 360° to 4%, roll time to 3%) —
  if the original's assist is real, those measured rates may already *contain* it for whichever
  direction was flown. Establish the flown directions before touching any constant.
  *Needs:* an original A/B — a full roll and a full rudder 360° in **both** directions at matched
  speed (a `CAP` when scheduled).

- `BL-310` `[Feature]` **Pitch-down on aileron roll is designed — unmodelled, and measurable from footage
  we already hold.** GDD §4.1.5: a roll carries a "small but noticeable" nose-over effect. We model
  no roll→pitch coupling. Before inventing a constant, measure it: the decoded 360° aileron-roll
  capture (manoeuvre #4, `analysis/video-flight-calibration/`) should show the nose-over as an
  altitude/ADI dip across the roll — if it cannot be read there, it is too small to model and this
  closes as won't-do. ⚠ Distinct from `BL-097` (the roll's spin-up *rate* shape); this is the
  cross-axis coupling.

- `BL-311` `[Feature]` **Ambient turbulence is designed and absent.** GDD §4.1.10: subtle, random jostling
  of the player's plane to sell moving through air — explicitly zero effect on speed, heading or
  performance. Visual-only is exactly the contract of the `PlaneShake` oscillators
  (`src/Flight/PlaneShake.cs`, visual-only roll on `ShakePivot`), so it slots in as one more
  source. Check `shakes.zrd.json`/`docs/formats/shakes.md` for an ambient source before inventing
  one; if no data carries it, magnitude and cadence are a TUNE against feel. Low priority; pairs
  with `BL-266`'s open shake data questions.

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

- `BL-305` `[Bug]` **C5's city blocks are packed edge to edge where the original shows pavement
  between buildings — within-block clutter density/alignment is wrong.** Found by `CAP-22`
  (2026-08-07) while closing `BL-250` (the doubled-district bug, landed the same day — evidence
  in that closing commit, `git log --grep=BL-250`, and `playtest/CAP-22/` while it lives): at
  scale-matched nadir (`playtest/CAP-22/ours-nadir-230-scale-matched.png` vs
  `orig-c-t4-nadir-crossroads.png`, matched by eye on avenue width, ±20 %) the original's blocks
  show pavement between neighbouring buildings, while ours have whole regions of no visible
  ground — and that persisted with the buried `cblock4/5/6` district already suppressed, so it is
  **not** the doubling: it is the placement of the surviving `cblock1/2/3`+`cblock7` districts
  themselves. Prime suspect: `ClutterBuilder` tiles each template on a fixed world-space X/Z grid
  of its authored period rather than reproducing the original's (undecoded) alignment
  (`Clutter.cs`'s class comment records the decision and why), which can double-stamp a
  template's cell pattern relative to the painted street layout the ground texture shows.
  A footprint-vs-texture matching attempt was already made and was inconclusive without the
  ground quad's UV-to-world orientation verified first —
  `analysis/bl-058-clutter-doubling/FINDINGS.md` (the `match_footprints.py` paragraph) has the
  dead end so it is not re-walked.
  ⚠ **Traps.** (a) Do not re-open the district question: the original's downtown is the
  `cblock1/2/3` city and `cblock4/5/6` stays exempted (`Clutter.cs`
  `BuriedClutterDistricts`, closing commit of `BL-250`). (b) The scale match behind the founding
  observation is by eye, ±20 % — re-shoot with a decoded altitude before tuning to it.
  (c) `cblock7` places `cb12a`/`13a`/`14a` (models in the exempted district's name range) — a
  density census that lumps by name range will mis-attribute exactly the way `BL-250`'s first
  census did; count per template root.

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
  outright). Start from a located landmark pose the way `BL-305` was finally confirmed (the user's
  own flyover, not a nadir — `SHOT-28`, `docs/verification.md`), not from `CAP-22`'s pose, which
  cannot resolve this question (it already reads correctly). *⚠ Traps:* (a) do not re-curate a
  list as a stopgap — that is exactly the mistake this item exists to not repeat. (b) A nadir
  shot cannot distinguish a painted rooftop from bare ground any better than it could distinguish
  a rooftop from a building (`SHOT-28`); use a low oblique. *Cross-refs:* `BL-305` (the fix that
  surfaced this), `BL-250` (the closed item this supersedes — do not reopen that ID; IDs are never
  reused, per this file's own rule).

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

- `BL-032` `[Feature]` `[Blocked: user decision]` **Burning-object fires (the four `fire.zrd`
  behaviours)** — **narrowed 2026-08-13.** The *ambient* half of this item is DONE: the always-on
  refinery flame now runs, and the mechanism was re-decoded out of `crimson.exe`. What is still
  blocked is only *when a damaged object catches fire*, which remains a decision, not a data gap.
  Decode in `docs/formats/anim-definitions.md` ("Fire: a texture cycle on a material, and
  behaviours nothing calls"):
  - **What landed 2026-08-13.** An EFFECTS flipbook is state on the **material**, not on the node
    that names it: `zeff_ini.c` (`FUN_00523ac0`) resolves the node, walks to its first mesh, and
    installs the frame list on surface 0's material record; the polygon draw loop (`FUN_005524d0`)
    tests the material's own cycled bit and advances it once per frame (`FUN_0055b1a0`). Materials
    are one record per texture, so material 88 is shared by `fire1`, `flame01` (the refinery vent),
    `mb1` and `mb_spinflame`, and all four animate together. `fire1.flt` is a proxy node exactly
    like the interp's `watersetup`/`surfsetup`. `EffectCycles` applies both entries to their gamez
    materials before the world build, and `SceneBuilder` registers them with the existing
    `TextureCycler`; the billboard material paths had to register cycles too, since every fire mesh
    is `Facade`/`CylindricalY`. C1 reports `fire1.flt→mat88 fire101.tif×12@10`. Material 133
    (`fire2`) installs but never registers, because `fire2` is its only user and the world never
    builds it, which is the decode's own prediction.
  - **Correction to the 2026-07-21 reading.** "EFFECTS is node-keyed" was wrong, and with it the
    conclusions that `flame01` is a static base flame and that the animated fire at the refinery is
    a placed `fire2` instance. The muzzle-flash observation behind it (a sustained flash never
    leaving `fire101`) is contradicted by the draw loop; `mb_spinflame` is a small spinning sprite
    and the twelve `fire1NN` frames are variations of one shape, so it was never a strong read.
    **A clean A/B is still owed:** watch the refinery vent in the original and confirm the frames
    roll, since it is a still object where they should be plainly visible.
  - **Why the rest is blocked:** the four `fire.zrd.json` behaviours (`timed_big_fire`,
    `persistent_big_fire`, `persistent_small_fire`, `timed_small_fire`, all anchored on
    `fire2.flt`) are called by **nothing**, their names appear in exactly one file, their own,
    and `CALL_ANIMATION` references animations by name string only (no index form exists anywhere
    in this data). **User searched the disassembly 2026-07-21 and found no trigger**, and the
    2026-08-13 sweep found none either: `CATCHES_FIRE` is a real ZWEP weapon key (`zwep_ini.c`,
    flag bit 13) that **no weapon in this install sets**. So the original starts them engine-side
    by a condition we cannot recover; reproducing them means inventing our own trigger, which is a
    fidelity guess rather than a data-driven port.
  - **If resumed:** the placement half already works, `CALL_ANIMATION`'s target parameter landed
    2026-07-21 and is the mechanism that puts a template at a site. Build the template pool first.

- `BL-034` `[Bug]` **`SpinMotion` re-seeds its rest pose from an already-spun pose (found 2026-07-22, deliberately
  not fixed).** `SpinMotion` captures `_rest = target.Transform.Basis` from the CURRENT pose at
  construction, and the idempotence guard in `Dispatch` matches only on identical
  `(rate, runTime)`. `zeppelin_rocksleft` fires five events with five different rate/runtime pairs
  at the same `rock_zeppelin`, so each replacement motion anchors to wherever the previous one
  left the node, and a looping call drifts. It is **bounded** — rotation is orthonormal, so this
  can never produce the 1e27 blowup it was originally suspected of (that was the unread
  `spline_interp` flag, fixed 2026-07-22 — see `docs/HISTORY.md`) — but the drift is real.
  **Not fixed because both candidate fixes risk a visible regression to cure an invisible one,
  and the data does not adjudicate:** (a) seeding from `RestOf` would discard a deliberately-posed
  starting orientation on all 590 spins in the install — C1/M05's `random_prop` poses `propstill`
  to a random angle *before* spinning it, and that pattern would break; (b) inheriting the
  previous motion's `_rest` assumes the five rock events oscillate about a fixed pose, but a
  chained eased rock (accelerate, decelerate, reverse) is at least as plausible a reading, and
  under (b) each event would snap back to rest. **Needs the original game**: watch a zeppelin rock
  through several loops and see whether it returns to the same attitude or walks. Same class of
  call as `MissionSetup`'s `Object3DRotate` angle unit, resolved 2026-08-06 by a per-script
  magnitude heuristic rather than a global guess (`BL-249`, `docs/plans/PLAN-m3-polish-10.md` C22) — do
  not resolve this differently.

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

- `BL-135` `[Bug]` `[Blocked: original-engine evidence]` **The one-frame `CallSequence` dispatch lag.**
  **Found 2026-07-22 while fixing C1's police siren** (that fix landed; see `docs/HISTORY.md`). This
  is the *other* defect that investigation turned up — real, engine-wide, and deliberately left
  unfixed because it was not what silenced anything.

  **⚠ Re-measured and RE-DEFERRED 2026-08-04** (`PLAN-m3-polish-6` B12). The bounded drain was
  built, measured across the whole install and then taken back out. Read
  [`analysis/bl-135-callsequence-lag/FINDINGS.md`](analysis/bl-135-callsequence-lag/FINDINGS.md)
  before touching this again — the implementation, the sized bound and the full measurement are
  there, and the "behaviour-neutral" claim below is superseded. Summary of what changed:

  - **It is no longer behaviour-neutral.** 4 of the 13 goldens move, deterministically:
    `c1-crash` 79.7 % of pixels, `c1-destroy-effects` 0.228 %, `c3-island` 0.029 %,
    `c5-city-night` 0.015 %. All four are particle shots and the difference is phase — same camera,
    same terrain, the effect one tick further along (C5 877 → 927 live particles at frame 120).
    7 of the 8 `--freecam --det` chapter captures stay pixel-identical.
  - **Nothing observable is repaired.** No content appears or disappears; every runtime total is
    unchanged. What moves is the bootstrap CENSUS (C1 35 → 53 lights, C5 9 → 37 puffer emitters),
    which is trap 3 below — those objects already existed one tick later.
  - **The bound is sized, not guessed.** The deepest same-tick CALL fan-out authored anywhere is
    **15** (every zeppelin's `main_altitude_check` → `rotatezep` → `breakupzep` → 13 `break*`
    pieces), so the cap was 64. It is load-bearing: C2/M02's `marypickford` really does ring
    (`randomloop → mpickford_bob → randomloop`), and it is instantaneous in THIS engine because
    `OBJECT_MOTION_SI_SCRIPT_ALL_NAMES` has no handler and reports duration 0.
  - **The blocking question is not capture-answerable**, so no `CAP-nn` was minted: the difference is
    one tick per hop over chains at most 3 hops deep (≈50 ms) off a trigger that is not on screen.

  **The mechanism.** `AnimInstance.CallSequence` appends to `Runners` (`SequenceRunner.cs:84`) while
  `AnimInstance.Advance` walks that list **descending** (`SequenceRunner.cs:67`). An appended runner
  therefore lands at an index the loop has already passed, so **every called sequence's first event
  fires one frame late** — not just the siren's. Scale: 22,391 compiled `CallSequence` events across
  3,434 defs, plus 1,865 in the reader files.

  **Why it was not fixed with the siren.** Draining same-pass-appended runners was implemented and
  measured behaviour-neutral at the time (exactly one number moved across all 8 chapters) — a claim
  the 2026-08-04 re-measurement above **supersedes**. But it repairs
  the siren only because the sound loader *happens* to still be alive at that instant, and leaves
  the other 947 late `SOUND_NODE` events broken. The loader lifetime was the real defect and is
  fixed; this lag is a separate question about dispatch timing fidelity.

  **⚠ Traps — read before touching this.**

  1. **The descending walk is deliberate, not a bug.** `AnimRuntime.cs:250` records why: instances
     can be added *during* the walk. Do not "fix" it by iterating forwards.
  2. **Any same-pass drain needs a bound.** A sequence that calls itself would spin within a single
     frame. (The siren's own `siren_police` is safe — 3 zero-delay events, no `Loop`, so its runner
     completes and is removed in one pass — but that is a property of that data, not a guarantee.)
     Confirmed 2026-08-04: `marypickford` is the def that proves it, and 64 is the sized cap.
  3. **Do not measure this with the bootstrap emitter census.** `anim: N ambient sound emitter(s)`
     is printed inside `Bootstrap`, so it is a snapshot that cannot see anything created afterwards
     — which is exactly how the siren's real cause stayed hidden through a full investigation
     (`docs/verification.md` LOG-2). C1 legitimately reports 38 while 39 emitters exist.

  **Open question this should answer:** does the original dispatch a called sequence in the same
  tick? If yes, every `CallSequence` in the install is currently a frame late and the fix is a
  fidelity improvement rather than a no-op. Nobody has checked, and as of 2026-08-04 nobody can from
  film — the difference is below a capture's resolution (see the re-deferral note above). Until it is
  settled from the original's code, the drain buys a golden rebaseline for an unverified direction,
  which is why it is not landed. Measured behaviour movement says only that *our* observable output
  changes; it is still not evidence about the original.

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

- `BL-293` `[Tuning]` **Rocket impact rings: orient the ground rings to the struck surface normal; the
  fixed-axis upper ring is faithful but reads poorly — parked** (PT-35, 2026-08-06). The
  actionable half: ALL ground rings — HE's ground ring, AP's cracks quad, the sonic stack —
  orient to the struck surface normal. Polish framing, no observed defect: on flat C1 terrain
  the rule changes nothing; the payoff is slopes and water. The parked half — faithfulness vs
  feels-good, decide later: rewatching the original (`Crimson Skies 1.02 2026-07-31 23-27-53.mp4`)
  shows the second (upper) HE ring always oriented on the same fixed axis, matching our
  behaviour — ours is CORRECT as-is and this is not a bug. The proposal on the table for the
  feel side: upper ring facing the plane / against the rocket's flight direction. The ring anims
  carry no rotation data (scale/opacity only — `docs/formats/weapon-effects.md`), so any change
  is engine-side and a deliberate deviation. Cross-link: `BL-292` (crash-splash orientation,
  different spawn path; scheduled in `docs/plans/PLAN-m3-polish-10.md` A3).

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
  session's 31 trips), `BL-231` (the pool-size tuning item
  this is explicitly NOT — a size increase would not touch this cost), `docs/verification.md`
  PERF-14.

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

- `BL-090` `[Feature]` **Small per-impact feedback gaps — items 1–4 landed (`docs/HISTORY.md`); one remains,
  with the data already shipped.**
  5. **`snd_dangerzone_camera` is a data-orphan with a ready trigger.** `dangerzone_camera.wav`,
     SFX, non-3D; in no `SOUND_GROUPS` entry and named by no world data. `StuntMission.Complete` is
     the obvious hook. ⚠ Confirm against the original that it is the zone-cleared cue and not a
     replay-camera sting — the name argues for the latter.
  ⚠ **Dropped from this group after checking — the "fireball leads the crash explosion by 0.5 s"
  claim does not survive the data.** In `player-player_crash_dirt.json` the `Sound snd_exp_ground_a`
  event is authored **before** the `large_fireball` calls (which cascade at +0, +0.25, +0.25,
  +0.25), and `large_fireball` carries no sound of its own. A lead in `FlightAudio.OnCrash` is a
  spec claim the shipped choreography contradicts — do not add one.

- `BL-109` `[Bug]` **Engine pitch behavior in dives**: the original's engine drops ~12% through a dive and
  overshoots ~1.05 at pull-out — not reproducible by the throttle-only pitch curve (cap 1.0).
  **Playtest 2026-07-30 narrows this**: the user confirms the original's note modulates with **climb
  rate and elevator input**, not throttle alone, and reads noticeably less constant than ours. Camera
  Doppler is ruled out as the mechanism — own-ship engine audio is a plain `AudioStreamPlayer`
  (`FlightAudio.cs`), non-positional by design, so no Doppler shift applies to it regardless of
  whether `AudioStreamPlayer3D.DopplerTracking` is ever enabled elsewhere — and `CAP-09` has since
  measured that the original applies **no Doppler to any emitter**, so `DopplerTracking` stays
  `DISABLED` on purpose (`docs/HISTORY.md` 2026-08-04). Left
  standing: a speed/RPM term, or wiring climb-rate/elevator directly into `EnginePitch.Eval`'s input
  instead of throttle. **`CAP-10` measured 2026-08-04 over eleven takes — both halves of the claim
  are now confirmed in direction and roughly halved in magnitude, and the note needs TWO terms.**
  Audio: the engine is a looped
  sample, so its spectrum translates rigidly in log-frequency, and cross-correlating each frame's
  whitened log-spectrum against level flight gives the playback-rate multiplier without an f0
  estimate (`analysis/engine-note/scale.py`). Flight state: both external takes decode through the
  existing chase-HUD calibration (`analysis/video-flight-calibration`, altimeter fit NCC 0.9900),
  paired to the audio on PTS by `enginepitch.py`.

  **Climb rate is ruled out, and so is every other state variable the gauges carry.** The user's
  2026-08-04 re-record (three takes, built to break the collinearity a plain dive has) collapses the
  correlation that the first two takes appeared to show:

  | take | what it varies | climb | speed | γ | dHe |
  |---|---|---|---|---|---|
  | `CAP-10 3 3rd Person.mp4` | plain dive | 0.949 | 0.746 | 0.942 | 0.384 |
  | `Bloodhawk Dive Sound.mp4` (t<16 s) | plain dive | 0.576 | 0.335 | 0.611 | 0.007 |
  | `…Dive 100% Thrust variable climb rate.mp4` | **climb rate** | **0.188** | 0.130 | 0.191 | 0.056 |
  | `…90° Banked Pith Up Down.mp4` | **elevator at γ≈0** | **0.159** | 0.206 | 0.120 | 0.081 |
  | `…Variable Climp Pitch up.mp4` | climb + pull | 0.442 | 0.237 | 0.416 | 0.055 |

  In a plain dive climb rate, γ, airspeed and elevator all move together, so the first two takes'
  R² ≈ 0.95 was **collinearity, not causation** — the clip flown specifically to vary climb rate
  scores 0.188 against it. The 90°-banked take is the discriminator: at 90° of bank the elevator
  swings the nose in azimuth, so it holds **altitude to 168 ft over 15 s (γ −5.7°..+0.7°)** while
  the note still swings **6.2%** (peak ×1.0608 at t=6.4 s). Energy-height rate `dHe` — the
  throttle-pinned proxy for how hard the airframe is being worked — does no better (≤0.384).

  **What is left is the elevator input itself, and the evidence for it is positive, not just
  residual.** `…Variable Climp Pitch up.mp4` drives the note *up* to **×1.1126** under pull (the
  first take to show a rise, the earlier dives only showing drops), and in the banked take the note
  **stays elevated at ×1.029–1.035 for as long as the stick is held** at near-zero climb rate, rather
  than decaying like a rate would. That is the user's "strain on the engine" reading. ⚠ An earlier
  version of this entry added "and it matches the sign throughout: pull → note rises, push → note
  falls"; the dive-recovery take below **disproves the second half** — a pushover raises the note by
  +3.4%, the same direction as a pull. The transient is unsigned, which is also why F13's rig, which
  alternated nose-up and nose-down within every step, read a *monotone* staircase in duty.

  **Confirmed by a scripted run, 2026-08-04 — the note follows the elevator input, and climb rate is
  dead.** `capture-rigs/ElevatorDutySweep.ahk` (F13) stepped the input duty cycle 0→1→0 in nine 6 s
  steps with every key edge logged, so the elevator is *known*. `dutysweep.py` aligns the log to the
  video and reads a clean monotone staircase:

  | duty | 0.00 | 0.25 | 0.50 | 0.75 | 1.00 | 0.75 | 0.50 | 0.25 | 0.00 |
  |---|---|---|---|---|---|---|---|---|---|
  | note | 1.0000 | 1.0024 | 1.0058 | 1.0108 | **1.0169** | 1.0132 | 1.0074 | 1.0043 | 1.0025 |
  | climb ft/min | −23 | +96 | +366 | +600 | +1273 | **+1679** | +1092 | +911 | +874 |

  `note ≈ 1.0002 + 0.0154·duty`, **R² 0.931**. Across the nine steps the note tracks **duty (0.931)**
  far better than climb rate (0.543) or airspeed (0.719) — and the descending leg dissociates them
  outright: at the run's **highest** climb rate (+1679 ft/min) the note is *lower* (1.0132) than at
  duty 1.00 where climb was only +1273 (1.0169), and by the final duty-0 step the note is back to
  1.0025 while the aircraft is **still climbing at +874 ft/min**. Stick stops moving → note returns
  to baseline regardless of climb rate. That is the controlled version of the result, and it kills
  climb rate as a candidate rather than merely out-scoring it.

  **The response builds with sustained deflection — it is not an instantaneous function of stick
  position.** The F14 held-pull ladder gives +1.5% / +4.7% / +8.0% / +8.9% for holds of
  250 / 500 / 1000 / 1500 ms, saturating near **+9%**, whereas F13's rapid 300 ms alternation reaches
  only +1.7% at *full* duty. So `EnginePitch` wants the elevator **low-passed / integrated**, with a
  time constant of order **0.5–0.7 s** (crude first-order fit to those four points; they do not fit a
  single exponential well, so treat it as an order of magnitude). That also explains the hand-flown
  spread — sustained dives and pulls reach ±10% while brief inputs barely move it.

  ⚠ Limits on the scripted run: the log-to-video alignment is only loosely determined (R² sits on a
  broad plateau for offsets 3.0–6.5 s, every one giving slope +0.015 ± 0.002 and span +1.6–1.9%, so
  the conclusion is robust to it but the exact offset is not). The rig's zero-mean-pitch-rate goal
  was **not** fully met — the aircraft gained 680 ft over the run and duty correlates with climb at
  R² 0.343 and airspeed at 0.512, which is why the descending leg, not the ascending one, carries the
  argument. F14 is weaker evidence than F13: its gauge decode is poor (`d2 sd` 19.2, airspeed
  bottoming at 0), its offset was solved from the note itself rather than independently, and its last
  row's gap window falls past the end of the run. Do not quote the 2000 ms row.

  **The pull-out, measured at last — `CAP-10 Dive Recovery.mp4`, 2026-08-04.** The eleventh take is
  the first to hold a dive *through* the recovery: level at 5,180 ft / 296 mph, pushover at t≈4.4 s,
  near-vertical descent to −32,600 ft/min and 355 mph, recovery t≈12.6–14.3 s, then five seconds of
  near-level flight at 296 mph. Decode is the cleanest of the set (alt `d2 sd` **4.61**, mph **1.14**
  — cf. F14's 19.2). `recovery.py` pairs it to the note; figure in `playtest/CAP-10/dive-recovery.png`.
  **Both halves of the original claim are confirmed in direction and about half the stated size:**

  | | claim | measured | where |
  |---|---|---|---|
  | drop through the dive | ~0.88 (−12%) | **0.9370** (−6.3%) | t=6.57 s |
  | overshoot at pull-out | ~1.05 | **1.0296** (+3.0%) | t=13.74 s |

  The overshoot is real — it clears both the level-flight baseline (1.0000) and the post-recovery
  settle (0.9985) — and it is **transient**, decaying back to baseline within ~1 s of the recovery
  finishing rather than establishing a new level.

  ⚠ **But the overshoot is not a pull-out phenomenon, and this take says the note needs two terms.**
  An equal-and-opposite bump appears at the **pushover**, where the stick goes the *other* way:

  | t (s) | 4.20 | 4.40 | 4.60 | 4.80 | 5.20 | 5.40 |
  |---|---|---|---|---|---|---|
  | note | 1.0004 | 1.0113 | **1.0301** | **1.0336** | 1.0048 | 0.9907 |
  | climb ft/min | +670 | +554 | +125 | −836 | −4,662 | −7,407 |
  | mph | 296.2 | 296.2 | 296.1 | 297.3 | 295.5 | 284.5 |

  At t=4.60 the note is already **+3.0%** while the aircraft is at its peak altitude, at its
  level-flight airspeed, and climb rate has moved only −545 ft/min. In the established dive a
  −30,000 ft/min change buys −6.3%; here a −545 ft/min change comes with **+3.0%** — ~55× the
  sensitivity and the **opposite sign**. A third instance sits mid-dive at t≈7.7 s: the note jumps
  0.9528 → **0.9942** while climb rate is still *steepening* (−26,400 → −29,800 ft/min). All three
  bumps have tracker NCC 0.62–0.68, i.e. level-flight confidence, so none is a tracking failure.

  So the note carries **(a)** an unsigned transient on stick movement — the F13/F14 effect, up for
  push and pull alike — and **(b)** a slow level that sits ~6% low in a sustained near-vertical dive
  and returns to baseline when level. BL-109's "overshoot at pull-out" is term (a) firing at the
  recovery; the "12% drop" is term (b). A single input into `EnginePitch.Eval` cannot produce both.

  ⚠ Read the whole-take R² on this clip with care, and do **not** use it to re-rank the drivers
  against F13. Over the 493 γ-unclipped frames it reads climb 0.519 / γ 0.511 / airspeed 0.314 /
  |dγ/dt| 0.052 — apparently reversing the scripted result. Two reasons it does not: this take has no
  logged input, so `dγ/dt` is the only elevator proxy available and it is a poor one (γ **saturates
  at −90°** for most of the dive because descent rate genuinely reaches airspeed, and the frames
  where it unpins throw ±60 °/s artifacts); and a whole-take R² weights the long sustained dive over
  the three ~1 s bumps, so it measures term (b) almost exclusively. The dissociation above is
  event-level and does not depend on any R².

  ⚠ The dive floor is **not** the overspeed whine (`BL-252`) leaking into the tracker: the note reads
  0.9381 at 301 mph (t 6.5–7.3) and 0.9415 at 351 mph (t 9.0–11.2) — 50 mph apart, 0.3% of note
  apart — and the floor is reached at t=6.57 s, *before* the high-speed regime. Tracker NCC does sag
  in the dive (0.48–0.51 vs 0.64 level), so treat the floor's exact depth as ±1% rather than exact.
  Two ~10 mph step glitches in the speedometer decode (t≈6.6, 12.1, 16.9) are needle-wrap artifacts
  and are not used for anything above.

  **The two cockpit takes cannot supply this number and are not a failed recording.** They do carry
  game audio — a +2 to +4 dB broadband 400–900 Hz swell that tracks the visual screen-shake window
  (`CAP-10.mp4` t≈6.5–12.0, `CAP-10 2.mp4` t≈8.0–17.5; r≈+0.5 against a frame-difference shake
  trace) — but a median-subtracted spectrogram shows **no moving harmonic at all** in either, against
  clear bending traces at ~230/400/660 Hz in the external takes. The cockpit mix is damped as the
  user describes, and what becomes audible as the shaking starts is **broadband rush, not a pitch
  change** — it is the wind/overspeed layer, and it belongs to `prop_sound`, not here (below).

  **Throttle was pinned at 100% through all four dives (user, 2026-08-04)**, so none of the swing
  belongs to the throttle term — it is climb-rate/elevator driven, as the 2026-07-30 playtest said.
  The user's own reading of the mechanism is *strain on the engine*, i.e. the note follows how hard
  the airframe is being worked rather than speed as such; that fits the sign we measure (nose-down,
  unloaded, engine **falls**) and is what the climb-rate-over-airspeed result says quantitatively.

  Decoded flight state and the pairing live in
  `analysis/video-flight-calibration/{run2chase,enginepitch}.py`
  (`playtest/CAP-10/` holds the audio side).
  ⚠ The engine note and the overspeed whine move together in a dive; a frequency-domain measurement
  that does not separate them will attribute one to the other.

- `BL-123` `[Tuning]` `[Owed-playtest]` **Audio (Run-2 item 11)** — `WhineMixGain` 0.12; A/B'd against the original 2026-07-30:
  close, but "could be a bit louder." `flightAudio.whineMixGain` is now config-wired
  (`FlightAudio.cs:145`). *Playtest after fix:* nudge `flightAudio.whineMixGain` up from 0.12 via
  `config.json`, re-A/B a dive, then delete the override (`docs/verification.md` DET-8 applies here
  too).

- `BL-161` `[Feature]` `[Blocked: cockpit view]` **`cockpit_engine_sound` ships per plane, unparsed.** `extracted/zrdr/vehicle.zrd.json`
  carries it alongside `engine_sound` for every plane def (Devastator: `snd_devastator_cp`, lines
  21-36; repeated for the other planes); `PlaneStats.Load` does not read it. (`damaged_engine_sound`,
  which this entry used to bundle, is parsed and consumed since `BL-090` item 1 landed 2026-08-01 —
  its gain TUNE is `BL-223`.)
  *Fix shape:* out of scope until a cockpit-audio feature is scheduled; recorded here so nobody has
  to re-derive from scratch that the *data* isn't the blocker.

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
  the def's own sounds.json volume, unattenuated) has no reference recording, unlike
  `WhineMixGain`'s measured dive — judge against the healthy engine at the controls after the
  regating. Config keys: `flightAudio.damagedEngineMixGain`.

- `BL-252` `[Tuning]` `[Owed-playtest]` **Overspeed-whine volume** (`prop_sound`; `FlightAudio.WhineMixGain` 0.12). `CAP-10` plus a
  live cross-check incidentally confirmed the **gating** of the original's dive/overspeed sound and
  left only its level open.

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
  ramp/stop cue against the new, unscaled level, not the old ×0.2 one.

## Cameras & views

- `BL-080` `[Feature]` **Future cockpit view** would consume a mix of already-parsed and still-raw data: `pcdpN`
  cockpit damage panels and the `*_damage_green/yellow/red` indicator anims are already parsed
  (PlaneStats parses them, DamageVisuals skips them). `cockpit_engine_sound` (`*_cp` WAVs, e.g.
  `snd_devastator_cp`) is still unparsed — see `BL-161`. `player_fuelleak` (0.85 threshold) IS
  already parsed, as a `VehicleInjureAnims` entry.

- `BL-150` `[Feature]` **plan-sized — not a TUNE. Numpad camera views — the whole scheme needs a rebuild, not a
  retune.** Current implementation: `FlightController.cs:286-303` (`Views[]` table, keys
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

- `BL-260` `[Feature]` `[Blocked: death/flyby captures]` **The death and flyby cameras stay capture-gated; two crash-camera fields unwired**
  (partial-close 2026-08-05: the crash camera and look-behind landed — `docs/HISTORY.md`).
  `CamParams.cs` parses all four authored `camparam.zrd.json` blocks; still dormant: the **death
  camera** (`death_interval/z/x/alt/min_alt` — placement magnitudes with unknown axes, and the
  engine has no distinct shot-down state to attach it to) and the **flyby camera** (12 fields
  describing a re-siting roadside pass; its trigger is undecodable without footage). A death and
  a flyby capture are still owed for those two. Also unwired on the landed crash camera:
  `crash_elev` 40 (duplicates `crash_y` 45's vertical role — the crash footage cannot separate
  the implied 56° from 53° look-down) and `crash_chord_y` 1000 (units unknown). Details and the
  decoded fields: `docs/formats/camparam.md`.
  ⚠ Trap: near-matches between authored fields and hand-picked values are suggestive, not
  decodes — wire nothing on one coincidence; each remaining camera waits for its capture.

- `BL-266` `[Research]` `[Owed-playtest]` **Plane wobble: residual decode questions after the
  wiring landed.** The oscillators are wired (`ShakeDefs`/`PlaneShake`, visual-only roll on the
  plane node; law and measurement in [`docs/formats/shakes.md`](docs/formats/shakes.md) and
  `analysis/gun-wobble-shake/`). Still open, all data questions: (a) the pure-caliber magnitude
  law is measured on ONE clip (Bloodhawk, 40-cal) — whether plane model/weight also enter waits
  on `CAP-30`; (b) the impact sources' per-event quantities are stand-ins declared TUNE (gun
  hits reuse caliber, rockets use armor damage) — a being-hit capture pins them; (c) the
  `ON_CALL` `small/medium/large` `damage_shakes` defs stay unwired — unknown caller, likely
  script/set-piece; (d) `high_speed`'s normalised-by-`fd_speed` reading fits the quiet-cruise
  evidence but is unverified against a calibrated dive.
  **`PT-44` flown 2026-08-07 and retired — three verdicts, and (d) is now half-answered:**
  - **(d) the normalisation is right, the OFFSET is missing — a decode correction.** At the
    controls the overspeed rattle is "too strong, and should ramp up the more we are over the
    speed" (user). The numbers agree: `PlaneShake.cs` computes `speedRatio / magnitude_quotient`,
    so at the gate it switches on at **1.0/70 = 0.0143 rad — 5.1× the 40-cal gun buzz
    (7e-5 × 40 = 0.0028)** — then grows only **27 %** across the whole remaining envelope
    (0.0181 at the 1.27× `fd_speed` dive terminal). Read instead as **excess over the gate**,
    `(speedRatio − min_speed) / magnitude_quotient`: zero at rated max, 0.0039 at the dive
    terminal — it enters imperceptibly, ramps with overspeed, and lands in the same order as the
    gun buzz instead of 5× above it. That is also why `min_speed` is authored as a gate *value*
    at all. Falsifiable against a calibrated dive.
  - **The frequency is NOT to be tuned** (user suggested lowering it): 15 Hz / damp 12.5 /
    sawtooth 1 is authored data, identical to `fire_bullet`. Expect the "too buzzy" complaint to
    dissolve once the magnitude drops — a 15 Hz buzz at 0.0143 rad reads nothing like the same
    buzz at 0.002.
  - **(a) the gun buzz is too small, and it is a pipeline loss, not a wrong constant.** Two
    independent sources agree: "barely noticeable" at the controls, and `engine_check.py` reads
    **~2–4× under the original clip**. But the `7e-5 × caliber` law was *measured from that very
    clip*, so a render 2–4× weaker than the clip means the chain from law to pixels loses
    amplitude — raising `magnitude_factor` would write a false number into decoded data to hide
    it. ⚠ `FINDINGS.md`'s leading explanation does not carry the gap: with `damp` 12.5 the
    envelope's mean over a kick cycle is 0.506 at 8 rounds/s vs 0.645 at 13, a factor of
    **1.27×**, not 2–4×. The wiring is not the problem either — `FlightController.cs:1417` kicks
    once per round per muzzle. *Approach, decided with the user 2026-08-07:* **(B) find the
    missing amplitude first** — reconcile the counter-derived 12–13 rounds/s against authored
    `FIRE_RATE` 8.0 (two guns at 8/s would be 16/s; check whether the probe fired one gun group
    or two), re-run `engine_check.py` rate-matched, and check what the 60 fps render-interpolated
    pose does to a 15 Hz buzz. Only if nothing survives that does it fall back to **(C)** a
    second amplitude measurement from `CAP-30` (whose row was extended for exactly this — as
    originally written it asks only about caliber and plane weight and **could not** have
    answered a uniform shortfall).
  - **Passing:** being-hit rocks read right — guns give a short rock, rockets read ok (user,
    2026-08-07, `--vs`).
  - **Unjudged:** (d) view coupling — the plane wobbling against the world in chase view cannot
    be seen while (a) is too small to see at all.
  *Playtest after fix:* re-fly the `PT-44` sortie once the offset correction and (a)'s residual
  land — the overspeed ramp, the gun-buzz magnitude, and the view-coupling check that (a)
  currently blocks.
  ⚠ Traps: `SHAKES_CAMERA` is NOT the fire-path shake mechanism — its sole carrier among all
  48 weapons is `wep_26` "FW", a zero-damage scripted fake weapon (a scripted detonation-shake
  marker); the fire path is the unflagged `fire_bullet` source. And the near-match trap: several
  magnitude candidates coincide with authored constants — wire nothing on one coincidence (the
  caliber law stood because the candidates separated by an order of magnitude each way).

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

## Splitscreen

Our splitscreen mode (2–4 players) has no counterpart in the original, so every rule it authored
against "the player" or "the camera" needs an explicit splitscreen verdict: generalise it, take a
nearest/union rule, or record it as deliberately single/global. This theme collects that work
(sweep of 2026-08-15). **Route fixes through the two existing seams instead of minting new ones:**
`GameSession.PlayerPositions` (nearest human) for gameplay rules that say "the player", and the
viewer set behind `ProjectilePool.Viewers` / `ScreenSize.NearestFloor` for draw rules that say
"the camera". Sim state stays global (`BL-338`'s ⚠). Splitscreen-scoped items that live with
their own system: `BL-231` (per-player pool term), `BL-296` (per-player ActionMap), `BL-299`
(MP spawn maps), `BL-301` (Dogfight tuning), `BL-314` (race countdown), `BL-351` (per-pane target
cycling), `BL-358` (board stacking).

The theme's fourteen items (`BL-126`, `BL-338`, `BL-339`, `BL-340`, `BL-365`–`BL-376`) are all
scheduled in [`docs/PLAN-splitscreen-polish.md`](docs/PLAN-splitscreen-polish.md) (2026-08-15) and
live there per the scheduled-items rule. New splitscreen findings mint here as usual.

## Missions, modes & campaign

- `BL-352` `[Feature]` **Instant Action's Table of Contents: the 19 preset scenarios and their View
  Story page.** Split out of [`docs/plans/PLAN-instant-action.md`](docs/plans/PLAN-instant-action.md) at writing
  (2026-08-14) as deliberately out of that plan's scope. The original's Instant Action screen is not
  primarily a form: down its left side sits `ia_tl_contents`, a 14-row list of 19 named preset
  scenarios (`langui` ids 3600 to 3618: *Girl Trouble*, *Sour Grapes*, *Black Hats and Hoplites*,
  *Swan's Gauntlet*, *Manhattan Tea Party*, …). Selecting one fills every dropdown, and *View Story*
  (`IDS_IA_B_VIEWSTORY`) opens its written setup. `IDS_IA_TABLE_INSTRUCTIONS` says so outright:
  "Select a mission below and click View Story to see its details on the next page." Most players
  never touched the dropdowns at all, so this is the mode's real front door.
  **What it needs.** The preset table is not in any shipped data file; `INSTANTACTION.SCRIPT` reaches
  it through engine callbacks 2301 (initial index plus mission type), 2302 (index to mission type)
  and 2353 (push the whole form), so it lives in `crimson.exe`. Decode those four callbacks and the
  table they read, then render the presets as a first screen ahead of the wizard's step 1. The story
  prose is a second question: it may be `langui` strings, or it may be `crimson.rof` artwork.
  ⚠ **Traps.** (a) `ia_tl_contents`'s selection sets the mission type, which then re-enables or hides
  the whole enemy-wave block (`if (0 == WT)`), so a preset is not just a set of dropdown values; it
  drives the screen's own state machine. (b) 19 presets against 7 environments means presets are not
  per-environment; do not assume a mapping. (c) Blocked on nothing, but pointless before
  `PLAN-instant-action` lands the configurable mission the presets would fill in.

- `BL-353` `[Feature]` **Weapon Loadout before an Instant Action flight.** Split out of
  [`docs/plans/PLAN-instant-action.md`](docs/plans/PLAN-instant-action.md) at writing (2026-08-14). The original's
  Instant Action screen carries a *Weapon Loadout* button (`IA_B_CHANGEWEAPONS`, `IDS_IA_B_WEAPONLOADOUT`)
  that opens `ORDINANCELAYOUT.SCRIPT` for either the pilot or the wingmen, selected by the
  `IA_B_PLAYER` / `IA_B_WINGMAN` radio pair beside it (`@globals@ZQ` is -1 for the pilot, -2 for the
  wingmen). We fly the stock fit and offer no way to change it outside `--loadout=` and the weapon
  lab.
  **Why it is cheap-ish.** The runtime half largely exists: `Loadout.ForRig` already binds every
  firepoint and every pylon from the stock fit, which is what the weapon lab uses to arm a mount the
  stock file never names, and `PylonOrdnance` already builds the mounted bodies. The missing half is
  the UI and the persistence of a chosen fit into `InstantActionDef`.
  **Raised 2026-08-15: this is no longer cosmetic on one mission type.** The `zeppelin_run` win
  condition decode (`PLAN-instant-action.md` G13's correction) established that the mode has two
  winning paths, the engines and the gasbag hull kill. Gasbags are behind the `DAMAGES_ZEPPELIN`
  gate, which in this install only `wep_14` (the aerial torpedo) and `wep_28` (the broadside
  cannonball) pass, and all 11 stock loadouts carry HE `wep_06`. So without this screen a
  menu-launched zeppelin run can only ever be won on engines: the hull path is unreachable by any
  route a player has, and `--rocket=wep_14` is a testing flag, not one. The original has no such
  restriction, because its own Weapon Loadout screen is where you fit the torpedo. The mode is
  fully playable meanwhile, which is why this stays a `[Feature]` rather than a `[Bug]`.
  ⚠ **Traps.** (a) The loadout is bound **before** the controller enters the tree, because
  `FlightController._Ready` builds the fire state and the ordnance-type list from it
  (`Session/FlightRigAssembler`); a fit chosen in a menu has to reach the assembler, not be applied
  after. (b) The pilot/wingman radio means one chosen fit covers all wingmen, not one each; do not
  build a per-wingman editor without checking that against the original. (c) The torpedo is not an
  ordinary rocket: `wep_14` carries `TARGETABLE` + `FLYOUT_HEALTH [10]`, so its in-flight
  projectile can itself be shot down ([`docs/formats/weapons.md`](docs/formats/weapons.md)). Offering
  it from a menu is the first time that path is reachable in normal play.

- `BL-354` `[Feature]` **The hangar: Build Custom Plane.** Split out of
  [`docs/plans/PLAN-instant-action.md`](docs/plans/PLAN-instant-action.md) at writing (2026-08-14) as a milestone
  of its own rather than a wave of that plan. `IA_B_BUILD` opens the customisation flow, which
  `crimson.rof` ships whole: `PLANESELECTION`, `PLANECONSTRUCTION`, `AIRFRAME`, `ARMOR`, `ENGINE`,
  `GUNS`, `HARDPOINTS`, `PAINT`, `PLANENAME`, `PURCHASE`. Instant Action's pilot-plane dropdown is
  sized `callback(1024) + 11`, the eleven stock airframes plus however many custom planes the player
  has saved, and `gui_continue` selects index 11 (the first custom plane) after a build returns.
  **What already exists here.** The paint half is decoded and implemented: the `.BM` region masks,
  the three-slot colour formula, the decal set and the per-aircraft pattern lists all live in
  [`docs/formats/paint.md`](docs/formats/paint.md) and `Mech3/PatternLibrary` + `Mech3/PlanePainter`,
  and the livery lab already steps them. The armour, engine, guns and hardpoints screens have no
  decode yet.
  ⚠ **Traps.** (a) Saved custom planes are 204-byte files in the install's `Planes/` directory and
  are only **partly** decoded: the name at 0x04 and the three colours at 0x68 as RGBA, with the
  pattern and decal indices immediately before them unread ([`docs/formats/paint.md`](docs/formats/paint.md),
  "Saved custom planes"). Importing a player's existing planes needs that finished; creating our own
  does not, and the two should not be conflated. (b) `PURCHASE` implies an economy, which belongs to
  the campaign and not to Instant Action.

- `BL-358` `[Polish]` **The Instant Action wrap-up board stacks on top of the stunt scoreboard on a
  `stunt_flying` mission.** Landed alongside `PLAN-instant-action.md` G14 (2026-08-14), screenshot-
  observed (`--ia=` a `stunt_flying` file, `--debug-scoreboard`): `FlightRigAssembler` already builds
  a per-pane `Flight/StuntScoreboard` unconditionally for any completed stunt run, and G14's own
  `Flight/IaWrapupBoard` wakes on the SAME event, so both render at once — the wrap-up board correctly
  drawn on top (`CanvasLayer` 10 over the pane's own HUD canvas), but the stunt board's zone-split
  table shows through behind it. Cosmetic only: every number both boards show is correct. Whether the
  fix is hiding `StuntScoreboard` for the duration of an Instant Action mission, sequencing the two
  (splits first, then the wrap-up), or leaving both (the original may have shown an analogous
  sequence of screens) is a design call this item did not make, since G14's own scope was the four
  rows, not the interaction between two already-separate boards — `docs/plans/PLAN-instant-action.md`'s own
  trap (b) on this item says not to change either board's persistence rule while adding one that
  shows both, which this leaves untouched.

- `BL-350` `[Bug]` `[Blocked: mission animations]` **Generator-spawned planes crash inside closed hangars
  (C1/M04 `--generators`, user-reported 2026-08-13).** The spawn position is decoded-correct: the
  original's launch routine (`FUN_00452450`) teleports the launched vehicle to the same generator
  node, joined by roster `group` over parked vehicles. What differs is the mission layer: the
  original plays a hangar-door-open animation (mission scripting/`WAKE_ANIM`, out of M4's scope)
  before launch, so the geometry the plane flies through is open. Ours never plays it, the door
  stays closed, and the collision sweep kills the plane on frame one.
  ⚠ **Traps.** (a) Do NOT "fix" the spawn placement — it matches the decode; the missing piece is
  the door animation, not the position. (b) Leads preserved from a partial decode, unimplemented:
  the launch also sets two timers (`+0xac = now + 1.5`, `+0xb4 = now + 2.5`, consumers untraced)
  and an initial velocity with a −22.352 m/s vertical component (the zeppelin drop case); read
  those before inventing any grace window. (c) The launched-vehicle mechanism itself (parked
  roster planes, not fresh spawns) is a separate fidelity gap from this bug; B6's fresh-spawn
  stand-in is documented in its landing commit.

- `BL-074` `[Research]` **PLAYER_INIT fields [3]/[4] semantics + per-plane spawn speed** — story-mission spawns
  currently assume the IA convention (0.5 throttle / 53.6 m/s).

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

  1. **Do not derive the pre-GO setback from a speed.** `SpawnSpeed = 53.6f` is flagged
     **PLACEHOLDER** (`FlightController.cs:300-302`) and `BL-074` will make spawn speed
     plane-dependent. Any "start N seconds back at the spawn speed" arithmetic therefore lands each
     aircraft of a mixed grid at a different point, which re-creates the unfairness the grid just
     removed — in the one coordinate the grid does not control. The on-rails walk above avoids this
     by construction: it simulates nothing and it ends on the spawn pose whatever the speed is.
  2. **This changes `StuntMission.Elapsed`'s documented rule.** "The clock never stops" is stated
     twice and on purpose (`StuntMission.cs:109-113` on the property, `:247-249` on `Tick`) — it is
     why a mid-run crash freeze still costs you time. A countdown means the clock must not *start*
     until GO, which is a different claim from stopping it mid-run; make the distinction explicit in
     both comments rather than deleting the rule, or the next reader reads the crash freeze as
     negotiable too.
  3. **Do not simulate the count and do not freeze the sim.** A physics-alive count that is actually
     flown re-opens the sink (`FlightController.cs:300-302`) and diverges per plane; a hard freeze
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
  readable and correctly edge-flipping at 2 players; 4-player still untested, no second controller
  pair available yet). Related, not absorbed: `BL-126` (splitscreen chrome). ⚠ The stunt race's
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
  *Cross-refs:* `BL-095`, [`docs/org/flightModel.md`](docs/org/flightModel.md)'s "Ground blow" —
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

- `BL-320` `[Bug]` **`RunTests.ps1` has no per-shot timeout, and the `viewer-bhawk` golden can hang
  the suite forever** (found during PLAN-overcast-match B15, 2026-08-08, reproduced on two
  consecutive runs, unrelated to that change — `--viewer` builds no chapter world). The shot
  renders, prints its unchanged hash, then the process never exits; the golden stage blocks
  until someone kills it by hand. Two halves: (a) diagnose why the `viewer-bhawk` launch fails
  to quit after `--screenshot` completes; (b) give the golden loop a per-shot timeout that
  fails the shot loudly instead of hanging the suite — a hung instrument that must be
  hand-killed silently corrupts unattended runs.
  *Workaround on record:* kill the lingering Godot process for that shot; the hash it printed
  is still valid.

- `BL-356` `[Bug]` **`HitchSidecar`'s queue (default depth 8, 3 s flush) loses records under a real
  hitch storm — confirmed at the controls, not just a theoretical TUNE gap.** A user session
  dragging the `DamageLab` sliders repeatedly (`.scratch/logs/fly-20260814-210336.{log,hitches.jsonl}`,
  see `BL-355` for the mechanism these hitches share) tripped `HitchMonitor` **31 times** in ~7 s
  (frames 3373-4243) but only **25 reached the sidecar/log** — three separate
  `hitch sidecar queue overflowed dropped=N` warnings (`N` = 1, 2, 3; the counter resets after each
  report per `HitchSidecar.Flush`, so the drops are additive: **6 records lost**, not 3). Both the
  human-readable `[perf] hitch …` line and the JSON sidecar entry are written together at flush time
  (`HitchSidecar.Flush`'s `WriteLogLine`+`WriteJsonLine` pair), so a dropped record vanishes from
  *both* — not silently (the warning fires, per the module's own design intent), but a diagnosis
  session reading the sidecar for "every hitch this session" is missing up to a fifth of them, and
  exactly during the busiest, most interesting stretch.
  *Fix shape:* `hitchSidecar.queueDepth`/`hitchSidecar.flushSeconds` are already `Config` keys
  (TUNE) — raising depth or lowering the flush interval is a one-line config change with no code
  risk, and is probably enough on its own for a solo-player session. Whether the DEFAULTS should
  move, or whether a compound event (BL-355 alone can produce 6-7 trips in two frames) needs a
  different policy (e.g. an immediate out-of-band flush the moment the queue nears full, rather than
  waiting the full interval), is the open design question — the constant fix is cheap, the policy
  question is not.
  ⚠ **Traps.** Do not read this as evidence the instrument is unreliable in general: every drop was
  reported (no silent gap), and the 25 records that DID land are exactly what diagnosed `BL-355` —
  this is a capacity tuning gap under a specific heavy workload, not a correctness defect in the
  detection or attribution logic.
  *Cross-refs:* `BL-355` (the hitches this session's queue couldn't keep up with),
  `PLAN-perf-hitches` B6 (`HitchSidecar`'s own design, `docs/architecture.md`).

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
