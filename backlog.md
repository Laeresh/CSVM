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

**Structure.** Items are grouped into eleven theme sections, in this fixed order: Damage &
destruction · Weapons & combat · Flight model & collision physics · Environment & world · Effects
& animation runtime · Audio · Cameras & views · HUD & UI · Missions, modes & campaign · Tooling,
platform & docs · Misc. Within a theme, items sort by ascending ID. A straddler goes to the theme
whose system you would open to fix it; Misc is the escape hatch for items with no such system —
if it grows past a handful, that is a missing theme, not a working bucket.

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
  them to move needs a new measurement first.

## Damage & destruction

- `BL-008` `[Bug]` **Break-apart debris barely moves — "parts only move a short way."** The piece launch is
  `MotionRuntime` (`CSVM/src/Mech3/Anim/MotionRuntime.cs`), and two things combine — **neither of
  them the FROM_TO dropped-delta bug** (that is a different event kind; the debris
  `translation.delta` *is* mapped):
  1. **World destructibles inherit no momentum.** `InheritedWorldVelocity` is set **only** by the
     plane crash (`FlightController.cs:1433`); the shared world `AnimRuntime` never assigns it, so a
     world piece gets only the small authored launch — a 5–10 m/s straight-up pop, which is exactly
     "the pieces barely drift."
  2. **Ground-rest / bounce is deferred.** `do_intersections` + `bounce_sequence` are not simulated
     (counted deferred at `AnimRuntime.cs:2034`), so a piece integrates freely over `run_time` then
     **holds its final pose** — translate a little, stop.
  ⚠ A third cause once listed here — "the magnitude decode is unsettled TUNE", `translation_range`
  xz/y read as distance ÷ run_time with `initial`/`delta` unmapped — is **settled and no longer a
  cause**: xz/y are an azimuth/elevation in degrees and `initial` the launch speed, `delta` a speed
  ramp (`docs/HISTORY.md` 2026-08-01, census of all 1,217 events). Do not re-open it. How much
  limpness is left after that fix is itself worth a look before this item is scheduled.
  **LARGER:** there is no single correct number — livelier world debris means either a world-object
  launch multiplier (a TUNE mirroring the crash's `WreckMomentum`) or implementing `bounce_sequence`
  ground-rest (the deferred Layer-1.5 physics-ray work). Both need an original-game A/B. Cross-ref the
  "Data-driven crash" TUNEs already in this file (`WreckMomentum`, tumble-rate, debris-arc).
  ⚠ Do **not** "fix" it by reviving the FROM_TO deltas — wrong mechanism.

- `BL-009` `[Research]` **C2 SeaHangar doors don't despawn and stay collidable after shooting the propane tank.** The
  SeaHangar doors are `sgh_door1`/`sgh_door2`, driven by `sghangar-opensgdoors` — a **HEALTH-0
  `OnStartup` "open the doors" animation, not a weapon-destructible.** A destructible is any def with
  `HEALTH > 0` (`AnimRuntime.cs:214-216`), so the doors are **never registered in
  `DestructibleRegistry`**; `Resolve` never maps their collider, no death sequence runs, so nothing
  hides them or removes their colliders — they open, then stay as solid set-dressing. The propane tank
  the user shot is almost certainly **Hollywood's `kkgate`** (its `ANIMATION_ROOT_NAME` is `propane`,
  HEALTH 10) — a *different* building, whose death chain (`genx12`/`tbridg*_fire`/`free_the_goose`)
  does not touch `sghangar`. **LARGER — a data/design gap, not a collider bug** (the collider-removal
  machinery is proven on `gate1`/`gate2` and `kkgate`). To settle: (a) confirm from the C2 gamez/zrdr
  whether any propane→`sghangar` chain is authored at all (docs show only `kkgate`'s); (b) A/B the
  original — does shooting a propane tank there destroy the SeaHangar doors?; (c) if it should, decide
  how — give the doors their own destructible def, or a chain-reaction `CALL_ANIMATION` firing an
  `OBJECT_ACTIVE_STATE` swap on them (the fuel-depot pattern). ⚠ The C25 plan text named
  `sghangar_doors` as an intended case, but the shipped C2 data does not make them destructible — an
  aspiration/data mismatch. ⚠ Even a working destructible door may leave wreck colliders — "clear
  passage" is its own playtest.

- `BL-022` `[Bug]` `[Owed-playtest]` **Debris trajectory is wrong, not merely slow — RE-PLAYTEST, the magnitude half is no longer
    a TUNE.** In-flight kills threw pieces "but not in the correct trajectory". **The largest cause landed
    2026-08-01**: `translation_range` was read as a distance travelled when it is an azimuth/elevation launch
    with `initial` the speed (`analysis/object-motion-range/`), which threw debris hundreds of metres along
    one bearing — visible as the `c1-destroy-effects` golden's line of fireballs marching up the runway.
    What remains of this item is what that fix does NOT cover: world objects inherit no launch momentum, and
    `bounce_sequence` ground-rest is unsimulated (both need a physics ray).
    ⚠ **Traps.** (a) Do not re-open the magnitude as a TUNE — the speeds are decoded and censused now; a
    piece that still looks wrong is the momentum or the ground-rest, not the launch. (b) `gravity.value` is
    absolute m/s² (a literal −9.8 on 173 events), NOT an offset to the aircraft's arcade `nom_gravity` of 20
    — that reading was considered and disproven by the same census.
    *Playtest after fix:* look for wreck pieces arcing along a correct trajectory, not just moving
    further. `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`.

- `BL-059` `[Feature]` **Data-driven crash — the remaining variants/follow-ups.** The dirt/ground crash is
  complete and the default (`PLAN-data-driven-crash`, `docs/HISTORY.md`). What is still open:
  1. **`bounce_sequence` re-launch (Layer-1.5).** The piece/debris `ObjectMotion`s carry
     `do_intersections` + a `bounce_sequence` (`pNhit` → `ground_mixed_exp_sg` + a second ranged
     launch) that `MotionRuntime` does not yet act on — it needs a ground-contact physics ray
     (`CrashBreakup.Advance`, on branch `bespoke-crash-animation`, is the reference integrator). The
     pieces tumble to rest fine without it; the bounce is an embellishment. Also needs a real
     `SOUND_GROUPS` resolver for `air_mixed_exp_sg`/`ground_mixed_exp_sg` (`snd_exp_ground_a` already
     plays, hardcoded like `plane_destroy_sg`).
  2. **The air variant.** `player_crash_default` — no-impact destruct, `destroyed=false`, pieces arc
     away, per-piece `large_firetrail` — has **no trigger**: it fires when the plane is destroyed with
     no impact at all, and nothing shoots the player down yet. A building crash is `_dirt`, not air, so
     `CrashSurface.Air` is deliberately unreachable from `ClassifySurface`, which reads a *struck
     body*. ⚠ Do not wire it off a low-HP test on the collision path — that is the ground crash with a
     different def. Data: `extracted/C1/cam_anim/player-player_crash_default.json`; the water half
     landed 2026-08-02 (`docs/HISTORY.md`), full decode there under 2026-07-23.

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
  `healthy`), not the impact point. For the debris arcs: `translation_range` is undocumented and
  `AnimRuntime` never simulates it, so the `fly_trailN` trajectory is a *reasoned reading* (`xz`/`y`
  = travel distance over `run_time`), not a settled decode — the anchor is invisible, only the
  trail shows, so the arc shape is TUNE, not fidelity; and the DISTANCE interval hides behind an
  inverted flag (`has_interval_value` false, key off `interval_type`).

- `BL-102` `[Research]` **Patrol boat: which HP governs?** (from `docs/plans/PLAN-M3-weapons.md` C23, 2026-07-22.)
  Two systems **agree on the damage-stage fractions and disagree on total HP by exactly 2×**: the
  `patrolboat` vehicle def says `health 40` with stages at 0.60/0.30 firing
  `ptboat_50damage`/`ptboat_75damage`, while the `C1/patrol_boat` anim def says `HEALTH 20` with
  stages at `ANIM_HEALTH` 12/6 (also 0.60/0.30) firing the generic
  `sputter_black_smoke_obj`/`sputter_fire_smoke_obj`. Likely reading: the vehicle def governs the boat
  as an **AI combatant**, the anim def as **placed scenery** — so M3 (scenery only) wants 20.
  **Counting hits in play is impractical (user, 2026-07-30) — settle from data instead.**
  **Where they are:** a placed `patrolboat` gamez node exists in C1, C2, C3 and C5 (none in C4); it is
  wired to a destructible (`HEALTH 20`, 0.60/0.30 stages, `ptboat_50damage`/`75damage`) only in
  **C1/M05, C2/M01 and C5/M01** — `--node=patrolboat --viewer --chapter=C1` (or `C2`/`C5`) frames it
  directly. **C3's placed boat has no mission wiring at all** (grepped every C3 mission/IA folder for
  `patrolboat` — zero matches outside `gamez`/`textures`), so it is inert scenery there, not a target.
  Those same three missions' `aiv.zrd.json` (the **AI vehicle table**,
  `docs/formats/anim-definitions.md:564` — confirmed **not** a spawn roster, so entry count ≠
  spawned-boat count) also carries `patrolboat_N` behaviour entries (12 in C1/M05, 1 in C2/M01, 2 in
  C5/M01) with per-entry position/heading — consistent with patrol boats being AI-piloted there, but
  `aiv.zrd`'s numeric schema is undecoded, so which HP value a moving AI boat actually reads is not
  provable from this file alone.
  **The "exactly 2×" doubling does not generalize** (checked as asked): `t_truck`'s vehicle def
  (`armor 0`, `health 40`) vs. its own mis_anim def
  (`extracted/C5/M01/mis_anim/t_truck-t_truck.json`, `health 15`, stages at 12/8 = 80%/53%, not
  60%/30%) disagree by **2.67×, not 2×**, and with different stage fractions — a genuine
  counter-example to a fixed doubling rule. `armytruck_destruct` and `fueltruck` have **no vehicle-def
  entry at all** (grepped `extracted/zrdr/vehicle.zrd.json`) — anim-only, so there is nothing to
  duplicate; notably `armytruck_destruct` still uses the same 60/30% stage split as `patrolboat`,
  which is better read as a **shared authoring idiom for two-stage damage** than as evidence of a
  doubling bug.
  **Revised settle path:** decode `aiv.zrd`'s per-entry field layout (or find it already decoded
  upstream for MW/PM, which share the vehicle-table concept) far enough to confirm whether a moving AI
  patrol boat's hit points come from the vehicle def or a mis_anim-style def — a stronger, data-side
  argument than a cockpit count, and it doesn't need the original open.
  ⚠ **Never infer a damage threshold from an animation's name.** `ptboat_50damage` fires at
  **60 %** health remaining and `ptboat_75damage` at **30 %** — the names lag their trigger,
  the same way `docs/formats/hud.md` records for the cockpit damage dial ("the anim names lag
  their effect by one state"). Measured 2026-07-22 while planning M3.

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
  any more; the heavy stage plays `player_damage_trail` at `prop1` (`BL-246` for *when*).

- `BL-122` `[Tuning]` `[Owed-playtest]` **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)** — several playtest-gated TUNEs,
  all needing the original at the controls: `WreckMomentum` **0.4** (`FlightController.cs` — the
  fraction of impact velocity the wreck pieces inherit, so they scatter along travel vs. pop straight
  up); the **`forward_rotation.Time.initial` ÷ run_time** tumble-rate reading in `AnimRuntime`'s
  `MotionRuntime` (the pieces carry clean π multiples read as a *total* angle, not a rate); the
  **debris-arc trajectory** (`translation_range` read as travel distance over `run_time` in a fanned
  azimuth — the `fly_trailN` anchor is invisible, so only the arc's rough scale reads; `MotionRuntime`,
  `initial`/`delta` unmapped); the **overall crash intensity** (the fireball, the cluster, the debris
  fire and the wreck fire are all additive, so a dirt crash can read as one big fireball — judge the
  whole against the original); and `snd_exp_ground_a` mix level + whether it should layer over
  `plane_destroy_sg` (the dirt def's only Sound is `snd_exp_ground_a`; we keep both). The retired
  bespoke crash on branch `bespoke-crash-animation` is the A/B reference for these.
  **`CAP-16` analysed 2026-08-04** (`CAP-16.mp4`, 2560×1440, 13.49 s; corroborated by
  `C1 IA1 Crash.mp4` and `CAP-14 Crash.mp4`, two further ground crashes with the same signature;
  stills in `playtest/CAP-16/`). ⚠ All times below are **wall-clock** off container PTS — multiply
  by k = 1.390 for sim-seconds before comparing against any authored `run_time`.
  - **Tumble is a total angle, not a rate — the reading in the entry above is confirmed.** A wing
    panel detaches at ignition and stays legible for 8 sampled frames, t = 6.13 → 6.60
    (0.47 s wall / 0.65 sim-s; `wing-tumble-strip-6.13-6.60.png`). Its long axis rotates only
    **~10–15° over that span** — order 20–30 °/s wall-clock. A π-rad-per-second *rate* would have
    turned it ~85° in the same window, which is not what the footage shows. So `forward_rotation`'s
    clean π multiples read as the piece's **total** sweep over `run_time`. *Limit:* one piece, seen
    near edge-on under camera motion, so only rotation about the view axis is observable.
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
    - Limit: the ruler only exists near ignition (the airframe is gone within ~0.5 s and no known
      length survives in frame), so this is an *early-frame* comparison. Our burst is dead by ~1.0 s
      while the original is still at full intensity 9 sim-s later — that gap is the hold time above,
      a separate matter from size.
  *Residual.* The player's **own** crash can never show the burn-out — the game cuts to menu — so do
  not re-film one hoping for it. The one untried vantage is an **enemy** plane crashing while the
  player stays alive, which would keep the scene up; worth a capture only if the hold time above
  turns out to be the binding constraint when tuning. Otherwise what remains is the **A/B against
  our build at the controls**, with the reference numbers above to judge against.

- `BL-246` `[Research]` **Smoke/fire trail is effectively unreachable from organic gameplay** (found while
  fixing the trail-anchor bug, 2026-08-03). The whole-plane `player_smoketrail` needs a part at
  ≤ 0.10 HP fraction (`DamageVisuals.cs`), but the only in-game damage source is a terrain graze:
  `GrazeMaxDamage` 18 behind `_damageCooldown` (`FlightController.cs`) against 15–25 HP parts, and
  a critical part reaching 0 crashes the plane outright — so hitting the 0.10 window without dying
  takes several survivable grazes on the *same* part, which normal play never produces. The F5 lab
  (or `--damage=`) is currently the only practical way to see the trail. Design/tuning question,
  deliberately split from the render fix: candidate shapes are weapon fire damaging planes (no
  enemy-fire path exists at all today), a lower smoke threshold, or accepting it as a
  near-death-only effect like the original. Decide against the original at the controls.
  **`CAP-15`'s footage (analysed 2026-08-05, `playtest/CAP-15/`) reframes the stakes:** in the
  original, ONE survivable graze puts on the whole show — per-panel `short_firetrail` fire for
  ~12 wall-s then sputtering black smoke past 31 wall-s, plus the charred-wing skin swap — and
  **no nose-anchored whole-plane trail ever appears** even with the wing red-critical to clip
  end. So the drama the player actually sees at heavy damage is the panel-level burn (reachable
  organically today), and the `player_smoketrail` pair may be rarer in the original than we
  assumed, or anchored at the damage site rather than the nose; the clip cannot separate those.
  **The anchor half is landed (`BL-259`, 2026-08-05):** the build plays `player_damage_trail`
  (`short_firetrail` at `prop1` + the `fire_lt` light) at the ≤ 0.10 tier. The corpus check made
  at that landing corrected an earlier claim: the data's own ≤ 0.10 entries (`player_smoketrail`
  / `player_firetrail`) DO call `dense_firetrail` at `prop1` — CAP-15 favours the
  `short_firetrail` shape and the mapping is one pinned string in `DamageVisuals.RigAnimFor` if
  ever revisited. The remaining question here is only *when* the exe calls the heavy stage, not
  where it sits — do not loosen the 0.10 tier to make it reachable.

- `BL-253` `[Bug]` `[Owed-playtest]` **The C2 facade panels' log debris landed (2026-08-04, `docs/HISTORY.md`); only the
  in-cockpit playtest is owed.**
  ⚠ **Still open.** (a) The in-cockpit playtest: fly a row of facade panels and confirm logs visibly
  launch from each struck panel, with several rows' debris flying in parallel rather than the earlier
  fix's per-row floor (the pool cap — 6, `localCallRoots` in `CSVM/data/effect_pools.json` — is an
  invented number, judged in the same flight; see `BL-231`). (b) The `air_mixed_exp_sg` one-shot
  authored on the same death is unconfirmed audible — D31 death audio is still stubbed engine-wide,
  unrelated to this item's scope.
  ⚠ **Trap kept from the diagnosis:** `facdsticks`' `part1`–`4` bind through the CALLEE's own
  compiled symbol table, and `blockit2`'s same-named `part1`–`7` carry their own distinct ptrs — the
  two never share a lookup, so do not add a name-based rescue near `Targets()`
  (`docs/formats/destructibles.md`'s `⚠` on symbol-table binding).

- `BL-254` `[Bug]` `[Owed-playtest]` **Both C2 studio gates' deaths now match the original (2026-08-04, `docs/HISTORY.md`);
  only the in-cockpit playtest is owed.**
  ⚠ **Still open.** (a) The in-cockpit playtest, now covering both gates: fly gate2 to confirm the
  doors fall before the archway blows and the passage only truly opens once the wreck's colliders
  replace the healthy ones; fly gate1 to confirm the doors open but the archway stays solid — no
  passage. (b) `gate2`'s death also calls `go_get_her` (mission scripting) — left to whatever
  handles it today; this item was the swap timing/authorship only. (c) The 28.5 s offset reads long
  but is what is authored — A/B the original's timing rather than "fixing" the number.

- `BL-291` `[Feature]` **A way to spawn/damage a zeppelin — the thin harness that finishes `BL-239`'s in-game
  verification** (PT-36, 2026-08-06). Splash damage reads right at the controls, but nothing in
  C1 shows damage registering on the zeppelin, so `BL-239`'s one unverified picture — a gasbag
  taking blast damage from a hit well off its centre — still has nowhere to be seen. Wanted: a
  `--damage-test`-style spawn of a damageable `hk_zep`, or a debug damage readout on the existing
  C1 one — just enough to watch blast numbers score to the gasbag. Acceptance test: a rocket into
  one END of a gasbag, away from dead centre, damages it (the nearest-collision-shape falloff,
  landed 2026-08-05). Explicitly out of scope: the authored destruction sequence (`breakupzep` →
  13 `break*`, the crash-sink motions) — that is its own M4-sized feature for when zeppelins
  matter to gameplay, not this item.

- `BL-297` `[Research]` `[Blocked: CAP-29]` **Panel-damage semantics: what the original actually
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
  template, so successive panels' bursts look identical. The armor question is a scale ambiguity:
  we consume threshold fractions as COMBINED armor+HP (`DamageLab.Combined`); stock parts are
  25 armor/25 HP so `pdpanelN` thresholds ≤ 0.5 do imply armor exhausted under armor-first — but
  armor upgrades shift the combined scale, `--damage` presets floor armor, and health-only vs
  combined is undecoded.
  *Fix shape:* answer `CAP-29` first; then either close as faithful-as-authored, or change
  mechanism — e.g. resolve `random_gun_impact`/`player_fuelleak`'s panel pick to the pdpN nearest
  the struck part instead of the authored random, and/or re-base injure thresholds on health-only.
  Any such change is a deliberate deviation or a re-decode — not a bug fix — until the capture
  says which.
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

## Weapons & combat

- `BL-062` `[Research]` **Rocket firing order — the H-selector fidelity question.** Settle from the original
  whether the player selects an individual **hardpoint** or the game just drains them in pylon
  order. Our build drains the selected pylon fully before advancing, and since `BL-025` landed
  (2026-07-30) H / D-pad Right cycles individual pylons even on a uniform-ammo loadout
  (`WeaponCursor.NextSelectable` — the old `_ordnanceTypes.Length > 1` gate is gone). Meaningful
  mixed-ordnance cycling arrives with the M4 configurator (mixed loadouts).

- `BL-065` `[Feature]` `[Blocked: M4]` **M3-deferred gun mechanics — firing heat and cannon jam** (scoped out of
  `docs/plans/PLAN-M3-weapons.md` 2026-07-22, decision 4: friction with no combat pressure to justify
  it while nothing shoots back). **The constants are exact, so nobody needs to re-derive them:**
  `weapons.json` `FIRING_HEAT` on 4 entries (30-cal = 5.0); `vehicle.json` `cannon_jam` on
  `player_airplane` = `heat_safe_limit 1000`, `heat_dissipation_rate 50`, `jam_chance 0.1`.
  Heat accumulates per shot, dissipates at 50/s, and past the safe limit each shot has a 10 %
  jam chance. Pick this up when there is combat pressure — i.e. alongside or after M4 AI.

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

- `BL-068` `[Feature]` **M4 (AI) is scoped but NOT scheduled** — [`docs/SCOPING-M4-ai.md`](docs/SCOPING-M4-ai.md)
  (2026-07-25). The shipped AI data (patrol graphs, turret specs, AI vehicle rosters, generators,
  zeppelin combat parameters, 1,309 combat voice clips) is already in the extraction and **nothing in
  the engine reads any of it**; the document inventories it against the retail files, re-expresses the
  original design's AI specification, and grades what the engine can reuse unchanged. **Its headline
  blocker — the flying aircraft has no physics body, so nothing can shoot a plane — gates the whole
  milestone.** It is a scoping study, not a plan: scheduling it means renaming it to
  `docs/PLAN-M4-ai.md`. The per-pilot skill vector is located and bounded in
  `analysis/m4-ai-data/FINDINGS.md`. `BL-069` records the M3-era leads it absorbs.

- `BL-069` `[Research]` **M4 dependencies discovered while planning M3** (2026-07-22) — recorded so they are not
  re-derived:
  - **Turrets are AI gunners**, not player-aimed: they acquire and engage other aircraft
    automatically (user-confirmed). This is why `extracted/zrdr/ai.zrd.json` contains nothing
    but `TURRET` defs. Five player planes carry one — `pavenger`, `pbalmoral` (two: front +
    rear), `pbrigand`, `pfirebrand`, `pkestrel` — and **W4 in the stock loadout table is filled
    on exactly those five and no others**. `vehicle.json` `turrets` gives `firstp`/`thirdp` node
    pairs (which mesh renders in which view, *not* a player camera mode) and **nothing else — turret
    rotation limits are not in any reader and stay undecoded**. `gun_pitch`/`gun_yaw` (±11°) are
    **not** the turret arc: they sit on AI aircraft defs including seven turret-less ones, on no
    player def including all five turret airframes, so they are the AI's forward-gun aiming cone
    (census in `docs/formats/vehicle.md`).
  - **`target`** — a mesh-less marker, one per plane root (11 player + 11 AI). The aim point
    for AI gunnery and air-to-air lock-on.
  - **Air-to-air lock-on.** M3 implements the full guided-missile flight model but restricts
    acquisition to ground destructibles, so air-to-air is a targeting change, not new flight
    code.
  - **Shootable ordnance.** `wep_14` (TORPDO) has `FLYOUT_HEALTH 10` and `TARGETABLE` — the
    torpedo itself can be shot down. Inert in M3 because nothing else shoots.
  - **AI vehicle armour/health.** `vehicle.json` carries an `armor` + `health` pair on AI defs
    only (aircraft always `armor == health`, 60–100; `patrolboat` and `t_truck` `armor 0 /
    health 40`). `PlaneStats` does not read either. The model is **armour-first, then health**
    (see `docs/plans/PLAN-M3-weapons.md` C23 for the dominance argument that settles it).

- `BL-091` `[Research]` **`sticky_bullet_*` — a shipped aim-assist system nothing reads.** `player.json` carries
  `sticky_bullet_catchup_rate 5.0`, `sticky_bullet_inaccuracy 1.0`,
  `sticky_bullet_forget_interval 1.5`, `sticky_bullet_dist_factor 0.0`; **zero references anywhere
  in the repo.** The names read as bullet magnetism toward a tracked target — how fast rounds catch
  up, a scatter term, and how long a round remembers its target — with distance attenuation shipped
  **off** (`dist_factor 0`).
  ⚠ **Traps.** `player.json` is the *global* tuning file, not a player-only one (it also holds
  `min_ai_active_dist`, `ai_groundblow`, `ai_skill_parameters`), so whether this assists the
  **player's** gunnery or the **AI's** is unsettled — settle that first, because "your bullets
  curve" and "their bullets curve" are opposite feel promises. Untestable either way until aircraft
  are hittable (see the incoming-fire entry). **The cheap first half is documentation:** no
  `docs/formats/` page covers `player.json`'s combat/AI globals — `vehicle.md` documents only
  `nom_gravity` and `sounds.md` the curve blocks — so `warning_shot_*`, `sticky_bullet_*`, the
  `crash` armour/health ranges and `respawn_rad`/`respawn_el` are all undocumented shipped tuning.

- `BL-141` `[Research]` **`shell1.png`/`shell2.png` — the doc's own listed "tracer" texture pair — are wired to
  nothing: not `gunshell`, not any reader def, not any engine code.**
  `docs/formats/weapon-effects.md:148` groups them under "Tracer" textures. Traced the actual
  consumer: `extracted/C1/gamez/textures.json` indices 104/105 → `materials.json` indices 108/109
  (`Textured`, `texture_index: 104`/`105`) → `models.json` model 60 → `nodes.json` node 205 (`g1`),
  whose parent chain is `rabbit_blur` (203) → `g11` (200) → `rabbit_blur` (198) → … — a recurring
  generic-named mesh chain with **no relation to `gunshell` or any weapon node by name or parentage**.
  Grepped `CSVM/src` and every `extracted/*/cam_anim/*.json` / `extracted/*/*/zrdr/*.json` for
  `shell1`/`shell2`/`rabbit_blur`/`gunshell`: only the texture files and this one material/model pair
  exist; nothing calls, anchors, or names them from any weapon-effect def.
  ⚠ **Correction (`BL-140`'s 8-chapter sweep, `analysis/weapon-effects-node-shape/`).** The node
  numbers above are off by the `+1` anim-def-ptr convention (`analysis/weapon-effects-node-shape/`) — raw
  `nodes.json` index 204, not 205, is the `g1`/model-60 node — and at the raw index, its
  `parent_indices` is `[203]` (`gunshell`) only, not the `rabbit_blur`/`g11`/`rabbit_blur` chain
  this entry describes (those names sit at nearby *list positions*, not as this node's actual
  parents). Model 60's node **is** `gunshell`'s own only child, contradicting "no relation to
  `gunshell` … by parentage" above. The `shell1`/`shell2` textures are still unmatched to it — that
  part of this entry stands — but "the data gives no mesh" is no longer true for `gunshell`
  specifically; see the corrected footnote in `docs/formats/weapon-effects.md`. Not re-investigated
  further here — whether `rabbit_blur` itself is real terrain-effect geometry, model 60's actual
  visual shape, and the `rabbit_blur`/`g11` chain's true relationship to model 60's node are still
  open.
  *Fix shape:* none — a "confirm before assuming" flag. `BL-013`/`BL-137` landed (C22, 2026-07-31)
  by **instancing the authored `gunshell` subtree**, so model 60 renders with its own materials (the
  ones whose texture indices are `shell1`/`shell2`) and no texture was hand-repurposed — the trap
  this entry guards never fired. Still open here: whether `rabbit_blur` is itself a real, unrelated
  visual effect (a motion-blur streak), model 60's actual visual shape, and the `rabbit_blur`/`g11`
  chain's true relationship to model 60's node.

- `BL-213` `[Research]` **Does the original splash when gun rounds range-expire over water?** Needs a CAP of the
  original (fire out to sea from altitude, watch the 1000 m expiry point). Until answered, our rounds
  expire silently, which METHOD-18 documents as correct-per-data.

- `BL-215` `[Tuning]` `[Owed-playtest]` **Rocket-trail puff size (C21, 2026-07-31)** — the trail look and per-type character
  passed the cockpit A/B (PT-09), but the user flags the puff size as possibly needing more tuning.
  The authored FLYOUT values are verbatim; only render-side size/overlap is in play.

- `BL-222` `[Feature]` `[Blocked: M4]` **The `player` IMPACT surface class — the general got-shot feedback on your own airframe,
  authored on 44 of 48 weapons and untriggerable until something shoots back (found 2026-08-01
  while landing `BL-090` item 2).** `weapons.json`'s `IMPACT` block is keyed by surface class, and
  `player` ("the struck surface is the player's aircraft") is populated on 44 entries: most name the
  caliber's own `*_gunhit`, several name `f18sparks2`, and `wep_03` (60slug) names
  `SURFACE_ANIMATION: random_gun_impact` — the spark burst at a `pdpN` panel that B4 wired.
  `SurfaceClass.Player` already parses (`WeaponDefs.cs`) and `ProjectilePool` already classifies
  surfaces; what is missing is a shooter. **Blocked on M4's enemy aircraft**, not on data or decode.
  This is the *general* mechanism B4's goal described — B4 reaches it only through the Devastator's
  one-off 0.99 `injure_anims` entry, which is plausibly an authoring leftover
  (`docs/formats/vehicle.md`).
  ⚠ **Traps.** (a) `ProjectilePool`'s hit detection is a world raycast against a body-less plane —
  a round never hits an aircraft at all today, so this needs the aircraft to become a hittable body
  first; it is not a matter of adding a switch case. (b) Do not reach it early by firing the
  `player` effect off our own collision path — that is what B4's Devastator entry already does, and
  conflating "I was shot" with "I scraped a wall" would make both wrong. (c) `f18sparks2` is
  undecoded — check it resolves in the effect readers before assuming the class is fully wireable.

- `BL-226` `[Feature]` `[Blocked: M4]` **The incoming-fire cue set's other two halves are blocked on things that do not exist
  yet.** The near-miss third landed (`BL-087`, 2026-08-02); `bullet_hit_sg` (= `snd_ricochet1-4`,
  `player.json`'s `bullet_hit_sound`) and `window_hit_sg` (= `snd_windowhit1-3`, non-3D) did not.
  Both sit on the five `player_pfighter-bulletN` canopy-hole defs (the `bullethole_anims` of
  `docs/formats/vehicle.md`, 10 files per chapter × 8), so they are shipped and referenced, not
  orphans. The design's rule is that incoming-fire intensity is how the player reads a shooter's
  distance, calibre and ammo type; the accumulator that rates it is now decoded and running
  (`WarningShotCue`), so both cues can hang off it once their blockers clear.
  ⚠ **Traps.** (a) **The blocker for `bullet_hit_sg` is that nothing can shoot an aircraft:** a plane
  exists in physics only as a `CastMotion` query shape (`PlaneCollider`), never as a body, so a
  projectile raycast can never strike one — own plane or another player's. Giving aircraft real
  bodies is the prerequisite, and it is not a small change (every round currently passes through
  every plane, including the firer's own). (b) `window_hit_sg` additionally needs a cockpit view —
  the bullet defs are `PlayerFirstPerson`-gated. (c) **Do not fake either off our collision path**:
  firing the hit cue on a wall scrape conflates "I was shot" with "I hit something", the trap
  `BL-222` records. (d) Only `snd_warningshot1-3` are true orphans (in no `SOUND_GROUPS` entry and
  named nowhere) — do not conflate the four groups.

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

- `BL-289` `[Tuning]` `[Owed-playtest]` **Gun-impact looks (A2/`BL-203`, landed 2026-08-01)** — `Projectile.cs`: dirt chips
  `DirtDebrisSprites` **5**, `DirtDebrisSize` **0.45 m**, `DirtDebrisLife` **0.9 s** (authored bit
  RUN_TIME is 1–2 s), `DirtDebrisSpeed` **4 m/s**, `DirtDebrisSpreadDeg` **60°**, `DirtDebrisSpinMax`
  **25 rad/s**; building ricochet `RicochetSparks` **8**, `RicochetSparkSize` **0.55 m**,
  `RicochetSparkLife` **0.55 s**, `RicochetSparkSpeed` **22 m/s**, `RicochetSpreadDeg` **90°** (a
  stand-in — both authored assets are missing from the install). The water-splash column width
  is settled and out of this entry: `SplashColumnWidthScale` **8×** confirmed at the controls
  2026-08-06 with the fades in (`BL-265` closed — the authored quad is 5 cm wide, sub-pixel past
  ~30 m; the reference ticks measure ~0.35 m, which 8× matches). A/B the rest against
  `Dirt Splash.png` at the controls; the splash *height/timing* curves are authored data, not TUNE.

- `BL-290` `[Bug]` `[Blocked: CAP-28]` **Torpedo flight dynamics: the original's torpedo has a max/cruise speed and
  visibly slows after firing; ours flies the generic projectile model** (PT-38, 2026-08-06). Data
  check done: the decoded weapon block carries only `VELOCITY` (muzzle/flyout speed) and
  `ACCELERATION` (0 = constant velocity) — no drag or speed-cap field (`docs/formats/weapons.md`) —
  so this is engine behaviour to measure, not data to consume. Hypothesis recorded, not evidence:
  the torpedo may inherit the launching plane's speed and decay toward its own authored
  `VELOCITY`; a fast launch would then visibly slow, as a drop-torpedo physically should. `CAP-28`
  films it; measurement can reuse `analysis/video-flight-calibration` if the HUD is in frame.
  `wep_14` is mountable via `--rocket=wep_14` (no stock loadout carries it).

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

- `BL-092` `[Bug]` **No induced drag: a hard pull costs us no speed.** Measured, `--dump-flight`'s `zoom-climb` row:
  from 300 mph level at full throttle our full-pull apex arrives still doing **266 mph** where the
  original bottomed at **104 mph**. The same signal appears in the loop the clock was measured from
   — the original's loop spans 120–280 mph, so it bleeds most of its speed round one. Altitude gained
  is close (1450 ft vs 1635), the energy is not. This is the sharp form of the older "the original
  visibly bled speed in a sustained full-pitch 360°" observation, which can now be retired as vague.
  Wanted: a load-factor term in the drag, i.e. drag rising with commanded pitch rate / lift.
  **Unblocked and the magnitude is now measured — `CAP-01` decoded 2026-08-03.** Full throttle,
  stick full back throughout (pilot-confirmed), Bloodhawk, ~3,200 ft. The clip holds a **+100 ± 4°
  banked turn for 15.9 sim s** and sweeps **449.8°** of heading, so it is a true sustained
  equilibrium, not a transient:

  | segment | speed | dV/dt | altitude |
  |---|---|---|---|
  | cruise, pre-pull (7.6 sim s) | **298.96 ± 0.20 mph** | +0.06 mph/sim-s | +5.7 ft/sim-s |
  | bleed-in (5.6 sim s) | 237.2 mph mean | **−7.50 mph/sim-s** | +6.6 ft/sim-s |
  | **sustained turn (15.9 sim s)** | **222.94 ± 1.77 mph** | −0.35 mph/sim-s | −1.85 ft/sim-s |

  So a max-pull turn costs the original **25% of its top speed**, held indefinitely. The pre-pull
  cruise re-measures the full-throttle level equilibrium at 298.96 mph in a *different session* from
  the 300.4 mph in `FINDINGS.md` — 0.5% apart, which is what makes the comparison a clean A/B.
  **The number to fit: our drag law needs a further `+0.38 × maxThrustAccel` at this load factor.**
  At the plateau `x = V/fd = 0.7457`, so `lerp(x², x, 0.35) = 0.6225 A` of level drag against `1.000 A`
  of thrust; since the turn is level the gravity-along-path term is ~0, so the deficit is real
  along-path force. The bleed-in transient gives **0.369 A** by a completely different route (from
  its deceleration, at a different speed) against the plateau's **0.380 A** — 2.9% apart.
  Turn geometry, for keying the term: **18.95 °/sim-s** (fit residual sd 0.39°) at 222.9 mph, i.e.
  `V·ω = 32.96 m/s²` lateral = **1.65 × `nom_gravity` 20.0** (3.36 g at 9.81).
  ⚠ **Traps.** (a) The candidate data ships: `player.json`'s `highGs [9,15]` / `lowGs [-6,-9]` /
  `maxAOA 46` / `liftAOAs [5,9]` / `lift_accel_rate 0.75` are an angle-of-attack model we have no
  equivalent of — decode that before inventing a term (see `BL-095`).
  (b) **It must not slow the sustained pitch RATE**, which is measured flat across 120–280 mph and
  asserted by the suite: the original bleeds speed in a pull *without* losing pitch authority, so a
  naive "less speed ⇒ less pitch" coupling would break a passing check. **`CAP-01` sharpens this
  and may complicate it:** at max pull the *heading* rate is only 18.95 °/sim-s, well under the
  ~33 °/sim-s sustained pitch rate the loop gave and under the design ladder's bottom rung of 30.
  A compass reads the nose, not the flight path, so this is not simply path-lag — either pitch
  authority in a banked turn is lower than the loop implies, or bank/AoA geometry eats the
  difference. Do not assume the loop's flat pitch rate transfers to a banked turn.
  (c) **0.380 A is one point on the curve, not the curve.** Both segments sit at essentially the
  same load factor (`V·ω` 32.96 vs 34.02 m/s², 3%), so the clip pins the magnitude at max pull and
  says nothing about the exponent — a term in `n`, `n²` or `ω²` all fit it equally. A second
  capture at a *deliberately part-deflected* pull is what would separate them.
  (d) **The deficit is shape-dependent — quote the drag law with it.** Same plateau, other laws:
  pure linear needs +0.254 A, our β = 0.35 blend +0.378 A, pure quadratic +0.444 A, pure cubic
  +0.585 A (total drag 1.34× / 1.61× / 1.80× / 2.41× the level drag at that speed). This is the
  same fork `FINDINGS.md` flags on λ.
  (e) **Part of the 0.38 A may be thrust vectoring, not drag.** Thrust acts along the nose and drag
  opposes the path; a sustained AoA of α puts `1 − cos α` of it into the deficit (α = 25° ⇒ 0.09,
  a quarter of the total). The clip cannot measure AoA, so 0.38 A is honestly a bound on the
  *combined* along-path deficit — which is nonetheless exactly what a load-factor term must supply.
  (f) **A 100° bank that holds altitude is not something our lift model can do.** `liftFrac` scales
  by `wingVert = |Attitude.Y · Up|` ≈ 0.17 there, so we would shed ~83% of gravity across the path
  and drop; the original sinks at 1.85 ft/sim-s. Fixing drag without looking at this will not
  reproduce the manoeuvre — see `BL-247`.
  **Cockpit-confirmed 2026-07-30**, not just decoded from video: the user reports the original visibly
  slows through a sustained pitch pull, and climb bleed is stronger in the original than ours, from
  the controls — this is the felt form of the same gap, not a second finding.
  *Playtest after fix:* once a load-factor drag term lands, fly a full-pull 360° and a sustained climb
  and compare the bleed by feel before closing this.

- `BL-095` `[Research]` **`player.json` ships a physics block we consume almost none of.** Alongside the used
  `nom_gravity 20.0` / `stall_mag 1.25`: `maxAOA 46.0`, `liftAOAs [5,9]`, `lift_accel_rate 0.75`,
  `highGs [9,15]`, `lowGs [-6,-9]`, `drag_factor 1.5`, `drag_fade_speed 40`, `turn_fade_in 10`,
  `turn_fade_out 50`, `high_speed_pitch_fade [1000,1001]`, `yaw_low_speed 0.0625`,
  `yaw_high_speed 0.17`, `yaw_fade_in 10`, `yaw_max 50`, `yaw_fade_out 400`, `groundblow_elev 400`,
  `groundblow_mag 10`, `ai_groundblow 0.5`, and the `crash` block's `bounce_factor 0.6`. Units are
  unverified; decoding it deserves its own pass, and it is the upstream of two other entries here:
  the `yaw_*` fade set is the original's own speed-dependent yaw authority behind `BL-108`'s
  interim `eff` (`1.4 − clamp(v/fd)`, whose comment already admits it is "still not same as
  original"), and the AoA/G block (`maxAOA`, `liftAOAs`, `highGs`/`lowGs`, `lift_accel_rate`)
  gates `BL-092`'s induced-drag term.

  **Ground blow: DECODED — settled 2026-08-07 by the design document plus `CAP-02` (five batches),
  and `CAP-02` is closed.** It is a *designed* feature, not a shipped-only one: the GDD has a
  section on exactly this — §4.1.7 "Ground Blow", under Motion Model/Flight Dynamics → Simulated
  Elements (`tools/cs_gdd_extracted/cs_gdd.txt`, git-ignored; restated here, no prose). Ground blow
  is a proximity repulsion exerted by large dangerous objects — the named emitters are the ground,
  **cliff walls, and zeppelins** — that never overpowers the controls and never saves a head-on
  collision; the closer the plane edges toward an emitter, the more stick it takes to keep closing.
  So the design's mechanism is a bias on *control response* pointed away from the object, scaled by
  proximity — not an applied force, and not specifically about terrain below. Everything `CAP-02`
  measured and everything the pilot reported fits that shape:

  - **Lateral, away from the object.** A wings-level full pull straight at a cliff steps the
    heading **17° away at up to 34 °/s** (`Run4 Clip3`, level 250 ft at the cliff), while matched
    free-air controls flying the identical held pull — at 1,113 ft and at **90 ft over water** —
    move under 1°. Deliberate ~45° banked turns at the same speed (`Run 5`) peak ~3× faster, run
    ~10× faster sustained, and keep turning; the cliff event is a one-off heading *offset* that
    then holds. Displacement away from the obstacle, not a commanded turn.
  - **Nothing in the vertical plane.** Path-normal acceleration 0.91–1.03× free air and ADI body
    pitch rate 0.91–1.10× across every controlled comparison, including the same held full-pull
    loop flown at 1,113 ft and at 90 ft. (An earlier "1.95× pitch authority" claim here was
    retracted as a borrowed yardstick — the record is in `git log --grep=CAP-02`, and the
    measurement methodology and its traps live in `analysis/video-flight-calibration/FINDINGS.md`.)
  - **Keyed to closure with the object, not height.** The one shallow water dive starts its pull
    where along-path *range* crosses ≈400 (`groundblow_elev` 400) while altitude 399 ft shows
    nothing; a level run held at 165–336 ft for 7.9 s never trips it; canyon runs tripping
    `LOW ALT` continuously never trip it either — nothing is close *ahead* however near the walls
    are beside you.
  - **The cockpit reports match the design text point for point**: it only exists while pitch is
    held (hands off is a crash — "never overpowers the controls"); elevator only; body-frame
    (inverted, pulling *down* works the same — impossible for a world-frame force); an assist, not
    a clamp (`pull up to cras` still ends in the water — "never saves a head-on").

  **Why `CAP-02` closed without the owed re-film.** The last measurement gap was that a ~15°
  *transient* roll would be indistinguishable from a yaw step in a daylight clip (no daylight roll
  readout exists). The design text supplies the mechanism — a control-response bias away from the
  emitter — and the pilot flew the cliff pull wings-level with pitch the only input; read together,
  the heading step is that bias acting on the one input held, and a re-film would only re-measure
  what is now known. Footage stays staged under `playtest/CAP-02/` (git-ignored, main checkout),
  clip registry in `analysis/video-flight-calibration/extract.py`.

  ⚠ **Still open on ground blow — none of it blocks implementing.** (a) `groundblow_mag 10` units
  are unverified: magnitude is a TUNE, and the calibration target is the cliff clip's 17° step at
  ~300 mph. (b) `groundblow_elev 400` read as trigger *range* fits the one clean onset (bracket
  427 → 376 ft) but pilot timing is not excluded — a working reading, not a confirmation. (c) One
  anomaly stands: `Up Down` recovery #1 reads 2.30× free air with 28–30% of samples gated at
  γ ≈ −63°, suspected estimator artifact; every clip designed to reproduce it came back flat.
  (d) The design names **zeppelins** as emitters and `ai_groundblow 0.5` plausibly scales the AI's
  version — both untested, both free checks whenever a zeppelin mission is flown.
  Implementing ground blow itself is unowned follow-on work — mint a `[Feature]` item when it is
  scheduled.

  Still to decode in the block: the `yaw_*` set (`BL-108`), the AoA/G set (`BL-092`), the `turn_*`
  fades, `high_speed_pitch_fade`, `drag_fade_speed`, and `bounce_factor`'s units (`BL-172`).
  One GDD lead for the AoA/G set: the design's "Elements not Simulated" list explicitly excludes
  red-outs, so `highGs [9,15]` / `lowGs [-6,-9]` are read as the lift model's load-factor envelope,
  not pilot-physiology thresholds.
  ⚠ **Traps.** (a) `yaw_max 50` and `yaw_fade_out 400` are not in the same units as our `eff`
  — do not map names onto our terms without deriving the units, because our yaw 360° currently
  matches the original to 4% and a mis-scaled substitution would break a passing suite check.
  (b) `drag_factor 1.5` here **collides with** `vehicle.json`'s per-plane `drag_factor` (0.37 on the
  Bloodhawk), so at least one of the two is not what its name suggests; our drag uses neither.

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

- `BL-108` `[Research]` **The yaw `eff` speed shape** (`1.4 − clamp(v/fd)`) is an unvalidated interim model away from
  cruise: the 360° rudder turn matches the original to 4%, but that is one speed. The original's
  own version of this ships as `player.json`'s `yaw_*` fade set — see `BL-095`, which owns the
  `player.json` decode; the two closed halves of this entry went to `BL-092` (the
  original's pitch rate is measured **flat** with speed, and it bleeds speed in a hard pull because
  of induced drag rather than a falling pitch rate).

- `BL-115` `[Tuning]` `[Owed-playtest]` **Flight model** — `StallNoseRate`, `ClimbGravityScale`, `LowSpeedDragBlend`
  (`KnifeAlignFloor` moved to `BL-247`).
  **`PitchTune` / `YawTune` / `RollTune` / `ThrustConst` are not on this list**: all four are
  measured against the original frame by frame and asserted by the `flight-envelope` suite, so they
  are not TUNE knobs and a feel A/B cannot overrule them.
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
  - **`LowSpeedDragBlend` 0.35 gives 4–6× too much drag below cruise.** The same clip is a
    thrust-free drag probe: measured `D` is **0.36 / 1.11 / 2.82 / 3.74 m/s²** at x = 0.25 / 0.35 /
    0.46 / 0.50 against our **7.69 / 12.13 / 17.91 / 20.25**. The ratio survives every `(g, C)` pair
    the fit tolerates. The model-free form: engine off at 152.6 mph in a +5° climb the original
    decelerates at **6.24 m/s²** where ours would take 21.6. This is an independent confirmation of
    `BL-092`'s `x^2.67` — see that entry, which owns the drag-law rewrite.
  - **`KnifeAlignFloor` 0.35** — the footage gives the observable (path lags nose by 4.8° at +3 s,
    7.2° at +24 s, 8.3° at +36 s of knife-edge) but *not* the constant, because gravity is pulling
    the path down over the same interval and this clip cannot separate the two. **The constant now
    lives on `BL-247`**, which inherited the knife-edge attitude terms when `BL-124` closed.
  - **`ClimbGravityScale` 0.6 is NOT settled and `CAP-05` cannot settle it.** ⚠ The fit is
    degenerate: holding `g` and refitting leaves rms flat (0.218–0.280 m/s²) over `g` = 17…25 m/s²,
    with `C` = 1.18 / **0.59** / 0.00 at `g` = 17 / 20 / 25. That `nom_gravity` 20.0 lands on
    `C` ≈ 0.6 is a consistency, not a measurement — precisely the "confirms whatever you feed it"
    trap `FINDINGS.md` warns about. The 50%-throttle climb clip cannot help: its ADI **saturates**
    (sky fraction pinned at 0.730), so the nose angle is unreadable above ~+30° and the along-path
    thrust cannot be formed. *What would settle it:* an independent `g`, or a capture with a
    **readable** nose angle in a sustained climb — i.e. a shallow, held climb at fixed throttle
    rather than a zoom.
    **The mechanism itself is designed (GDD §4.1.1, read 2026-08-07):** gravity is pitch-angle-
    scaled by design — minor effect near level, stronger when climbing or diving, and *reduced on
    upward pitch relative to downward* so climbs stay flyable while dives still build speed.
    Existence, sign and shape of the asymmetry are design intent; only the constant's value is
    still unmeasured.

- `BL-120` `[Tuning]` `[Owed-playtest]` **Collision feel** — behaviour against building corners.

- `BL-147` `[Research]` **Pitch's spin-up: the premise was wrong, and the original's response is NOT a single
  first-order lag — which is exactly what we implement. Measured 2026-08-03 from `CAP-04`; the
  capture is discharged and retired, the item stays open pending an A/B against our own build.**
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
  **What remains open:** τ itself.
  **How to get it — a fixed-cadence key macro. 30 fps is a floor, not a target; record at whatever
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
  **Next, and it needs no new footage from the original:** run the identical cadences through our
  build via `--hold`, decode altitude the same way, and compare amplitude-for-amplitude. Same input,
  same measurement, same nonlinearity and same operating points — so the operating-point objection
  above disappears and no transfer function has to be assumed. Plot: `playtest/CAP-04/cadence_response.png`.

  The driver is `analysis/video-flight-calibration/pitch_cadence.ahk` (AutoHotkey v2) — its header
  carries the rig rationale, the re-flight rules (non-integer periods, the busy-spin timing) and
  the verified timing figures; the edge log it writes (`cadence-logs/*.csv`) is the deliverable as
  much as the video is. ⚠ For a re-flight: `extract.py`'s `LAYOUTS` knows only 2560×720 and
  2560×1440 and raises on anything else — declare a new geometry *before* recording. The rig's
  beeps are in none of the clips (Game Bar records game audio only); find the cadence window by
  matched filter on altitude, which is what the analysis does.
  ⚠ **Traps.** (a) `PitchTune` **0.75 is a pinned measurement** (see the flight-constants standing note at the top of this file) —
  and the cadence sweep leaves it untouched: it sets the **steady** rate, which still matches, while
  what the sweep refutes is the *transient shape*. It cannot move to fix a feel report — and it is
  emphatically not the knob for the roll-off mismatch. (b) **Do not quote a τ from `CAP-04`, from the
  loop clip, or from the cadence sweep.** The loop clip's fitted τ is smoothing-limited and falls
  monotonically as the window tightens (`FINDINGS.md`'s "a peak found by differentiating a smoothed
  signal is a smoothing artifact", in its exact form); the sweep's points are not at one operating
  point. The sweep refutes a *shape*; it does not fit a constant. (c) Don't fold this into `BL-097` — same shape of gap,
  different axis, and pitch's own coupling to speed (`BL-092`'s induced-drag gap) makes conflating the
  two easy to get wrong. (d) The digital-input finding is not pitch-specific — it means **`BL-097`'s
  roll question has the same defect**: there is no partial aileron deflection either, so a "moderate
  roll input" clip cannot be flown, and `BL-097` should be re-read as a held-key step question too.

- `BL-172` `[Feature]` **Graze pushback is entirely unmodelled — and the shipped data already has the constant to
  bind it. Plan-sized — not a TUNE.** `FlightController.SurviveHit` (`FlightController.cs:1435-1535`)
  only ever does a friction-scaled tangential slide (`GrazeFriction`), a lever-arm attitude kick
  (`GrazeKick`), and a fixed `GrazePushOut` (0.15 m) off the surface — there is no
  restitution/repulsion term along the normal at all. Meanwhile `player.json`'s `crash` block ships
  **`bounce_factor 0.6`** alongside `armor_damage_range [50,300]` / `health_damage_range [50,300]`
  (`docs/formats/vehicle.md:65,90-149`), and nothing in `CSVM/src` reads any of the three (`BL-095`
  flags the block's units as undecoded — grep confirms zero hits for `bounce_factor` under `CSVM/src`).
  *Fix shape:* add a restitution impulse along the contact normal scaled by `bounce_factor`, alongside
  the existing tangential slide — this turns "invent a pushback mechanic" into "bind the shipped
  constant." *Blocks:* the collision-feel sign-off.
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
  ⚠ **Traps.** (a) `bounce_factor`'s units are unverified — `BL-095` flags the whole `player.json`
  physics block as needing its own decode pass. `CAP-14` now supports reading it as a plain
  coefficient of restitution on the contact normal, but **0.6 is *consistent with* that footage, not
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

- `BL-247` `[Bug]` **The original holds altitude at 100° of bank; our `wingVert` lift model cannot** (decoded
  out of `CAP-01`, 2026-08-03). Measured off the ADI: the Bloodhawk rolls to **+100 ± 4°** — past
  vertical — and holds it for 15.9 sim s while altitude stays at 3217.5 ± 14.4 ft, sinking at only
  **1.85 ft/sim-s**. `FlightModel.cs:275` scales lift by `wingVert = |Attitude.Y · Up|`, which is
  **0.17** at that bank, so `gAcross * (1 - liftFrac)` would put ~83% of gravity across our flight
  path and drop the aircraft out of the manoeuvre entirely.
  This is the same `player.json` angle-of-attack block `BL-092` (a) points at — `maxAOA 46`,
  `liftAOAs [5,9]`, `lift_accel_rate 0.75` — seen from the lift side rather than the drag side, and
  the two should be decoded together: `CAP-01` is one clip that constrains both.
  **This entry also owns the knife-edge attitude terms** — `KnifeNoseSag` 0.07 rad, `KnifeNoseRate`
  0.2 rad/s and `KnifeAlignFloor` 0.35, all `FlightModel.cs` — inherited when `BL-124` closed
  answered on 2026-08-04 (`docs/HISTORY.md`). They are the same mechanism seen from the attitude
  side: `knife = 1 − |up·Y|` is `1 − wingVert`, so whatever replaces the lift keying has to replace
  these at the same time. The landed behaviour is nose −4°, path −10°, sink 19.4 m/s, 634 m lost in
  35 s, reached within a second of roll-in.
  ⚠ **Traps.** (a) **Do not "fix" this by flattening `wingVert`.** The same quantity drives the
  knife-edge nose-sag, whose *presence and direction* are proven by scripted test, so a
  bank-independent lift term would reproduce this turn and break that. Whatever carries the turn
  has to vanish by 90° *without* being a function of bank alone — pull/AoA is the obvious
  candidate, since knife-edge is flown near neutral stick and this turn at full back.
  **The knife-edge side had independently guessed the same fix** — "gate on actual bank instead of
  `1−wingVert`", because `knife` grows with pure pitch at *zero* bank and a full-pull zoom therefore
  loses ~11° of apex to a term that should not be firing at all. `CAP-01` is the first hard evidence
  that `wingVert` alone is wrong.
  **`CAP-05` (decoded 2026-08-04) now supplies the other end of the curve, and it confirms the
  hypothesis in (a).** Its two knife-edge takes sit at **+94…+104° of bank — the same bank as
  `CAP-01`, by the same ADI measure — but at near-neutral stick**, and the outcome is the opposite:
  the aircraft falls out of the sky (nose sagging without bound, sink reaching 93 ft/sim-s, 540 m
  lost in 38.9 sim s) while sweeping only **0.68 / 1.13 °/sim-s** of heading against `CAP-01`'s
  **18.95** — 17–28× slower, tracked off the compass tape at peak median 0.998.
  **So lift is not a function of bank.** A term keyed on bank alone must give these two clips the
  same answer, and they differ by a factor of 25 in turn rate and by everything in altitude. What
  carries the `CAP-01` turn has to be **pull / angle-of-attack**, exactly as (a) guessed — which
  also promotes `player.json`'s `maxAOA 46` / `liftAOAs [5,9]` / `lift_accel_rate 0.75` from
  "candidate data" to the most likely home for the term. Design the replacement against **both**
  clips: it must hold altitude at 100° bank under full pull, and must not at 100° bank with neutral
  stick.
  ⚠ One caveat on the pairing: the neutral-stick reading is inferred from the footage (the turn rate
  and the monotone nose sag both say no pull was held), not pilot-confirmed the way `CAP-01`'s "stick
  full back throughout" is. If it turns out the knife-edge takes carried some back pressure, the
  factor-of-25 gap narrows but does not close.
  **The knife-edge trajectory the replacement has to reproduce** (from `CAP-05`, both takes, at
  143 mph and 300 mph, agreeing to ~13% — so it is driven by time-since-roll-in, *not* airspeed):

  | time since roll-in | nose | path | sink |
  |---|---|---|---|
  | 0–3 s | −4° step, then drifting | ≈0° | **0.5 ft/sim-s — it genuinely holds altitude** |
  | +12 s | −12.0° | −6.0° | 24 ft/sim-s |
  | +24 s | −20.0° | −12.8° | 60 ft/sim-s |
  | +36 s | −27.0° | −18.7° | 93 ft/sim-s, still steepening |

  So the sag is an immediate **≈4° step** (fitted intercepts −3.4°/−4.2°, i.e. `KnifeNoseSag` 0.07 rad
  is the right *magnitude*) followed by an **unbounded linear drift of 0.69–0.89 °/sim-s**. ⚠ **Do
  not retune `KnifeNoseSag`/`KnifeNoseRate` to fit this — the shape is what is wrong.** A bounded sag
  cannot produce a linear 36-second drift, and raising the bound to 27° would destroy the first three
  seconds, which are the part we currently get *worst* and the original gets flat. Whatever replaces
  it must be near-flat at roll-in and unbounded after. Total altitude lost is nearly the same either
  way (540 m original vs our 634 m over ~35 s) — the shape is the whole difference, which is why a
  feel A/B on sink alone would have passed a wrong model.
  For `KnifeAlignFloor`: the observable is that the path lags the nose by **4.8° at +3 s, 7.2° at
  +24 s, 8.3° at +36 s**. ⚠ Do not back an align rate out of that — gravity is pulling the path down
  over the same interval and `CAP-05` cannot separate the two effects.
  *Playtest after fix:* inherited from `BL-124` and still owed, because the fix has not landed — (1)
  does the knife-edge sink feel like the original's; (2) does the full-pull zoom still feel nose-heavy
  (the `knife`-at-zero-bank leak above); (3) stall-into-knife-edge recovery should not feel "doubled".
  (b) The bank is read from the ADI sky-region centroid, which measured the 360° roll and is
  trusted for bank, but 100° is past vertical where the aircraft symbol painted on the ball is
  least helpful — treat "past vertical" as solid and the exact 100° as ±4°.
  Measured: the original settles at **137.9 mph** (0.459 × fd_speed) at 1/8 throttle and takes
  **7.04 sim s** to fall 290 → 150 mph. We settle at **93 mph** (0.309, and below lift speed, so
  ours is sinking rather than holding level) and decelerate in **2.47 s** — 2.8× too fast. Both are
  printed by `--dump-flight` as `(not asserted)`.
  ⚠ **Traps.** (a) **These two numbers cannot separate the two candidates.** We model thrust as
  linear in throttle; if the original's is not, the equilibrium moves with no drag change at all.
  Solving it as drag alone needs `x^2.67` at low speed, which contradicts the *other* reading in
  the same data (the acceleration's fall-off near fd_speed is steeper than a single power law fits,
  implying ~7.8 below fd against ~3.5 above — probably a soft governor near fd_speed). One more
  measurement discriminates: a level run at 1/4 and 1/2 throttle held to equilibrium. (b) The full
  throttle equilibrium is exactly fd_speed **for any drag blend** by construction, so it cannot
  detect a wrong shape here — the low-throttle end is the only place the shape is observable.
  (c) `LowSpeedDragBlend` 0.35 exists to answer a user report that a throttled-back plane barely
  decelerated; whatever lands here must not reintroduce that.
  **`CAP-05` (2026-08-04) breaks trap (a)'s deadlock from the drag side, with no thrust term in it
  at all.** `CAP-05 Stall 0% Thrust no input` is a zero-thrust deceleration from 159 to 70 mph, so
  `dV/dt` is drag plus gravity and nothing else. Measured drag is **0.36 / 1.11 / 2.82 / 3.74 m/s²**
  at x = 0.25 / 0.35 / 0.46 / 0.50, against our blend's **7.69 / 12.13 / 17.91 / 20.25** — 4–21×
  less, and the ratio holds across every `(g, climb-scale)` pair the fit tolerates. Model-free
  version: engine off at 152.6 mph in a +5° climb the original decelerates at **6.24 m/s²** where
  ours takes 21.6. Because the full-throttle equilibrium pins `D(fd) = A` (trap (b)), a curve this
  weak at x = 0.5 and equal to `A` at x = 1 **must be much steeper than quadratic below cruise** —
  which is `x^2.67`, arrived at here by a completely independent route. So the fork is resolved in
  favour of drag shape: the low-speed drag really is far weaker than ours, and thrust non-linearity
  is no longer needed to explain the 1/8-throttle equilibrium (137.9 mph ⇒ thrust(1/8) ≈ 2.8–4.5
  m/s², against 7.5 for a linear model — still sublinear, but only mildly).
  ⚠ Trap (c) is now the binding constraint, not a footnote: whatever replaces the blend cuts
  low-speed drag by a large factor, which is exactly the direction of the original user report. The
  fix has to come from the *shape* (a steeper exponent, so drag still bites approaching fd) and be
  re-playtested against that report specifically.

- `BL-271` `[Tuning]` `[Owed-playtest]` **The survivable-graze and stop laws are invented physics with player-facing
  consequences** (`FlightController.cs:271-295,1652-1672`, header "all TUNE"): attitude kick
  `GrazeKick` 1.2 rad/s, `GrazeFriction` 0.35, quadratic severity damage, "sliding below
  `GrazeStopSpeed` 12 m/s = destroyed", "3 failed embed push-outs = explode". The original
  might throw the nose differently or let a plane belly-slide to a stop ("collecting 0-dmg
  kisses" is the user report that motivated the stop rule). `CAP-14`'s analysed graze
  (2026-08-04) already bounds part of this — the original's graze cost ~5% speed + sink with
  wings level, no visible attitude kick at that severity. Judge the kick and the stop rule
  against that footage and `BL-120`'s corner feel item before tuning further.

- `BL-306` `[Feature]` **Engine torque is a designed, one-sided turn assist — unmodelled.** GDD §4.1.8
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

- `BL-307` `[Feature]` **Pitch-down on aileron roll is designed — unmodelled, and measurable from footage
  we already hold.** GDD §4.1.5: a roll carries a "small but noticeable" nose-over effect. We model
  no roll→pitch coupling. Before inventing a constant, measure it: the decoded 360° aileron-roll
  capture (manoeuvre #4, `analysis/video-flight-calibration/`) should show the nose-over as an
  altitude/ADI dip across the roll — if it cannot be read there, it is too small to model and this
  closes as won't-do. ⚠ Distinct from `BL-097` (the roll's spin-up *rate* shape); this is the
  cross-axis coupling.

- `BL-308` `[Feature]` **Ambient turbulence is designed and absent.** GDD §4.1.10: subtle, random jostling
  of the player's plane to sell moving through air — explicitly zero effect on speed, heading or
  performance. Visual-only is exactly the contract of the `PlaneShake` oscillators
  (`src/Flight/PlaneShake.cs`, visual-only roll on `ShakePivot`), so it slots in as one more
  source. Check `shakes.zrd.json`/`docs/formats/shakes.md` for an ambient source before inventing
  one; if no data carries it, magnitude and cadence are a TUNE against feel. Low priority; pairs
  with `BL-266`'s open shake data questions.

## Environment & world

- `BL-036` `[Research]` `[Blocked: zone selection unknown]` **We ignore `zone_id` entirely** (found 2026-07-22). Every gamez node carries a `zone_id`:
  `-1` = always rendered, `1`/`2`/`3` = only when that zone is active. Both zones span the **whole
  map** spatially, so they are alternative world variants, not regions. Per-chapter node counts
  (`-1` / zone1 / zone2 / zone3): C1 3529/2666/869/—, C1B 3500/2101/2/—, C1C 4181/146/1317/—,
  C2 4189/766/1/—, C2B 3338/149/1414/—, C3 3759/1647/2/—, C4 5330/802/2157/—, C5 9734/1555/—/149.
  So C1B, C2 and C3 are effectively single-zone (1–2 nodes in the second); C1C, C2B and C4 are
  zone2-dominant; C1 and C5 zone1-dominant. **Nothing in `CSVM/src` reads the field** — we render
  every zone's geometry at once. Plausible source of artifacts; not yet shown to cause a specific
  one (checked and ruled out for the C5 ground z-fight, where both surfaces are `zone_id=1`).
  **Also checked and ruled out for `BL-250`'s C5 doubled clutter (2026-08-04):** the repro node
  (`g4664`, `analysis/item9-depth-bias/CBLOCK-LOD.md`'s clean case) carries `zone_id=1` for the
  *whole* node while hosting both the `cblock1` subface polygon and the `cblock4` base polygon —
  one node-level value cannot separate two polygons on the same node. Broader: C5's `cblock1/2/3`
  nodes span `zone_id` −1/1/3, `cblock4/5/6` nodes are only −1/1 (never 3) — no clean split between
  the two districts either way, so `zone_id` is not the missing filter there.
  **Documented 2026-07-22** (polish-3 item 2) in `docs/formats/world-structure.md`, counts
  re-verified against `nodes.json`. **Blocked on the same unknown as the fog zone:** which zone a
  mission activates is in no file in the install (exhaustive negative result now written up in
  `docs/formats/weather.md`), so implementing this means *guessing what to hide* — and a wrong
  guess deletes visible world content, which is strictly worse than drawing both. The user's
  zone A/B has since answered **C5 = zone1** (2026-07-22), which would mean hiding C5's 149
  `zone3` nodes — but that is exactly the guess-what-to-hide risk, and the *fog* answer does
  not license a *geometry* change. C1–C4 are still unanswered. Do not act on this until the
  remaining chapters are settled and there is a visible artifact it demonstrably fixes.

- `BL-037` `[Research]` **`WorldPartitionSetActive` is unimplemented and undescribed — a C3-only mechanism.**
  Measured: **all 25 uses are `support\c3\*.gw` — C3 only** — and it takes rectangle coordinates,
  not node names. It never appears in any C5 script, so it is NOT the C5 ground-LOD mechanism —
  that is the **subface flag** (`analysis/item9-depth-bias/CBLOCK-LOD.md`; the clutter side of it
  closed as `BL-250`, `git log --grep=BL-250`) — and not "the runtime system that picks between
  coarse and fine ground", a claim this entry once made and retracted (⛔ 2026-07-23). It is a C3
  question, not a ground-LOD one.

- `BL-038` `[Feature]` **`FogState` is a decoded animation event we do not act on** (found 2026-07-22). Fog **can** be
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

- `BL-071` `[Feature]` **Skybox colour grading.** No tint, grade or tonemap is applied to the skydome anywhere —
  `WorldBuilder.BuildHorizon` only disables shadows, billboards the moon and disables light
  range-fade, and the `WorldEnvironment` sets background/ambient only. The dome does get the shared
  per-mission scalar dim `csky_world_light`, which is brightness, not grading. The decoded
  per-mission cloud tints are parsed and deliberately parked (`Weather.cs`, "unused this
  milestone") — they are the obvious input if this is picked up.

- `BL-075` `[Feature]` **Sky UV scroll** (`h_zone*scroll`) — scroll rate unknown, not implemented.

- `BL-076` `[Feature]` **Star twinkle + undecoded light fields** (flags 523/…, the 0.17 float) — stars/beacons
  render as fixed-size soft sprites, no twinkle.

- `BL-100` `[Research]` **Which weather/sky zone do C1, C1C, C2B and C4 actually use?** **C5 is
  answered — `zone1`** (user A/B 2026-07-22; landed as polish-3 item 2). **C1B, C2 and C3 are
  answered — `zone1`**, settled from the data rather than by A/B (2026-08-06, `BL-277`): their
  gamez `horizon/zone2` is a bare marker with no dome at all, so the geometry decides it and
  `WeatherState.PreferPopulatedHorizonZone` now selects it (`docs/formats/weather.md`).
  **What stays open is the four chapters where BOTH zones build a dome** — C1, C1C, C2B, C4 — and
  there the choice really is a fidelity call the geometry cannot make. ⚠ **The "C1 first, its own
  scripts disagree" ranking is withdrawn** (2026-08-06, `interp.json` re-read in full): its two
  strings are not two zone selections — `load.gw`'s `CameraSetHorizonXZ zone2_cloud_floor` names a
  node **C1's gamez does not contain**, and `tex_fx.gw`'s `FindNode h_zone1scroll` is a UV scroll,
  not a pick. All four zone-bearing strings in the 98 scripts are accounted for in
  `docs/formats/weather.md`; none bears on zone choice. So this is a plain four-chapter sweep, with
  no data reason to order it. C1 is still the most *visible* case (its two zones are genuinely
  different skies — zone2 moon/stars night, zone1 day haze). Method: fly each candidate in the
  original and compare against `--sky-zone=zone1` / `=zone2`, which still render a named zone
  literally.

- `BL-101` `[Tuning]` `[Owed-playtest]` **Fine-tune fog and environment** — method: record video from a spawn point flying straight for a
  fixed number of seconds, in both engines, and compare.

- `BL-105` `[Research]` **Map-edge continuation — the mirror half is ANSWERED 2026-08-04 from `CAP-17`; the unit
  size is not.** **The original mirrors.** Our alternating reflection in `MapEdgeExtender.MirrorAxis`
  is correct and must NOT be swapped to plain repetition — the long-standing user belief that it
  plain-repeats (NOTES.md) is withdrawn, and the class doc's "user believes the original does NOT
  mirror" caveat with it.
  **How it was measured** (`playtest/CAP-17/`, method and traps in its README). One take,
  `CAP-17 C2 south.mp4`, 67.6 s of straight nose-view flight south over the C2 coast. For a fixed
  screen row, that row from all 2027 frames is stacked into a **spatio-temporal strip**, so the
  land/water boundary (keyed `R − B > 15`) draws the coastline along the whole flight path. Read at
  five rows (900–1300):
  - **Translational period 471 ± 5 frames**, NCC **+0.90…+0.95**; consecutive periods are identical
    copies (as-is +0.899…+0.949 vs time-reversed −0.086…+0.080).
  - **Reflection seams every 240 ± 2 frames** at NCC **0.89–0.94** — row 1200 at 896/1138/1378/1619
    (spacings 242, 240, 241), row 1300 at 916/1157/1398/1637 (241, 241, 239). Translational period =
    2 × seam spacing, which is exactly what alternating reflection produces and plain repetition
    cannot.
  - The seam crosses **later on nearer screen rows** (row 900 → 1300: frame 1250 → 1398, monotone) —
    the signature of a real ground feature, which no camera artefact can fake.
  Heading, speed and altitude were ruled out first: compass tape moves **4 px total** over the clip
  (corr with the coast trace −0.067), airspeed is flat at **295–302** units/sim-s (sd 2.7, matching
  the 299.0–300.4 level max), altitude excursion is **218 ft** total and flat after frame 800 (corr
  +0.19). That matters because a straight coast's screen-x scales as 1/h.
  **What is still open: the size of the mirrored unit — it is NOT one 1024 m cell, and (measured
  2026-08-07, second map) it is NOT one universal cell count either.** The C2 seam
  spacing is 240 frames = 8.006 wall s = 11.13 sim s at k = 1.390, i.e. **3.28–3.36 km ≈ 3.2 cells**
  at the measured speed. A one-cell unit is excluded by ~3×, and directly: translation NCC decays
  smoothly through the lag a 1024 m cell would occupy (lag 74 = +0.353, lag 111 = −0.038) with no
  peak there. Two independent supports that our per-axis *border-cell* clamp is wrong: C2's own south
  border row is nearly all water (coast between cols 8 and 9 of the 12×12 × 1024 m grid), so
  repeating it southward would give a coastline **invariant in z** — a straight line, not the
  observed 471-frame swing.
  **C4 north measured 2026-08-07** (`playtest/CAP-12/c4-mapedge/`, reusing the CAP-12 file
  `CAP-11 C4 and CAP-12 Clouddeck.mp4`, t 72–114 s: due-north flight over C4's river, compass
  scroll 0.0 px, airspeed flat 298.5–299.6, gate dx=dy=0 peak 0.848). The river's
  water-fraction/x-centroid traces repeat at a **translational period of 333.5 ± 6 frames**
  (NCC +0.555; −0.30 at the half-lag), reflection seams at half that, and consecutive episodes
  match better **time-reversed** (0.648/0.678 vs 0.563/0.587) while episodes two apart match
  better as-is — alternating reflection again, on a second map and axis. Unit:
  **2310 ± 60 m ≈ 2.26 cells** — not C2's ~3.2 cells, so the mirrored block is per-map (or
  per-edge), and any fixed `Rings`-style constant is the wrong shape. Noted, unproven: both
  measurements sit on **n + ¼ cells** (2304 m and 3328 m); a single k·V systematic cannot make
  both integers (+12.5 % vs +7 % needed), so if the pattern is real it is about where the
  mirrored block is anchored, not a scale error.
  ⚠ **The cell count is the soft number, the mirroring is the hard one.** The metre conversion
  inherits both V and k, and k = 1.390 is a machine/session property measured on *other* clips, so
  read each unit as soft ("about three cells" on C2, "about two and a quarter" on C4 — definitely
  not one, definitely not equal) rather than exact integers. Settling it needs either a level
  constant-altitude pass with a known start position, or an A/B against our own
  build once `Rings`/the clamp granularity is changed.
  **Extent:** the clip covers ~28 km ≈ **2.3 × the 12,288 m map** and the mirrored tiling continues
  undegraded to the last frame — no limit, no change, no fade found within that range.
  ⚠ **A symmetric border feature cannot discriminate mirror from repeat, and a zigzag coast is
  locally symmetric about every headland.** The 2026-07-22 open-ocean check was inconclusive for the
  first reason; a reflection scan with too small a half-width fails for the second (half-widths
  30/37/55 return spurious seam spacings of 31/31/90 against the true 240). Use a half-width of a
  full half-period.

- `BL-118` `[Tuning]` **Cloud deck** — the `CloudDeck` **mesh** brightness reads ~40 units lighter than the
  original (measured 2026-08-07: **+54, and only from below** — see the `CAP-12` block) — **plus a
  post-`BL-273` density judgement of the now-authored ambient cloud field.**
  ⚠ **RE-SCOPED 2026-08-06, `BL-273` landed.** Everything this item used to say about the sprite
  field went with `CloudPuffs.cs`: the field is now `fogvol.zrd`'s authored clutter scattered
  through the gamez `fvol*` volumes, and it carries no TUNE constant at all
  (`docs/formats/fogvol.md`). Both 2026-07-30 symptoms are answered — "puffs at all height levels"
  was the deleted `BandBelow`/`BandAbove` (120/280 m) + `VertFull`/`VertFade` (200/560 m) margins,
  which seeded visible puffs across [290 m, 1964 m] of a 2003 m envelope; "not on every map" is
  authored (C1B/C2/C3 ship no fog volumes and no `cloudsprite*` template). What is left here is
  (a) the deck mesh's brightness, which `fogvol` never explained, and (b) whether the authored
  field's density and look match the original at the controls.
  *Specific thing to judge (b) on:* at a grazing angle the 130 m scatter grid is visible as a faint
  comb in the cloud sheet — a 10–20 m `perturb_dist_range` on a 130 m `distance` is only ±15 %
  jitter. That is what the authored numbers produce under the documented grid reading of `distance`
  (fogvol.md, "What is decoded and what is inferred"); if the original shows no such structure, the
  reading of `distance` is what to revisit — **not** a new tuning constant.
  **`CAP-12` delivered and analysed 2026-08-07** (evidence in `playtest/CAP-12/`, decode via the
  chase pipeline — gate dx=dy=0 peak 0.758, altimeter NCC 0.9900). What the footage settles:
  - **The deck band is the authored `fogvol` slab.** Full whiteout spans **3290–3560 ft
    (1003–1085 m)** across six crossings, top edge 3560 ± 6 ft over five of them; first wisps at
    ~3222 ft (982 m), clear above by ~3700 ft. Authored slab: 970–1090 m — congruent to within
    metres, so deck placement is data, not a constant to tune.
  - **(a) is confirmed, signed, and localized to the underside.** Matched-box A/B against our
    build at the same altitudes (`--pos` shots, same 480×110 game-coord box): deck from below
    original **167** vs ours **221** (+54, ours too bright); inside 248/243; tops from above
    211/214; from 5570 ft 196/202. The interior and tops already match within a few units —
    only the base lighting is wrong. Original base: flat dark-gray sheet, soft mottling; ours:
    white, top-lit, hard-edged crenellation.
  - **The original shows no comb.** Grazing passes along tops and base (stills t=44/59/97/124,
    t=29.2) show soft continuous structure only — no 130 m lattice at any angle. Whether *our*
    field shows one at the controls is still `PT-42`'s check; the original side is now on file.
  - **Night puffs are directionally moonlit in the original (`CAP-11` C1B, 2026-08-07):** cloud
    cores near the moon reach p90 **218** (t=16) while the away-from-moon cloud sits at p90 **70**
    (t=5); our uniform `WorldLight` 0.426 renders p90 **102** — matching the away side and ~2×
    dark against the lit side (`playtest/CAP-11/`). Any night-cloud brightness judgement must say
    which side of the moon the measured puff faces.
  - ~~Discrete puff balls above the tops in ours, none in the original~~ — **explained, not a
    defect (user, 2026-08-07): the placed/scattered puffs exist only over the base map**, and the
    clip had left it (a straight run at ~300 mph covers the 12,288 m map in under a minute),
    while our `csvm-above-1160m.png` sits at (-4974,-3861) — on it. The edge extension carries
    no puffs in the original; same phenomenon on C2's world-placed puffs. Any future above-deck
    A/B must say which side of the map edge both frames are on.
  **C4 take analysed 2026-08-07** (`CAP-11 C4 and CAP-12 Clouddeck.mp4`, gate dx=dy=0 peak
  0.848, d2 sd 0.99 ft; evidence in `playtest/CAP-12/c4/`) — the clear-air chapter separates
  what C1's murk hid, and it bears on the **uniform-vertical-fill inference** in
  [`docs/formats/fogvol.md`](docs/formats/fogvol.md):
  - **C4's authored numbers put the puffers above the deck, uniquely among chapters.** Deck mesh
    y=1050 (`Sky1.tif` tiles), `CLOUD_COVER` 1000–1100 (colors authored 192-gray), `fvol` slab
    **1060–1180.5** — the slab tops out **80 m above** the cover band (C1's 970–1090.5 nests
    inside its 970–1124). Puffs riding visibly above the deck are data, not a bug.
  - **The user-reported gap is real in the footage, and uniform fill cannot produce it.** At
    1135 m (t=19.5) the original flies in *clear air* — gray sheet below, puff bases above; the
    whole climb 1003→1230 m never fully obscures (lum ≤ 195). Under uniform fill the 132.3 m
    cards (scale ≤1.5) hang to ~956–990 m, piercing the deck — no gap is expressible. Our build
    at the same spot (`csvm-c4-1135m.png`) sits in murk, and at 1050 m
    (`csvm-c4-1050m-deck.png`) renders a **243 whiteout where the original's obscuration is a
    GRAY-out** (user, 2026-08-07): C4 authors `CLOUD_COVER` `TOP_COLOR`/`BOTTOM_COLOR` =
    192,192,192, and the original's veil measures exactly that flat 192 — so C4 carries a
    *second*, in-cloud brightness delta (243 vs 192, +51) on top of the C1 underside one, and
    the target value is authored data, not a judgement call. A **top-anchored** scatter
    (centres near the volume top, perp jitter) puts card bottoms at ~1076–1127 m — a 30–75 m
    clear band over the sheet, which is what the clip shows. C1 cross-checks: top-anchoring at
    1090 predicts bottoms ~991–1027, matching the measured whiteout onset 1003 m / wisps 982 m.
  - ⚠ Confound to keep separate: C4 also ships **45 `cloudparent` clusters parked at the world
    origin** in gamez (runtime-placed by mission setup, altitude not in `nodes.json`); the big
    cumulus towers at 1200–1600 m in the same clip are likely those, not `fvol` scatter.
  *Fix shape:* revisit fogvol.md's vertical-spread inference (anchor at/near the volume top
  rather than filling it), per this entry's own rule — an inference correction, not a TUNE.
  *Playtest:* `PT-42` ([`playtest.md`](playtest.md)) keeps the at-the-controls look judgement.
  ⚠ **Trap.** The "~40 units lighter" brightness reading is about the `CloudDeck` **mesh**, a
  different object from the sprite field — do not read one as evidence for the other. (The +54
  measurement above is the mesh underside; the sprite field sits *inside* the whiteout band.)

- `BL-165` `[Feature]` **The sun renders no lens flare; the original does — layout now decoded from `CAP-13`.** Confirmed absent: `Launcher.cs:569`
  builds only a plain `DirectionalLight3D` (`Sun`) + a `WorldEnvironment` with no glow/bloom
  configured. Grepping `CSVM/src` for `flare`/`glow`/`bloom` turns up only the wingtip nav-lights
  (`WingLights.cs`), world lamp/beacon glow sprites (`gen_flare_yellow`, `poleflare`,
  `docklight_flare` — `SceneBuilder.cs:771`, `WorldBuilder.cs:403`), and gun/rocket effects — nothing
  tied to the sun.
  *Candidate asset, engine-bound (upgraded 2026-08-04):* every chapter's texture archive ships a
  lens-flare-shaped set — `bigflare01`/`bigflare02` (large core discs) + `lflare1`..`lflare4`
  (small secondary rings) — and the interp boot scripts **register the small set by verb**:
  `support\c2\init.gw` and `support\c3\init.gw` each carry `LensFlareTexture 0 lflare1` …
  `LensFlareTexture 3 lflare4` (plus `LightMapTexture lightmap`), binding `lflare1`–`lflare4` to
  flare element slots 0–3 in that order. No gamez node, material, or cam_anim/zrdr def references
  them, consistent with a hardcoded screen-space effect fed by these registered textures. Open
  oddity: only C2/C3 carry the lines — the other chapters' `init.gw` scripts register nothing, and
  the user's CAP-11 chapter sweep (2026-08-07) **found a visible sun only in C3**, so "flares
  everywhere" is no longer presumed; `bigflare01`/`02` remain a naming-convention lead only.
  *Measured spec (CAP-13, `CAP-13 C3.mp4`, analysed 2026-08-07 — full numbers and stills in
  `playtest/CAP-13/README.md`):* **no streaks** — the flare is exactly four elements: a
  saturated-white core glow at the sun (half-max dia ~63 px of 1280×720, blue-cyan skirt) plus
  three thin blue-white rings (annulus RGB ≈ 194,226,254) strung along the sun→screen-centre
  vector at fractions **0.50 (dia ~102), 0.90 (dia ~45, brightest), and 2.0 (dia ~164,
  faintest)** — fractions verified ±0.03 at two poses (t=17.0, t=19.5). On top of it a
  **full-screen white wash**, opacity ~linear in the sun's screen distance from centre:
  α≈0.66 at 30 px → 0.40 at ~180 px → 0.13 at 350 px (dark fuselage 38,2,9 → 179,174,173 at
  max). The whole rig pops in complete when the sun core enters the frame and fades out in
  ~0.1–0.15 s as it exits (t=19.75–19.85); with the flare off, the sun itself stays visible
  as a plain pale-yellow billboard disc (`CAP-11 C3 2.mp4` t=20–48).
  *Occlusion & layering (CAP-13 + `CAP-11 C3 2.mp4` + user live tests, all 2026-08-07):*
  the two halves gate **differently**. The **sprites** (core + rings) are killed by terrain
  occluding the sun and by the own plane *fully* covering it — but a *partial* plane cover
  changes nothing (t=18.5), and billboard sprites don't occlude at all (volcano eruption
  puffs drifted across the sun: no visible change). The **wash** survives terrain occlusion
  outright and stays on its unoccluded trend under partial plane cover. Layer order measured:
  world < flare sprites < HUD/cockpit (compass clips the core, t=6.4) < wash — the wash
  whitens the HUD itself at the same α as the world (compass 20,20,18 → 178,180,177 at max).
  *Fix shape:* a screen-space flare rig keyed off the sun's view-space direction, per the
  spec above: sprites drawn under the HUD, gated by a line-of-sight test against solid
  geometry (terrain, own plane) and by the sun-centre-on-screen test, both with the quick
  ~0.1 s fade; the wash drawn over the HUD, keyed only on screen-centre distance, ignoring
  geometry occlusion.
  ⚠ **Traps.** (a) Do not confuse `gen_flare_yellow` (a town-lamp light-source glow node) with this —
  same texture-naming family, unrelated purpose. (b) The footage fixes the element inventory
  (core + 3 rings) but not which texture feeds which element — `bigflare01`/`lflare*` remain a
  naming-convention lead; match render output against `playtest/CAP-13/` stills, not the asset
  names. (c) Nose/cockpit-camera flare behaviour was still being tested by the user when this
  was recorded — check with them before assuming the chase-cam layering holds there. Full
  record: `playtest/CAP-13/README.md`.

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

- `BL-251` `[Research]` `[Blocked: CAP-23]` **Does the original draw water over the shoreline, or the shoreline over the water?** Our cross-node draw order is "the later gamez node wins" — `nodes.json` is
  a depth-first serialization, which is the original engine's own draw order — and the dense
  conflict rank (`BL-053`, landed 2026-08-04) now enforces that decisively where it used to be a
  near-tie. At the C1B pose `analysis/item9-depth-bias/FINDINGS.md` §7 recorded, that means
  `wtr00000` (node 743) draws **in front of** `srf0001` (node 716), and it now wins by 9× the
  margin it used to. The rule itself is well evidenced; what is NOT evidenced is that this
  particular pair looks right — §7 flagged it as the case where "the flicker metric improves while
  the picture gets worse" would be invisible to us.
  ⚠ **Traps.** (a) The data cannot settle it: "later node wins" is the documented rule and it says
  water. Only a capture of the original decides. (b) If the capture says surf-over-water, the fix is
  **not** to invert the tie-break — that would break every other pair the rank now gets right
  (666 install-wide); it would mean the two nodes' authored order carries something we are not
  reading. (c) Do not judge it from our own render at a single frame — before the fix this pair was
  swapping winner on 1.76% of the frame under a 1 mm camera move (`verification.md` INSTR-8), so
  screenshots taken before 2026-08-04 show an arbitrary winner, not a decision.
  *Playtest after fix:* `CAP-23` (water vs shoreline order, `playtest.md`).

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

- `BL-303` `[Bug]` **Three scenes render permanent murk or a black sky the original doesn't have —
  and all three sit against zones authoring a 9000–10000 m fog band** (`CAP-11` A/B, 2026-08-07;
  evidence `playtest/CAP-11/README.md`; surfaced closing `BL-110`). The cases:
  - **C3**, matched canyon pose (`--pos=-3504,710,-3619 --direction=-0.40673,0,-0.91355`,
    identical 2329 ft both sides): original near slope 36.5, far hills 19.9–60.3, blue-gradient
    sky 194.9; ours near slope 130.4 (×3.6), far hills flat **201.0 = full `c9c9c9` fog**, sky =
    fog. The authored near 1000 / far 4500 cannot full-fog a hill 1–2 km out, and C3's
    CLOUD_COVER sits at 10000–11000 m — this is not the whiteout band.
  - **C2B above the deck**: original dark blue-gray dome 82.7 (t=50); ours flat b0b0b0 fog
    **176.0** at 1230, 1350 and 1500 m — identical at every altitude, so not an altitude miss.
  - **C5**: our sky pure black 0.1 vs the original's dark-blue night dome 15.3 (C5 authors fog
    000000; the `Weather.cs` NoFog-fallback note is adjacent).
  Every *healthy* scene's active zone authors either a reachable deck band (C1C/C2B zone1 ~1000,
  C2 zone1 256–1024) or 10000–11000 (C1/C1B zone1) — how the fog-altitude fade treats a band
  wholly above the flight envelope is the common suspect; start where `ZoneFog`'s
  fog_low/fog_high are consumed. Adjacent from the same capture, probably NOT fog: C5's lit
  facades read ×0.58–0.66 of the original (tower faces 10.2 vs 15.5, low-rise 21.7 vs 37.6)
  with WorldLight already at clamp 1.
  *Playtest after fix:* re-shoot the three poses named in `playtest/CAP-11/README.md` against
  the same original stills.

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

## Effects & animation runtime

- `BL-032` `[Feature]` `[Blocked: user decision]` **Burning-object fires (`fire1`/`fire2` templates + `EFFECTS` flipbooks)** — **POSTPONED
  2026-07-21 by user decision: minor detail, and the trigger is not findable.** Fully decoded,
  so nothing needs re-deriving; what is missing is *when* to start a fire, not how. Blocked on
  a decision, not on data. Decode in `docs/formats/anim-definitions.md` ("Fire: templates,
  flipbooks, and a trigger that lives in the exe"):
  - **Templates:** `fire1`/`fire2` are real single-poly `Facade`/`CylindricalY` meshes under the
    **parentless roots** `fire1.flt`/`fire2.flt` (C1 nodes 493–496), which `WorldBuilder` never
    builds (it builds only World children + partition-referenced subtrees). Same for the other
    effect roots (`large_firetrail`, `short_firetrail`, `lg_fireball`, … ~gamez idx 74–150).
  - **Flipbook:** `effects.zrd.json` gives `fire1` 12 maps @ 10 fps, `fire2` 6 @ 5 fps, resolved
    **by filename from the texture archive** — `textures.json` registers only `fire101`/`fire102`
    while `extracted/<ch>/texture/` ships all twelve `fire1NN.png`. `TextureCycler` already plays
    frame lists, so this is small *once the templates are built*.
  - **EFFECTS is node-keyed, not texture-keyed** (user-confirmed: a *sustained* muzzle flash never
    changes texture, always `fire101`). So `flame01` — the refinery gas flare, sharing material 88
    with the `fire1` template — is a **static base flame**, and the animated fire the user sees
    there is a **placed `fire2` instance** (6 frames @ 5 fps, matching their independent read).
  - **Why it is blocked:** the four `fire.zrd.json` behaviours (`timed_big_fire`,
    `persistent_big_fire`, `persistent_small_fire`, `timed_small_fire`, all anchored on
    `fire2.flt`) are called by **nothing** — their names appear in exactly one file, their own,
    and `CALL_ANIMATION` references animations by name string only (no index form exists anywhere
    in this data). **User searched the disassembly 2026-07-21 and found no trigger either.** So
    the original starts them engine-side by a condition we cannot recover; reproducing them means
    inventing our own trigger, which is a fidelity guess rather than a data-driven port.
  - **If resumed:** the placement half already works — `CALL_ANIMATION`'s target parameter landed
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
  falls back to **1** sprite per `TIME_INTERVAL`. That fallback is a guess at the original engine's
  default, not decoded data: the sibling `large_10sec_fire` authors `NUMBER 3` from otherwise
  comparable values, so the real default may well be higher and every unnumbered emitter in the game
  correspondingly thin. Judge the density at the controls now that the fire's *shape* is right
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
  still-flying set) on a longer one. Unmeasured against an actual in-cockpit pass — the playtest
  this pool wants is the same one `BL-253`'s own owed playtest already asks for.

- `BL-245` `[Bug]` `[Blocked: ground ray]` **The other 379 bounce-terminated `OBJECT_MOTION`s are FALLS, not launches — no apex to
  solve, and a live `water`/`lava` surface table to choose between (split out of `BL-240` when the
  census separated them, 2026-08-02).** Same authored idiom as `BL-240` — no `RUN_TIME`, a
  `BOUNCE_SEQUENCE` naming the landing — but these start at rest or head downward, so
  `BL-240`'s return-to-launch-height solve yields `t = 0` for every one of them and leaves the bug
  exactly as it is today. Three shapes, censused over all 17,568 extracted defs:
  **335** `translation initial=(0,0,0)` with gravity −9.8 — a shot-down `gasbag1` or `cargozep1`'s
  `crashnode1` sinking to the ground, bouncing into `hit_ground1` or `hit_water1`; **~17** thrown
  downward at elevation −70…−90° (`lifesaver11`'s `lifeboat` → `boat_explode`/`boat_explode_water`,
  `b_turret1`'s parts); and **8** `chuteman` at `translation (0,−3,0)` with **gravity 0** — a
  constant 3 m/s descent, no parabola at all, ending in `deactivate_chuteman`.
  **A fourth group joined on 2026-08-06, from `BL-257`'s census** (`analysis/bl-257-nulled-launch/`):
  **8 events that name no `BOUNCE_SEQUENCE` either**, so the 733-event census above never counted
  them — `susp_bridge`'s `rope1burn` `part2`/`3a`/`3b`/`3c`/`part4` (`translation.initial.y`
  −0.44…−1.0 with `rnd_xz.y` ±0.89…±1.0, gravity −9.8: a burning rope end dropping, and the user
  confirms at the controls that it visibly falls in the original), `bridge_destroy01`'s
  `bridge_truck01` (`initial.y` exactly 0, no spread — level), and both `fuelboxbreaks` `rockerarm`s
  (elevation 90° but speed **−45…45**, so half the draws point down). `BL-257`'s widened gate admits
  by APEX, so these are declined by the same `FlightToLaunchHeight` guard and stay posed at rest.
  ⚠ For the spherical `translation_range` form the vertical speed is `sin(elevation)·speed` — **a
  negative speed inverts an upward elevation**, which is why the rockerarms are not solvable
  launches; any later census of this family must read the speed range, not the elevation alone.
  **Blocked on a ground ray**, and blocked on it twice: the fall distance is unknowable without one,
  and unlike `BL-240`'s 150 these carry populated `water`/`lava` branches, which need the struck
  collider to select. The engine already has both halves of the second problem —
  `ProjectilePool.ClassifySurface` (`Projectile.cs:377`) maps a collider's group to a
  `SurfaceClass`, and `Projectile.cs:296/625` shows the reusable ray query — so this is wiring, not
  decode, once something casts the ray.
  ⚠ Traps:
  - **Colliders are conditional.** `SessionSpec.cs:157` is
    `BuildsCollision => Fly || DamageTest || ForceCollision || DebugDamage != null` — a `--freecam`
    run and every golden-capture mode build **no world colliders at all**. A ray-based fix silently
    does nothing there, so it needs a stated fallback, not an assumption of ground.
  - **Do not give these a constant fall time.** Same trap `BL-240` carries: it would invent a
    landing altitude for 335 zeppelins.
  - `chuteman` has **zero gravity**. Any solve phrased as a parabola divides by zero on it; it is a
    constant-velocity descent and needs the distance, nothing else.

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
  evidence but is unverified against a calibrated dive. *Playtest:* `PT-44`.
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

## Missions, modes & campaign

- `BL-064` `[Feature]` **Better mission states.** There is still a lot of difference between our maps and the
  original's. May need a pipeline to diff them, or to crack the mission loading states properly.
  (`MissionSetup`'s interp boot script, landed 2026-07-22, closed the largest single gap and
  incidentally fixed the C3 coast z-fight — but it is a boot script, not the full state model.)

- `BL-074` `[Research]` **PLAYER_INIT fields [3]/[4] semantics + per-plane spawn speed** — story-mission spawns
  currently assume the IA convention (0.5 throttle / 53.6 m/s).

- `BL-083` `[Research]` `[Owed-playtest]` **Finished-pilot behaviour in a splitscreen stunt race** (M2.5 item 7): a pilot who clears
  every zone freezes at the finish showing their placing while the field flies on. It matches
  the solo run's freeze and makes the placing unmissable, but it parks a player with nothing
  to do for as long as the slowest pilot takes. The alternative — keep flying freely with the
  timer stopped — is a small change (drop the AllComplete early-return when `Race != null` and
  gate only the objective/marker updates). Decide from the two-controller playtest.

- `BL-084` `[Feature]` **Race spawn fairness**: each player takes the next entry in the mission's `stunt_flying`
  spawn list, so pilots start at genuinely different distances from the first zone. Fine for
  a prototype, unfair as a race. Options: spawn everyone abreast from one point (the
  `--pos` `SpawnAbreast` fan already does this), or rank on a per-player-normalised time.

- `BL-126` `[Tuning]` `[Owed-playtest]` **Splitscreen** — the `HudMetrics` sqrt pane damping, `MixGain`, `SpawnAbreast`, join/lock
  feel, tag-gutter widths.

- `BL-299` `[Research]` **Decode `net.zrd.json` as the multiplayer spawn table → the retail MP1–MP3 maps for
  Dogfight.** 45 files, one flat group each, node counts quantised by mission type (MP1→80,
  MP2/MP3→48, campaign→8), 23 distinct payloads shared across files — shape and distribution say
  *spawn table*, not patrol route (`docs/SCOPING-M4-ai.md` survey; its "do not build patrol on it"
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
  `PT-43` evidence, not speculation: **bullet magnetism / aim assistance** (the original assists
  gun aim; without it kills lean on rockets — decide mechanism and strength), spawn
  camping / spawn protection (none in v1), suicide penalty and last-damager credit (0 / none in
  v1), sudden-death overtime on a drawn time-out (draw declared in v1), menu-side match options
  (kill target and time limit are CLI-only), `dogfight_ace` vs `zeppelin_run` spawn spacing, the
  self-blast exemption (own rockets can't hurt you — the guns invariant applied consistently, not
  a balance call), VS HUD line/arrow sizing at 4-player panes. Related, not absorbed: `BL-084`
  (race spawn fairness), `BL-126` (splitscreen chrome).

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

## Tooling, platform & docs

- `BL-030` `[Cleanup]` `[Blocked: M4]` **`docs/SCOPING-M4-ai.md` still names `PlaneViewer.cs:<line>`.** The C11 final sweep
  (PLAN-planeviewer-split) re-pointed the three `docs/formats/` hits to their real post-split
  owners (`WeatherRig.Build`, `WorldEffectsFactory.BuildWorldEffectsRuntime`) but deliberately left
  this one — it's a future-milestone planning doc whose line numbers were already invalidated by
  B7's extraction and the B8 `git mv`, so re-numbering it now is pure churn. Fix when M4 is picked
  up and the doc gets rewritten anyway.

- `BL-033` `[Cleanup]` `[Blocked: SDL >= 3.4.4]` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effect while active: DirectInput-only controllers (non-XInput
  sticks without an SDL HIDAPI driver) are invisible in-game.

- `BL-129` `[Research]` `[Owed-playtest]` **`--freecam` interactive feel** — look sensitivity and the speed curve have never been
  assessed by hand; the module was built entirely through scripted screenshots and
  `--debug-anim`.

- `BL-130` `[Research]` `[Owed-playtest]` **The labs are mouse-driven** (`--viewer`: damage on F5, livery on L, mesh on M) and have had no
  interactive playtest beyond scripted verification. The damage lab now has a second, live host —
  F5 in `--fly` drives the flown plane's real HP while the sim runs — which widens the gap rather
  than closing it: a drag there competes with a per-frame read-back and nothing scripted can prove
  that feels right. `PT-29` is the owed test.

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

- `BL-284` `[Bug]` **Wing-light flare: soft round glow vs the original's sharp star burst; view-dependence
  unproven.** Follow-up from `BL-119` (landed 2026-08-05): with the authored one-sided quad restored
  and the blink at the measured ~1 frame, the flare reads as a compact soft amber glow — much closer
  than the old billboard blob, but the PT-03 reference still shows sharp radiating star points that
  our plain radial `oil_liteflare` sprite does not produce. Whether the original draws the flare
  from every angle (a one-sided quad is roughly chase-view-only) is also unmeasured — one orbit
  clip of a lit plane in the original settles both (piratefighter or brigand: the only airframes
  whose defs wire `wing_lights_blink`; the Bloodhawk carries no flare nodes at all). Also riding
  here: `WingLightBlinker.LightEnergy = 1.0` is a declared TUNE — the def authors the point
  lights' range/colour only, no intensity.
  ⚠ Traps: (a) re-adding the billboard is the rejected fix — PT-03's screenshot is against it.
  (b) don't edit or swap the sprite to fake the star: the star points may be the original engine's
  flare *rendering* (a cross-flare pass), not the texture asset — the orbit clip decides first.
