# Backlog, unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. Which plan is active, if
any, is `PROJECT_CONTEXT.md`'s "Current status", never restated here. Per-item history/diagnosis
detail is in commit messages (`git log --grep=BL-NNN`; earlier in the archived development log's
dated entries) and `docs/architecture.md` (module bullets); how to
verify a change without fooling yourself is `docs/verification.md`. **The live list of hand-tuned
constants awaiting playtest is the set of items tagged `[Tuning]`** (consolidated actionable
index: [`playtest.md`](playtest.md)). When an item gets scheduled into a plan, move it there; when
it lands, delete it here.

**Item IDs.** Every entry carries a flat `BL-NNN` tag, assigned once at minting and never
renumbered or reused, even when the item it names is deleted, so a stale cross-reference elsewhere
fails loudly instead of silently pointing at the wrong item. **Mint a new ID by running
`./New-ItemId.ps1 -Kind BL`**, never by scanning this file or taking "max + 1" by hand. The
counter lives in `.git/item-id-counters.json` (shared by every worktree, outside version
control) and the script increments it under an exclusive file lock, so two concurrent sessions
cannot be handed the same number.
⚠ **Run it for EVERY id, every time, it is not a once-per-session lookup.** Minting one id and
then deriving the next by adding 1, or reusing a number the script handed you earlier in the
session, desynchronises the counter from the file: the id you invented is not recorded, so the
next call hands it out again and the duplicate-id hook fails a later commit. Need several at
once? `-Count n` reserves a block in one call. The failure is silent at the time and surfaces
in someone else's commit, which is why the rule is absolute rather than a default.

**Structure.** Items are grouped into twelve theme sections, in this fixed order: Damage &
destruction · Weapons & combat · Flight model & collision physics · Environment & world · Effects
& animation runtime · Audio · Cameras & views · HUD & UI · Splitscreen · Missions, modes &
campaign · Tooling, platform & docs · Misc. Within a theme, items sort by ascending ID. A straddler goes to the theme
whose system you would open to fix it; Misc is the escape hatch for items with no such system,
if it grows past a handful, that is a missing theme, not a working bucket. Splitscreen is the one
cross-cutting exception: an item whose subject is the single-viewer/single-player assumption goes
there, even though the fix opens another theme's system.

Every item is one flat bullet:

    - `BL-NNN` `[Type]` `[Status?]` `[Size]` `[Next: …]` `[Impact: …]` `[Evidence: …]` `[Scope?]` **One-sentence claim, the symptom or goal.** body…

`[Type]` is exactly one of: `[Bug]` (behaviour is wrong vs the original or vs intent),
`[Feature]` (something the engine does not do yet), `[Research]` (the deliverable is an answer,
not code), `[Tuning]` (a hand-tuned constant needing judgement at the controls), `[Cleanup]`
(debt with no player-visible behaviour change), `[Fidelity]` (the mechanism is decoded and ours
differs, with no symptom yet), `[Perf]` (frame time or memory), `[Tooling]` (scripts, hooks and
the verification loop), `[Testing]` (a missing test or test aid). The optional status tag is `[Owed-playtest]`
(the code/constant side is done; what is missing is a human at the controls) or
`[Blocked: <blocker>]` (cannot start regardless of priority, the blocker is named: a capture
`CAP-nn`, a milestone, another item, an upstream release, a user decision). No status tag means
open and unblocked.

**Property tags** follow the type and status tags, in this fixed order, and say what a full read
of the body would say, so that the artifact can filter on them. `CheckItemIds.ps1` rejects a value
outside these vocabularies; it does not require the tags, so an item minted without them still
commits, and they are added when its body is next reshaped.
- `[S]` / `[M]` / `[L]` is the fix's size by shape, never by hours: `S` is one file, one constant,
  a test-only or doc-only change, or a close with no code; `M` is one subsystem, a few files, or a
  change that re-pins goldens; `L` is plan-sized, cross-cutting, or needs a design first.
- `[Next: decode|data|code|look|decide]` is the first step before the item can move: `decode`
  reads the original executable; `data` reads extracted game data, footage or a measurement;
  `code` writes code or tests now with nothing owed first; `look` needs a human at the controls
  (every `[Owed-playtest]` item and every `CAP-nn` blocker); `decide` needs a user decision.
- `[Impact: high|low|none]` is what a player would notice if the item were done: `high` shows in
  ordinary play, `low` is subtle, rare or only visible when looked for, `none` is cleanup, tooling,
  instrumentation or a research answer with no visible change.
- `[Evidence: decoded|data|footage|spec|feel|trace]` is the strongest source the item's
  *Evidence:* line rests on, in the standing notes' order of trust: an executable decode, extracted
  data files, a capture under `OriginalScreenshots/`, the pre-release design spec, or a feel report
  or estimate. `trace` is the case with no original-game source at all: a read of CSVM's own code,
  a log or a profile.
- `[Scope]` is optional and names the one mission or scene the item is tied to (`[CM14]`, `[C5]`,
  `[MP1]`), spelled as the item spells it. An item that spans missions carries none.

**Body template for new entries** (existing bodies are reshaped opportunistically, when an edit
touches them anyway): after the bold title sentence, labelled run-in lines, each present only
when it has content, *Evidence:* (what was measured or checked, with `file:line`/data paths,
the one field every entry should have) · *Fix shape:* · *⚠ Traps:* (mechanisms already ruled
out, unit ambiguities, "do not fix it by X") · *Playtest after fix:* (launch command + what to
look for) · *Cross-refs:* (related `BL-nnn`/`CAP-nn`/docs, with why).

## Standing notes

- **Scheduled items live in their plan, do not re-add them here.** If one is closed without
  landing, its record goes in the closing commit's message.
- **[`playtest.md`](playtest.md) is the actionable, consolidated checklist** for everything
  tagged `[Owed-playtest]` or blocked on a `CAP-nn`, what to look for, the launch command, and
  what each blocks. The backlog keeps the deep evidence/traps; keep the two in step.
- **Reference shots are in `OriginalScreenshots/`** (gitignored, cited by filename).
- Bare code paths are relative to the Godot project's `src/`.
- **On the original pre-release design spec:** its structural claims have held up against our
  data, per-hardpoint cluster sizes, the 8-firepoint rig, the zeppelin launch-altitude gate, the
  two-volume danger zones, the armour/health damage split. Its per-item art and balance numbers
  have repeatedly failed, gun ranges, rocket speeds, zone hit points, the crash fireball's
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
  ledger"). `FlightScenarios` is 4.

## Damage & destruction

- `BL-672` `[Fidelity]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The remake attributes a weapon hit by climbing the node-parent chain;
  the original attributes it only to the struck node's own handler.** *Evidence:* `FUN_005abcf0`
  reads the hit record's struck node at `+0x24`, reads that node's handler at `+0xbc`, and returns
  0 when it is null. There is no parent walk at all. `DestructibleRegistry.Resolve` instead climbs
  to the nearest node a pool claims, which is the deliberate remake rule recorded in
  `docs/formats/destructibles.md` under "Remake node resolution". Two visible consequences: a round
  on the Gemini's *open* hatch still damages the cannon behind it, where the original's
  `upper_br_door` registers no handler and the hit does nothing; and a round on `turret`
  (model 865, under `gunback`) damages the cannon where the original ignores it. *Fix shape:*
  decide whether the climb is kept as a deliberate forgiveness or narrowed to the decode. Narrowing
  it needs `Probes.cs`'s deep-descendant walk-up assertion rewritten first, which is why it is not
  a small change. *⚠ Traps:* the climb is what makes most destructibles hittable at all, so do not
  narrow it without a per-chapter census of which pools stop answering. `BL-640`'s stowed-cannon
  fix already carves out the one case that mattered (a fallback claim to a live pool whose damage
  node is hidden), so this entry is the remaining, wider question, not that one again.
  *Cross-refs:* `BL-640`'s closing commit, `docs/formats/destructibles.md` "Which node takes the
  hit".
- `BL-060` `[Feature]` `[L]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **Improve on the original crash, the bespoke "breaking apart" (branch `bespoke-crash-animation`).**
  User's call (2026-07-23): the retired bespoke `CrashBreakup` wreck-scatter looked *better* than the
  faithful data-driven crash, so it was preserved on that branch rather than deleted. **The A/B playtest
  passed (2026-07-23), the faithful data-driven crash is confirmed as the default**, so this is now the
  standing follow-up: once the faithful recreation is fully settled, revisit blending the branch's nicer
  breaking-apart (free-body scatter + down-ray ground-rest) into (or over) the data-driven path, an
  explicit "improve on the original" opportunity, not a faithfulness regression.
  ⚠ **Traps (from slices 1–2, 2026-07-23).** The `blend`/`softParticles` `Puffer.Create` overrides
  exist and default to a byte-identical shader, reuse them; a MIX-blend dark puffer near the
  ground also needs `softParticles: false` or the depth-fade zeroes it. A fading additive fireball
  reads as smoke in a screenshot, isolate the emitter (suppress the others, freeze the crash with
  no `--hold`) before believing an effect is present. Anchor at the plane centre (`pose.Origin` =
  `healthy`), not the impact point. For the debris arcs, the executable decode governs now
  (`PLAN-object-motion-decode`, 2026-08-13): `translation_range` gives `dirY = elevation/90` and
  horizontal `1 − |elevation|/90` (an L1 direction, not spherical), `initial` the launch speed,
  `delta` an acceleration. The retired `DebrisTune.LaunchScale` of 0.65 was a footage fit laid over
  the earlier, wrong spherical reading and is deleted along with the whole tune class, a blend
  here starts from the authored arc, with no compensating scalar. The unscaled arc reads like the
  original at the controls, so nothing is owed on it and there is no scalar to put back. What stays
  TUNE is the `fly_trailN` anchor being invisible so that
  only the trail shows; and the DISTANCE interval hides behind an inverted flag
  (`has_interval_value` false, key off `interval_type`).

- `BL-121` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Damage (Run-2 item 10)**, breakup scatter, and whether
  the 10c panel-flip and smoke-trail look right in real flight. ⚠ The invented contact constants
  this item used to name are gone: the crash speed, the stop speed, the graze friction and the
  fitted kick are retired against the decoded response and the decoded death rule
  (`git log --grep=BL-271`, `git log --grep=BL-381`), and the graze feel is judged at the controls
  as matching (`git log --grep=BL-120`), so nothing contact-side remains here. What is this item's
  own is the breakup-scatter feel judgement.
  Rendering at real spawns is verified (the `TopLevel` anchor fix, the archived development log,
  2026-08-03 entry, `trail-world-anchor` suite); this item is a magnitude/feel judgement. Tree softness is retired dead code
  (the archived development log, 2026-07-23 entry), not a TUNE, do not re-add it here.
  **`CAP-15` analysed 2026-08-05** (the burn-down half of `CAP-14 Graze and CAP 15 wing to
  red.mp4`, 37.45 s Bloodhawk chase clip; stills + per-frame fire counts in `playtest/CAP-15/`;
  times are wall-clock PTS, sim-s = ×1.390). The 10c look verdict, part by part:
  (a) **Panel flip matches.** The original's visible damage stage is a skin swap, the outer
  third of the right wing turns charred black at the threshold (all-red before contact), with no
  large flapping geometry readable at chase distance; our torn-`pdpN`-shown/`_h`-hidden swap is
  the same mechanism. Ignition is on the contact frame itself (t = 5.886).
  (b) **Trail staging does NOT match.** The original streams `short_firetrail`'s full
  three-puffer stack from the damaged panel, fire_f01–06 flipbook + orange-born smoke + a
  pure-black smoke puffer, deactivating at the authored 4/6/8 s `EVENT_OFFSET`s and
  sputter-looping while the panel is active. On film: fire-dominant ~12.4 wall-s (peak 9,418
  orange px at t = 6.82), then two pure-black wing plumes, then thinning, sputtering smoke still
  going ≥ 31.5 wall-s after contact at clip end. Our per-panel trails are four bare `firepuffer`s
  (`FlightRigAssembler.cs`), fire flipbook only: no black-smoke phase, no staged burn-out, no
  sputter. **That gap is closed**, `BL-259` landed 2026-08-05: the panels play the authored
  staged burn (fire → black → sputter), census-matched to this clip's 8/6/4/2 sim-s cascade.
  (c) **Gauge timing confirmed:** the damage silhouette's right-wing segment goes RED (nose
  YELLOW) on the first lit blink ≤ 0.25 s after contact, then blinks lit/dim persistently.
  (d) The clip contains **no nose-anchored trail** even with the wing red-critical for 30 s,
  moot in code since `BL-259` landed (2026-08-05): nothing anchors at a synthetic nose offset
  any more; the heavy stage plays `player_damage_trail` at `prop1`. *When* is settled by the decode
  (`FUN_004b3800`, `docs/org/vehicleDamage.md` "Damage staging"): the whole-vehicle health fraction
  at 10%, which a graze that leaves the hull healthy never reaches, so this clip showing no
  whole-plane trail is expected, not a puzzle.
  *Cross-refs:* `PT-123` (the flight that judges the scatter).

- `BL-122` `[Tuning]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)**, several playtest-gated TUNEs,
  all needing the original at the controls: the **debris-arc trajectory** (the executable decode is settled, `translation_range` gives
  `dirY = elevation/90` and horizontal `1 − |elevation|/90`, `initial` the launch speed, `delta` an
  acceleration, `PLAN-object-motion-decode`, 2026-08-13; the former `DebrisTune.LaunchScale` footage
  fit is deleted with no replacement scalar, and the arc's *look* is settled too: the unscaled arc
  reads like the original at the controls; the `fly_trailN` anchor being invisible means only the
  trail's rough scale reads); the **overall crash intensity** (the fireball, the cluster, the debris
  fire and the wreck fire are all additive, so a dirt crash can read as one big fireball, judge the
  whole against the original); and `snd_exp_ground_a` mix level + whether it should layer over
  `plane_destroy_sg` (the dirt def's only Sound is `snd_exp_ground_a`; we keep both). The retired
  bespoke crash on branch `bespoke-crash-animation` is the A/B reference for these.
  **`CAP-16` analysed 2026-08-04** (`CAP-16.mp4`, 2560×1440, 13.49 s; corroborated by
  `C1 IA1 Crash.mp4` and `CAP-14 Crash.mp4`, two further ground crashes with the same signature;
  stills in `playtest/CAP-16/`). ⚠ All times below are **wall-clock** off container PTS, multiply
  by k = 1.390 for sim-seconds before comparing against any authored `run_time`.
  - **The crash pieces barely turn, and the decode since 2026-08-13 says they do not turn at all.**
    A wing panel detaches at ignition and stays legible for 8 sampled frames, t = 6.13 → 6.60
    (0.47 s wall / 0.65 sim-s; `wing-tumble-strip-6.13-6.60.png`). Its long axis rotates only
    **~10–15° over that span**, order 20–30 °/s wall-clock, against the ~150 °/s the ÷`run_time`
    reading of the day predicted. `FORWARD_ROTATION` is now decoded (`PLAN-object-motion-decode`
    C10): the crash `pieceN` fly the vector `TRANSLATION` form, which fills none of the launch
    direction cache the tumble multiplies through, so they hold their orientation and what the strip
    shows is the piece's path plus camera motion. Recorded as agreement, not as evidence, the
    footage is one piece, near edge-on under camera motion, and no measurement off it decides a
    decode.
  - **Wreck momentum on a ground crash, there is none, and the owed judgement is the look of that.**
    No fraction is authored or applied: the `player_crash_*` defs this path plays inherit nothing
    (`player_crash_dirt` authors `impact_force` false, `player_crash_default` never arms the
    instance), and the original scales the inheritance nowhere in any case
    (`docs/org/objectMotion.md`). The pieces therefore stay where they blew rather than scattering
    along travel. ⚠ `CAP-16` shows a panel travelling down-and-forward and chunks scattered
    laterally at rest, which reads as inheritance; it is one piece near edge-on under camera motion,
    with no known impact speed, and no measurement off it decides a decode. What this item still
    owes is the whole crash judged at the controls, not a number.
  - **Overall crash intensity, "one big fireball" is correct for ground, and is surface-dependent.**
    The dirt crash genuinely reads as a single dominant fireball: granular yellow sprite cluster at
    ignition (t = 6.27), white-hot core with orange body by t = 7.40, still at full intensity at
    t = 12.50 when the clip ends. The concern that the additive stack over-reads is **not supported
    for ground crashes**, the original looks like that. ⚠ But `CAP-14 Building crash Balmoral.mp4`
    (t ≈ 5.0) shows a *building* strike as a spread of small discrete orange puffs with **no** large
    fireball and **no** dark halo. Do not tune the two surfaces to one look.
  - **The smokeball is dark red-brown, not black** (t = 8.00–12.50), in all three ground crashes.
  - **Dirt burst reads as a separate, lower, ground-coloured cluster**, three distinct pale-cream
    puffs sitting at the ground line beneath the fireball at t = 7.40, clearly not fire-tinted.
  - **Audio: the two sounds are sequential, not stacked.** Two spectrally distinct onsets, one at
    t = 6.15 (+4.5 dB, spectral centroid **958 Hz**, mid-band dominant 55.8%: the airborne breakup)
    and a deeper one at t = 7.09 (+5.3 dB, centroid **711 Hz**, low-band dominant 55.7%: the ground
    explosion, coincident with the dirt burst appearing). They are **0.94 s apart wall-clock
    (1.31 sim-s)**, with near-equal peaks (−15.1 / −15.2 dBFS). So layering both is right, but they
    should be **offset ~1.3 sim-s**, not triggered together. Mix level is modest: the crash peaks
    only **~+5 dB over the engine bed** (bed −19.1 dBFS median) and never clips. ⚠ Sound *identity*
    is inferred from timing against the visuals, not decoded, the footage cannot prove which onset
    is `snd_exp_ground_a` vs `plane_destroy_sg`.
  - **Duration: the effect never visibly ends, and it never can, the game takes the scene away.**
    A fatal crash returns the original to the menu, so there is no single-player vantage from which
    the fire burns out on screen. `CAP-16` catches that cut: mean frame luma holds *flat* at 50 from
    impact until **t = 12.65**, then fades to black by t = 13.00 (a ~0.35 s fade, the return to
    menu). The fireball is at **full intensity when the fade starts**, it is not decaying. So the
    number to build against is not a burn-out time but a **hold time: 6.52 s wall / 9.06 sim-s from
    ignition to the fade**, during which the effect must not visibly thin out. The other two ground
    clips have no black frame at all (recording simply stopped while lit), so `CAP-16` is the only
    one that captures the cut.
  - **Crash audio ends naturally at 5.47 s wall / 7.60 sim-s after ignition**, i.e. ~1.1 s *before*
    the visual fade begins, so the last second of the burning wreck is silent. The envelope decays
    smoothly (−20 → −25 → −32 → −41 dBFS across t = 9.1 → 11.55) and reaches digital zero at
    t = 11.60; an interrupted recording would have truncated at a non-trivial level instead.
  - **Fireball SIZE, measured against the plane as an in-frame ruler, our burst is ~2.5–3× too
    big, and the `SizeScaleDefault` ×4 stand-in is the reason.** ⚠ Estimate, not a decode. Method:
    the Bloodhawk (`player_bhawk`, the plane in `CAP-16.mp4`) has no wingspan field anywhere in
    data, so the reference is the mesh-AABB figure recorded in `PlaneCollider.cs:31-34,56-60`,
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
      across at t = 0.14 and settles at ~12.7 m, i.e. the authored velocity/friction spread matches
      the footage almost exactly, and needs no change.
    - What does not match is the **sprite size**. `Puffer.SizeScaleDefault` is **4**, explicitly a
      judged stand-in for a missing engine constant, not a decode (`Puffer.cs:276-281`, TUNE settled
      at the controls 2026-08-01), and the quad side is that size in metres (`QuadMesh` 1×1 scaled
      by it, `EmitterRenderer.cs:134,150-156`). At ×4 the mean sprite is 15.8 m across at t = 0.14
      and the whole burst spans **27 m**, reaching **54 m** by end of life. At ×1 (authored verbatim)
      it is **13.2 m** at t = 0.14, within ~35% of the footage's 9.7 m, and the gap closes further
      once the fire sprite's soft alpha edge is allowed for (visible fire is well inside the quad).
      **So the footage puts the missing constant near 1, not 4**, for the crash burst at least.
    - ⚠ Tension, not a verdict: ×4 was chosen because at ×1 the emitters "read as a thin scatter of
      specks against the original's volume." Both observations can be true, the deficit at ×1 may
      be *density* (18 sprites) or sprite alpha rather than size, in which case the fix is more/
      denser particles at authored size, not bigger ones. The knob is per-path, so this bears only
      on **`puffer.burstSizeScale`**, not on the trail/sustain scales that were judged alongside it.
    - **2026-08-09 update, superseding the ×4/×1 analysis above:** `PLAN-puffer-engine-deltas` traced
      `SizeScaleDefault` to an exact decoded constant, `FUN_0057c5c0` hands `SIZE_RANGE` to the
      sprite draw as a screen-space HALF-extent, so the quad side is `2 × SIZE_RANGE`, not `1 ×`,
      and separately found `DEVIATION_DISTANCE` was scattering `±d` where the engine draws `±0.5·d`
      (A2). Re-running this same measurement (`RecordingEmitterRenderer`, `fierypuffer` verbatim,
      `Puffer.Burst`/`_Process` at dt = 1/60 to t = 0.14) on the unchanged build read **mean sprite
      4.10 m, cloud span (centres) 12.52 m, whole burst span 16.62 m**, already past the footage's
      9.7 m at the old ×1 default, not under it as the analysis above concluded (that analysis used
      an analytic estimate, not this instrumented one; the two are not directly comparable). At the
      landed A1+A2 constants (`SizeScaleDefault` 2, deviation halved) the same measurement reads
      **mean sprite 8.20 m, cloud span 12.53 m, whole burst span 20.73 m**, cloud span is
      unchanged (`fierypuffer` authors no meaningful `DEVIATION_DISTANCE`; its spread is almost
      entirely the ±65 m/s random velocity, so A2 does not move this particular puffer), and mean
      sprite doubled exactly with the constant, confirming the intervention took effect. **The
      decode makes the crash burst read larger against the footage, not smaller, the opposite of
      what the ×1 analysis above expected.** Per `PLAN-puffer-engine-deltas`'s explicit instruction,
      the decode lands anyway and this is recorded as a finding, not split against the footage: the
      corrected sim's sprite may still be reading too big against `CAP-16` for a reason A1/A2 do not
      touch (sprite alpha falloff inside the quad, or the fire-keyed pixel measurement in the
      original bullet finding less than the full additive quad), that is now a live open question
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
      while the original is still at full intensity 9 sim-s later, that gap is the hold time above,
      a separate matter from size.
  *Residual.* The player's **own** crash can never show the burn-out, the game cuts to menu, so do
  not re-film one hoping for it. The one untried vantage is an **enemy** plane crashing while the
  player stays alive, which would keep the scene up; worth a capture only if the hold time above
  turns out to be the binding constraint when tuning. Otherwise what remains is the **A/B against
  our build at the controls**, with the reference numbers above to judge against.
  *Cross-refs:* `PT-124` (the flight that judges it, both surfaces and the sound).

- `BL-297` `[Research]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **Panel-damage semantics: what the original actually
  shows when a part is damaged, the user's re-test verdict is that our authored-data reading has
  the feature wrong.** User at the controls 2026-08-06, after `BL-288`'s pooling fix landed
  (bursts no longer teleport, that mechanical fix stands and is not in question): (1) nose
  damage sprays effects at the WINGS; (2) panels appear to tear while armor should still be
  absorbing; (3) identical repeated debris bursts read as "the same panel flies away again", a
  torn panel should be gone once. Expectation: debris matches the point of destruction, is
  health-gated, and each panel tears exactly once.
  *Evidence (data reading, 2026-08-06):* the per-part `injure_anims` DO map panels to their part
  (`extracted/zrdr/vehicle.zrd.json`, player-1: nose→`pdpanel7`, tail→`pdpanel8`,
  leftwing→`pdpanel5`/`4`/`3` at 0.5/0.3/0.15, rightwing→`pdpanel6`/`1`/`2` at 0.4/0.3/0.15). The
  cross-part bleed the user sees is authored *elsewhere* in our reading: (a) every part's 0.99
  `<part>_damage_effects` shim → `random_gun_impact`, which sparks a random `pdp1` (40%)/`pdp2`
  (40%) and ALWAYS `pdp4`, wing sites, whatever part was hit; (b) the vehicle-level 0.85
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
  whether the spark shim exists at all in the original, coordinate, don't overlap). Do not
  re-open `BL-288`'s pooling, the theft mechanism was real and its fix is verified independent
  of these semantics.
  *Cross-refs:* `CAP-29` (the capture), `CAP-27` (spark-shim existence), `BL-288` landing
  (`PLAN-m3-polish-10` A1), `DamageVisuals.cs` (the consumer),
  `extracted/zrdr/vehicle.zrd.json` (the authority).

- `BL-394` `[Bug]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` **AI identity: landed, and owed a flight.** `PlaneStats.LoadForAi` takes a def
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
  *What the AI defs actually author (data, 2026-08-16).* A `weapons` block of 5-tuples,
  `fury`: `[wep_04, 4, 200, 30, 800]`, `[wep_07, 2, 200, 30, 800]`, `[wep_130, 9000, 0.05, 1, 900]`,
  overridden per militia variant (`secfury` swaps to `[wep_12, 6, 30, 200, 800]`,
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
  no def (Sacred Trust's Warhawk, Broadway Bomber's Peacemaker), expected, since that table comes
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
  *Size:* what remains is localized, the same resolution wired into a campaign mission's enemy set
  and into `--generators`, plus the cockpit confirmation.
  *Cross-refs:* `BL-386` (the damage half, landed and closed 2026-08-16,
  `git log --grep=BL-386`; this builds on the `PlaneStats.AiDefName` seam it left),
  `docs/formats/vehicle.md` (the def-family census), `docs/formats/instant-action.md` (the militia
  table's provenance).
  *The AI ordnance trigger reads the fit now.* `AiRocketeer` takes each pylon's own engagement band
  and refire interval, and stamps both timers a launch stamps in the original: the vehicle-wide
  lockout that blocks ordnance of any kind and the launching slot's own next-ready. `AiGunner` takes
  a bound group's window the same way. Launch rates are worth tuning from here, and the
  `DAMAGES_ZEPPELIN` rule is exercisable in the cockpit rather than only in `AiRocketeerTests`.

- `BL-515` `[Research]` `[S]` `[Next: data]` `[Impact: low]` `[Evidence: feel]` `[CM04]` **CM04 (C3/M03): the Barracuda takes damage from every side, and the original may
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
  voice line is flavour and this closes. A second report from the controls: three flak rockets
  on the hull killed it, which reads too easy if the hull takes hits at all and right if only
  the hangar does, so the count is the same question and not a second item. A third report from the
  controls, the CM04 sortie behind `BL-910`: the Barracuda died to about six to eight flak hits on
  the hull. *Cross-refs:*
  `CAP-57` (the original filmed taking three flak rockets on the hull).

- `BL-561` `[Research]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: decoded]` **Aircraft projectile hit volumes are tuned convex decompositions, and the
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

- `BL-639` `[Bug]` `[Blocked: CAP-47]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: data]` `[CM14]` **CM14 (C2B/M04): the Gemini's gasbags do not burn out
  completely, so the zeppelin never dies by its gasbags.** *Evidence:* reported at the controls and
  re-confirmed on the merged build. The authored death is a count:
  `extracted/C2B/M04/mis_anim/geminizep-all_gmzep_gasbags.json` carries
  `activ_prereq_min_to_satisfy: 3` over five `finish_gmzepgasbagN` animation prerequisites, and its
  `call_all_bags` sequence invalidates itself, calls `killgmzep` and runs the remaining gasbag
  animations, so three finished gasbags of five kill the Gemini and the rest burn for show. That is
  the `MINIMUM_TO_SATISFY`/`ANIMATION_LIST` prerequisite form CSVM's reader does parse, with
  `ZeppelinRuntime` its only consumer, so the gate exists in the port; what does not arrive is a
  finished gasbag. *What to settle first:* whether the fire's own animation stops short, or whether
  it completes and the `finish_*` state is never raised. Those are different faults with the same
  symptom, and only the second is a prerequisite question. `BL-599`'s closing commit is the nearest
  precedent, a zeppelin that stopped burning out because the compiled prerequisite state is bit 0
  of `active_raw` (`git log --grep=BL-599`); read it before starting.
  *⚠ Traps:* do not judge a fix by whether the mission ends sooner. The mission's own progress gate
  is the cannons, not the gasbags: primary 3 completes when five of six `deploy_gmzep_lbroadNN`
  animations go `INVALID`, and the briefing says so outright (`MSG_BRF_HWM4_OBJ3`, "Destroy the
  GEMINI by shooting the open cannon hatches"). Gasbags are not an authored substitute for that.
  *Blocked:* what a finished gasbag looks like in the original is unfilmed, so "completely" has no
  reference to compare against; `CAP-47` is that clip.
  *Cross-refs:* `BL-640` (the same zeppelin's cannons); the other prerequisite form, node state, is
  parsed on both paths and enforced at `Start` (`git log --grep=BL-575`).

## Weapons & combat

- `BL-066` `[Feature]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` **M3-deferred, ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- `BL-233` `[Feature]` `[Blocked: M4]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **Extend the proximity fuse to zeppelins (and any other M4 flyer) when they get
  bodies.** The fuse itself came back 2026-08-06 (PLAN-vs-mode B14): re-enabled **aircraft-only**
  against the registered `AircraftBody` list, never world geometry, matching the user's
  recollection that the original fuses on enemies, never terrain, detonating at the round's
  **closest approach** within the swept step, with the fused-on body carried into `Impact` (the
  per-surface IMPACT entries are reachable; the old null-collider bug cannot recur). DD = fuse
  trigger distance, IP = effect radius (settled 2026-08-02 from the choker/torpedo/flare census,
  see the B14 landing commit for the full argument). Remaining work: when M4 gives zeppelins (or
  anything else that flies) collision bodies, they join the fuse's candidate list, the natural
  seam is `CollisionLayers.Aircraft` or a shared targetable layer read by
  `ProximityFuseTriggered`'s registry.
  ✅ **The aircraft-only rule is now decoded, not recollected.** `FUN_004b5fb0` runs the fuse over the
  aircraft list (`DAT_0071dabc`) and never touches world geometry
  ([`docs/org/ordnanceTypes.md`](docs/org/ordnanceTypes.md)). Also decoded: when the weapon carries
  `DETONATION_DOT_PRODUCT`, range alone does not trigger it. The dot is taken against **the
  candidate's** orientation axis, not the round's, and must reach the authored threshold.
  ⚠ Traps: (a) the six plain rockets author `DETONATION_DISTANCE == IMPACT_PROXIMITY`, so a
  first-entry-into-range fuse always detonates exactly where the blast falls to zero and
  deals nothing, the closest-approach rule is load-bearing, keep it for any new candidate class;
  (b) a world-armed fuse re-detonates every rocket 15–50 m short of terrain (the 2026-08-02
  failure), never widen the mask to world bodies.

- `BL-286` `[Tuning]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Muzzle-flash residues after the `BL-263` pick (triad kept, 2026-08-05)**, two
  small opens. (a) closed 2026-08-06: the muzzle-light stand-in magnitudes (was `BL-200`, rode
  `BL-261`/`BL-263`; `MuzzleLightEnergy` 2.5, 2-frame `MuzzleLightLife` 0.03 s, the def carries
  range/colour only) are signed off, judged in `--weapon-lab`; static, so the sign-off covers
  magnitudes only, not motion. (c) Anchor the flash quads AND the muzzle light to the muzzle
  point: at the controls 2026-08-06 the flash sits visibly forward of the muzzle ("direct at it
  would look better"), and the light is still world-fixed, at speed it lags the plane by ~2 m
  for its 2 frames, which the weapon-lab sign-off could not see. Same anchoring work, one
  landing; re-judge both **in flight**, not the lab. When landing, check whether the forward
  offset is authored (a node offset in the def), if so this is a remake-only rule and the
  entry's close should say so. (b) The user's engine-semantics
  hypothesis, open: the def's 3-way `RANDOM_WEIGHT` roll (30/80/140°) may be rendered
  concurrently (all branches) by the original engine rather than pick-one, which would make the
  authored form itself a triad at those exact angles. Our triad uses 120° spacing with one
  continuous roll; a 30/80/140° triad is one constant away and could be A/B'd against
  `MuzzleFlash1-3.png` if the flash shape is ever revisited.
  ⚠ Trap: the pick-one single-quad reading (+ `_muzzle1`→`_muzzle2` flip) was implemented and
  rejected at the controls, do not re-land it without new footage evidence.
  *Playtest after fix:* the anchoring, judged in flight rather than in the lab: the flash sits on
  the muzzle at speed and the light travels with the plane over its two frames.

- `BL-289` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Gun-impact looks (A2/`BL-203`, landed 2026-08-01)**,
  ⚠ **The six `DirtDebris*` constants left this entry: the dirt-chip effect they tuned was deleted
  2026-08-15 (`BL-313`, closed), so there is nothing to A/B.** Dirt now takes the single spark.
  What remains here is the building ricochet:
  `RicochetSparks` **8**, `RicochetSparkSize` **0.55 m**,
  `RicochetSparkLife` **0.55 s**, `RicochetSparkSpeed` **22 m/s**, `RicochetSpreadDeg` **90°** (a
  stand-in, both authored assets are missing from the install). The water-splash column width
  is settled and out of this entry: `SplashColumnWidthScale` **8×** confirmed at the controls
  2026-08-06 with the fades in (`BL-265` closed, the authored quad is 5 cm wide, sub-pixel past
  ~30 m; the reference ticks measure ~0.35 m, which 8× matches). A/B the rest against
  `Dirt Splash.png` at the controls; the splash *height/timing* curves are authored data, not TUNE.
  *Cross-refs:* `PT-128` (the flight that judges the ricochet).

- `BL-693` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The rebinding screen's three axis-capture constants are
  picked, not measured.** *Evidence:* `ControlCapture.RestBand` **0.25**, `MoveThreshold` **0.6** and
  `CapturedDeadzone` **0.5** are what decide whether a stick or a trigger a player pushes becomes a
  binding, and none of them has a decode behind it: the original cannot bind an axis to a command at
  all (`docs/org/input.md`, `FUN_00537090`'s four typed slots), so there is nothing to match. The
  ordering is the rule and is deliberate, `RestBand` < `CapturedDeadzone` < `MoveThreshold`: an axis
  must be seen inside the rest band before a move counts, so drift cannot latch; the move must clear
  a threshold well past that band, so a sloppy centre cannot either; and the deadzone stamped on the
  binding sits between the two, because the value a stick crosses is not the value it settles at.
  *Owed at the controls:* with a real pad, bind a flight action to a stick direction and to a trigger.
  Does 0.6 feel like a decisive push rather than a nudge, and does 0.25 forgive the stick the pad
  actually rests at? Then fly the result: at 0.5 the bound half of the stick has to feel like a
  button, on and off, without a dead patch a player reads as a broken binding.
  ⚠ A trigger already bound at another deadzone is not a second control: `ActionMap.SameControl`
  ignores the deadzone on purpose, so capturing the right trigger takes it from both Camera Boost
  (0.5) and Camera Dolly Out (0) rather than stacking a third reading. Raising or lowering these
  numbers does not change that, and must not be used to try to.
  *Cross-refs:* `PT-120` (the pad sitting that judges the three), `BL-296`, `docs/org/input.md`, `docs/org/targeting.md`, `docs/controls.md`.

- `BL-399` `[Feature]` `[L]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **Track Target's camera behaviour, `L` is reserved, the camera itself is
  undecided.** *Evidence:* the player-targeting plan's out-of-scope call (b), 2026-08-15: the
  original's `Views 1 → Track Target` binds `L` (free in our flight keymap; our `L` is the
  viewer-only livery lab), decoded in `docs/org/targeting.md`, but "keep the target framed" hides a
  pile of camera decisions that plan deliberately deferred: snap vs smooth follow, override vs
  blend with the chase camera, behaviour with no target selected or a target behind the pilot, and
  interaction with the right-stick free look (`BL-372`). `L` is reserved in `docs/controls.md` but
  bound to nothing. `PLAN-cockpit-view`'s head-look decode names the mechanism this camera would
  ride: the look-state byte the controller reads (`DAT_0064ef68`) has a third value, `2`, for
  padlock, sitting beside the `0`/`1` snap/free-look states `BL-432`'s selector keys pick between,
  so `L`'s camera is this same state machine's third mode, not a bolt-on. Building it needs
  `TargetSelection.Current` plumbed into `HeadLook`'s target so the padlock state aims the head at
  the current target instead of reading player input.
  *Fix shape:* a camera-focused item once the questions above are settled, not a change to the
  targeting module itself, which already exposes `TargetSelection.Current` cleanly for a camera to
  read.
  *Cross-refs:* `BL-372` (right-stick free look), `BL-432` (the `K`/`J` mode selectors, the byte's
  other two states), `docs/org/targeting.md` "Track Target", `docs/controls.md`,
  `PLAN-cockpit-view` (`HeadLook`, `src/Flight/HeadLook.cs`).

- `BL-397` `[Feature]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **A modernized target marker: brackets only PAST range, not under it, the
  deliberate INVERSE of the original's own rule.** *Evidence:* the user's preferred rule (brackets
  only past 500 m) was the player-targeting plan's original premise and was disproven in the
  2026-08-15 grilling: the original draws brackets only UNDER the selected gun's reach
  (`TargetHud.GunReaches`), distant enemies get none, and near ones get brackets the silhouette
  often swallows. The user's ask is the exact inverse of that, not a memory of it. The shipped
  marker uses the original's rule as fidelity; this item is the later, separate call to add the
  modernization as an opt-in or a replacement.
  ⚠ *Trap:* do not "fix" `GunReaches`'/`TargetHud`'s gate to match this without checking this
  entry first, the two rules are opposites BY DESIGN, not an oversight left behind.
  *Fix shape:* a flag or setting flipping the gate's sense once the product call is made (past range
  = bracketed, inside = not), reusing the same hysteresis machinery already built.
  *Cross-refs:* `TargetHud.GunReaches`.

- `BL-411` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **Force feedback is unimplemented, and it is the only thing the `TORPEDO` flag
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

- `BL-603` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The human rig sweeps the mesh hull where the original sweeps its def's six
  `collision` probes.** *Evidence:* decoded for `BL-601` (`git log --grep=BL-601`): `FUN_0048d7f0`
  carries the def's `collision` list as rays from the previous pose, six points on the `p*` player
  defs; `FlightController.SweepProbes` does that for an AI rig and keeps the mesh-derived hull sweep
  for the human rig, which is wider than the six points. *Fix shape:* fly a slot the six points
  clear and the hull does not (CM13's dbase arch on dzpath2) in both games; if the original passes,
  sweep the player's probes too. *Cross-refs:* `PlaneStats.CollisionProbes`, `docs/formats/vehicle.md`.


- `BL-877` `[Bug]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **A stock fit of two or six pylons is split
  between the wings by a count heuristic that disagrees with the rig, so the Hoplite's ammo screen
  offers a right-wing pylon the aeroplane does not have.** *Evidence:* `HangarFeature.StockWingCounts`
  groups a fit's pylons as 1-4 against 5-8, while the rig pairs them across the centreline, odd to
  port and even to starboard ([`docs/formats/markers.md`](docs/formats/markers.md), "Pylons"). The two
  agree for counts 1, 3, 4, 5, 7 and 8, and disagree for the Hoplite's 2 (the heuristic says 1/1,
  the rig hangs pylons 1 and 5, both port) and the Firebrand's 6 (3/3 against 4/2). The counts are
  what the flight check and the ammo screen bound their per-wing cells by, so on those two airframes
  a stock-fit plane shows a cell whose pylon `Loadout.PylonForCell` cannot find, and the pick is
  dropped. *Fix shape:* derive the counts from the same wing split the cell join uses, one function
  in `Loadout`, or decide the heuristic is what the screens should keep showing. *⚠ Traps:* the same
  counts seed a fresh hangar build (`LoadStockWeapons`), so changing them moves the Hoplite's
  default build to 2/0 and the Firebrand's to 4/2; the total, and therefore the price, is unchanged.
  The original's own Hoplite special-plane template is 1/1, which says nothing about a stock fit.
  *Cross-refs:* `docs/formats/saved-games.md` ("A cell names a pylon only against the fit"),
  `CSVM/src/UI/Menu/HangarFeature.cs`.
- `BL-910` `[Bug]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: feel]` **Allied AI on CM04
  take the camp buildings at mission start and never leave them for the enemy aircraft; the
  original's allies attack aircraft only.** Flown in the original: on CM04 the allies never attack
  an engine or a building, they attack enemies, and several enemies go down without the player
  firing. In CSVM the same sortie has every wingman and Devastator on `u_camp1..3` and two AA guns
  for the whole mission. *Evidence:* the session log on the run branch: at the mission start the
  three wingmen and both Devastators acquire `u_camp1..3` and `aagun30/32` at 0.85 to 1.3 km with
  183 structures in the scan, while the nearest enemy aircraft stands at 4.9 km, beyond the 2000 m
  activation range, so no aircraft ranks and the structures are admitted. `BL-866`'s aircraft-first
  preference is consulted only at that first pick: `FlightController.DriveAiGunner` keeps a
  standing target while it is alive and never re-ranks. *Fix shape:* three parts. (1) Decode which
  structures reach an AI pilot's pool in the original (the `+0x8d` mission structure list in
  `docs/org/aiPilot.md` "Target acquisition", against `AimCandidateSet.AddStructures` in
  `CSVM/src/Flight/AimAssist.cs`, which admits every hostile destructible), and whether a
  `wingman` scorer admits structures at all; match that admission. (2) Port the 20 s re-score as
  decoded (`docs/org/aiPilot.md` "A standing target is sticky for 20 seconds"): the standing
  target is re-scored once the hold runs out and kept while it still scores valid, replacing "keep
  while alive". (3) Keep the aircraft-first preference and apply it at each re-score too, as
  CSVM's own layer over the decode, so a wingman leaves a building the moment an enemy aircraft is
  in reach even where the decode alone would hold it; say so in the code. *⚠ Traps:* the re-score
  alone does not sweep the pool in the original, a camp that still scores valid is kept, so (2) by
  itself would not have changed the CM04 sortie; the divergence is the first pick, which is (1).
  `--ai-targeting=decoded` restores the original's arithmetic with no class priority and is the
  control for (1). *Playtest after fix:* CM04 from the campaign, watch the wingmen through the
  first two minutes: they turn onto the British aircraft as those arrive and never strafe the
  camp. *Cross-refs:* `BL-866`'s closing commit (the preference and the picker), `BL-515` (the
  Barracuda's flak count from the same sortie), `docs/org/aiPilot.md`, `docs/org/targeting.md`.
- `BL-918` `[Bug]` `[M]` `[Next: data]` `[Impact: high]` `[Evidence: feel]` **The original's
  Non-Aircraft target cycle offers only the mission's curated entries and never a turret.**
  Compared at the controls: in the original's CM01 the Non-Aircraft class selects the Pandora and
  nothing else, in CM06 it selects a "[Destroy] Cargo Train" marker the player had not noticed,
  and in neither mission can a turret be selected with it. *Evidence:* the user's comparison
  sortie in the original; `BL-400` closed the list as the mission's own `targets.zrd`, which
  agrees with the two observations, but whether CSVM admits anything beyond that list (a turret,
  a destructible with an authored team) has not been checked mission by mission. *Fix shape:*
  cycle Non-Aircraft in every campaign mission in CSVM and record the list per mission; any turret
  or structure outside the mission's `targets.zrd` is a wrong admission, find the path that adds
  it (`AimCandidateSet`, the turret pool, `TargetPool`) and close it. Where the lists agree the
  item closes as an answer with the per-mission table in the commit. *⚠ Traps:* the AI pilot's own
  acquisition (`BL-910`) admits turrets by design and shares candidate code with the player's
  cycle; do not narrow the AI's pool while fixing the player's. *Cross-refs:* `BL-400`'s closing
  commit, `BL-839`'s closing commit (the profile-less campaign launch binds the same table),
  `docs/org/targeting.md`.


## Flight model & collision physics

- `BL-562` `[Perf]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` `[CM11]` **CM11 (C2/M02) still spends single physics ticks of 45 to 51 ms in flight and
  about 124 ms on the first tick after the world build.** *Evidence (traced):* the bracketed
  instrument (`PhysicsTickCost`, `--perf`'s `phys_tick_ms` / `phys_tick_max_ms` / `phys_hz`) over 82
  windows of a flown CM11, with the recurring telemetry burst removed, leaves three residual terms,
  each attributed by a temporary sub-scope breakdown inside the tick. (a) The FIRST tick after the
  build costs about 124 ms, 95 ms of it the 20 animation runtimes' first `Advance` (46,355 index rows
  walked over five cold `FindAll` misses, plus a 29 ms first sim step); it reproduces on every run to
  within 2 ms and is the world-build settling regime, not flight. (b) One tick in a sortie reaches
  46 ms inside `FlightController`'s AI collision sweep (`SweepProbes` + `CenterRayContact`, 45.1 ms
  in a single aircraft's step). (c) One reaches 50 ms inside a single `AnimRuntime.Advance`. Nothing
  else exceeds 16.7 ms and the tick rate holds at a median 60.0. *Fix shape:* (b) first, since it is
  the one a player meets mid-flight: log which collider the sweep struck on the spiking step and
  whether the cost is the query or the report it fills. (a) is worth a separate look only if a
  cutscene handoff or a mid-mission stage build repeats it. *⚠ Traps:* **the cap-exhaustion premise
  is dead**, the entry used to claim 72, 102 and 177 ms ticks discarding about six sim steps each
  against Godot's default `max_physics_steps_per_frame` of 8; over four paired 83-second runs on the
  current build no window's worst tick reaches 133 ms, so nothing exhausts the cap. **Do not chase
  the sustained step** either: the ~39 ms step and half-speed sim the entry once claimed were a
  misreading of Godot's `physics_ms` monitor, which holds the WORST tick of the last wall second
  (`docs/verification.md` PERF-1). The recurring 18 to 25 ms band that dominated every earlier
  reading was the ungated once-a-sim-second telemetry print, one write per live aircraft in the same
  tick (PERF-23); it is gated and gone, so do not re-derive it. Compare durations in sim seconds,
  never wall seconds; state which mode a re-measurement flew (the numbers here are `--no-det` with
  nobody at the controls, so they under-weight projectiles and destruction cascades); and do not
  raise `max_physics_steps_per_frame`, which deepens the catch-up spiral rather than recovering
  lost steps. The per-sim-step query objects those ray casts build are now reused rather than made
  fresh (`GodotWorldQuery`), so a re-measurement of the tick meets a different allocator than C22's.
  *Cross-refs:* `PLAN-M5-polish-6` C22, `docs/verification.md` PERF-1, PERF-20 and PERF-23.
- `BL-914` `[Bug]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **An autogyro takes no
  input from the mouse under mouse flying.** Reported at the controls after `BL-447` landed: with
  Mouse set to Fly, the autogyro does not respond to the mouse at all and the mouse keeps driving
  the head look. *Evidence:* `MouseFlight.Read` implements the decoded roll/yaw exchange but the
  port reads two mouse axes and hard-codes the third to zero, so the exchange leaves the
  autogyro's roll at zero; that explains a missing roll, not a missing yaw and pitch. A second
  candidate: with mouse flying on, the right button is a free-look toggle
  (`FlightController.StepFreeLookFlag`) and `MouseFlightRead` returns nothing while free look is
  set, so one earlier tap leaves the mouse on the head for the rest of the sortie. Neither is
  confirmed as the cause. *Fix shape:* find why the autogyro gets nothing (a headless probe that
  logs the mouse deflection reaching `_model` per tick, on an autogyro and on a Fury), fix that,
  and make the autogyro's mapping the decoded exchange with two axes: mouse left and right yaws,
  up and down pitches, roll stays on the keys. *⚠ Traps:* `BL-915` changes the right button from a
  toggle to a hold; land that first or together, since it removes the second candidate.
  *Playtest after fix:* Instant Action in an autogyro with Mouse on Fly: the nose follows the
  mouse in yaw and pitch from the first frame, no button pressed. *Cross-refs:* `BL-447`'s
  closing commit (the exchange and its honest limit), `BL-915`, `BL-916`, `BL-917`,
  `CSVM/src/Flight/MouseFlight.cs`.
- `BL-916` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **The mouse pointer
  stays visible and free while flying; it should be hidden and captured by the window.**
  *Evidence:* nothing in flight sets `Input.MouseMode`; the only writers are the pause boards, the
  preferences page and the spectator camera, so `FlightController.MouseLookDelta` reads a visible
  OS cursor that walks off the window on a second monitor. *Fix shape:* capture and hide the mouse
  when a flight session takes input (both Look and Fly schemes), release it on every board that
  needs a pointer (pause, wrap-up, the Original UI dialogs) and re-capture on resume; the delta
  read moves to relative motion under capture. *⚠ Traps:* the hidden test desktop and `--det`
  runs must not depend on the mouse mode; guard the capture on a real display. *Playtest after
  fix:* fly on a two-monitor rig, the pointer never appears and never leaves the game window;
  pause shows it again. *Cross-refs:* `BL-447`'s closing commit, `BL-914`, `BL-915`,
  `CSVM/src/Flight/FlightController.cs` (the mouse read).


## Environment & world

- `BL-070` `[Bug]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` `[C5]` **C5's `poleflare` clutter renders with the wrong billboard axis** (one of two residuals
  from polish-3 item 5, 2026-07-22; the other, the static collider probe's off-by-6/11, closed
  2026-08-04, the archived development log's "M3 polish-6 C22" entry, with a rewritten probe now committed at
  `analysis/collider-probe/`). The `cblock*` templates ship `lightpole` (`CylindricalY`) posts
  *and* `poleflare` (`SphericalY`) glows, 33,682 of each in `cblock1` alone. `ClutterBuilder.Kind`
  carries no per-kind billboard mode, so every kind goes through the one Y-axis shader: the glows
  spin upright instead of facing the camera. Now *detectable* (the shared
  `SceneBuilder.ClassifyBillboard` distinguishes the two), but fixing it means giving `Kind` a
  billboard mode and a second material path, and it changes how 139,388 C5 sprites look with no
  reference shot to check against, so it needs an original-game A/B.
  *The "should be exempt from the SUNLIGHT dim" half is a data question, not a texture one.* The
  original's hardware draw has no per-texture lighting exemption (`docs/org/vertexLighting.md`,
  "The hardware draw"); the only exemption is the model's own `lighting` flag, and whether the
  `poleflare` and `lightpole` decoration models carry it is read off the templates, not fitted.
  What remains here is the billboard-axis question, and it still needs the A/B.

- `BL-272` `[Tuning]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Precipitation: every unit mapping from `weather.json` to a look is invented, and
  one remake-only rule is deliberately held back** (`Precipitation.cs:29-62`, type/tint/rate/density
  are authored; fall speed, box size, particle counts, streak length/width, sway are 16 TUNE
  constants; the sprites themselves are procedural stand-ins for the original's untextured
  line/point primitives, and rain streaking along fall-direction-vs-velocity is a documented
  remake-only rule pending an A/B). Needs original rain and snow footage to calibrate, worth a CAP
  when weather work resumes.
  ⚠ One calibration fact is already on file (`CAP-11`, 2026-08-07, user): C2B IA1's rain falls
  below the cloud cover as **one-pixel-wide streaks**, narrow enough that the 2560-wide Game DVR
  capture swallows them entirely, while ours are plainly visible in the same scene
  (`playtest/CAP-11/csvm-c2b-low.png`). Streak width is the first constant to revisit.

- `BL-322` `[Bug]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` `[C5]` **C5's lit facades render ×0.58–0.66 of the original with WorldLight already at
  clamp 1.0** (split out of `BL-303` at its close, 2026-08-08; measured `CAP-11`: tower faces 10.2
  vs 15.5, low-rise 21.7 vs 37.6). Explicitly NOT fog, `BL-303`'s own adjunct note, and the Wave
  B fog work moved none of it.
  *Decoded, and the item's own premise is refuted:* the original's surface lighting is written up in
  `docs/org/vertexLighting.md`. On the hardware draw every retail capture shows (`FUN_00554550`) the
  one gate is the model's `lighting` flag (`FUN_00552020`), and **the measured facades are on the
  lit side of it**, so the original modulates them exactly as we do. Asking "should these facades be
  modulated at all" therefore answers yes, and no exemption is available to close the measured
  ratio. The texture's alpha bit exempts a polygon in the software draw alone (`FUN_005524d0`),
  which no capture shows, so the overlays drawn on top of those facades (`buildingspotlighted`,
  `nypd`, `clock`, `fadedsign01`–`03`, `lightpole`, `lite_out`, `bliteon`/`bliteoff`,
  `traffic_sign1`) are lit in the original as they are here, and offer no lever either. The
  residual is unexplained and not predicted by any decoded lighting term.
  *Playtest after fix:* the C5 night poses in `playtest/CAP-11/README.md`.

- `BL-325` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: footage]` `[C1B]` **Night cloud sprites are directionally moonlit in the original; ours are
  uniformly lit** (split out of `BL-118` at its close, PLAN-overcast-match `C24`, 2026-08-09,
  decision 2 of that plan kept it out of a two-daytime-stills milestone). The original lights a
  night cloud by which side of it faces the moon; we apply one flat `WorldLight` to the whole
  population, so a moonlit frame is right on the away side and roughly half as bright on the lit
  side.
  *Evidence:* `CAP-11` C1B (2026-08-07, `playtest/CAP-11/`): cloud cores near the moon reach
  **p90 218** (t=16) while the away-from-moon cloud sits at **p90 70** (t=5); our uniform
  `WorldLight` 0.426 rendered **p90 102**, matching the away side and ~2× dark against the lit
  side. Re-measured on the Wave-B build (`B15`, plan `B17` Table 4): our C1B moonlit cloud tops
  read **p90 187.5** against the original's **155.4 (t5) / 181.9 (t16)** at the spawn pose
  (`--chapter=C1B --pos=-5406,55,-7200 --direction=-0.391,0,-0.921`), i.e. the *band* is now
  plausible and the *direction* is still absent.
  ⚠ **Which population.** C1B ships **no `fvol` volumes at all** (`FogVolumeTests`
  `C1B "0|-|206.25|bare|cloudsprite:absent"`; its freecam census prints no `fogvol clouds`
  line), so every cloud in that footage is one of the **70 placed `cloudparent` facades**,
  ordinary world geometry. Verified by `C23`'s fork landing, which changed the `fvol` card colour
  and left C1B byte-identical (`mean|d| 0.000, 0 px changed`, `c1b-night-sea` golden `ok`).
  ⚠ **Vocabulary, three populations, never one phrase for two** (`BL-118`'s note, kept alive
  here): **`cloudsprite1`/`cloudsprite2`** are the `fvol*` clutter scatter (the deck field,
  world-locked and tiled); **`cloudparent`** are discrete world-placed clusters (C1B's 70, C1's
  28, C4's 45); and the **plane-local ambient wisps** each chapter's `speed_cue.zrd` emits 60 m
  ahead of the player (`Flight.SpeedCue`, `docs/formats/effects.md`) are a third. A claim about one is not
  evidence about the others, and the first two **share their textures**, `--tex-override` on
  `cloud1.tif`/`cloud2.tif` paints both (`SHOT-21`), so separate them by altitude or cluster
  position, never by texture.
  ⚠ Traps: `csky_world_light` is CAP-11-calibrated on terrain, a directional cloud term must be
  cloud-local, the way `C22`'s deck fix was deck-local. And C1's own daytime cards were measured
  faithful at `lighting: false` (`C21`/`C23`). Nothing in the original scales a cloud card's
  colour at all (decoded, `docs/org/cloudCards.md`), so this must not become a global cloud
  brightness knob; `FogVolumeClutter` now renders the authored 240 unscaled and carries no such
  constant.
  ⚠ **The `lighting: true` cards are the other half of this, and they are `fvol`, not
  `cloudparent`.** `lighting: true` on a `Facade` card admits `AMBIENT + DIFFUSE × max(N·L, 0)`
  per vertex on the card's own three authored normals through the billboard basis
  (`docs/org/vertexLighting.md`), not a flat multiply. C1 and C4 author `lighting: false` so their
  daytime plateau is untouched, but C1C, C2B and C5 author it true and we apply a flat
  `csky_world_light` there. That is a look change on a visible population, owed a verdict at the
  controls before any code moves, and it replaces the flat multiply rather than stacking on it.
  Today's frame at C1C is the flat-multiply one: placed `cloudparent` facades **235.25**,
  un-dimmed deck floor **195.8**, `fvol` cards **163.7** (measured 163.24 / 163.83; C2B 163.24),
  cloud-population spread **71.6**.
  *Playtest after fix:* the C1B night spawn above, against `playtest/CAP-11/`'s t5 and t16
  frames, saying for every measured puff which side of the moon it faces.
  *Cross-refs:* `docs/formats/effects.md`'s speed-cue section (the third population),
  `docs/org/vertexLighting.md`'s facade section, `CAP-11`.
  ⚠ **The lighting gate cannot supply this item's direction, decoded.** Every placed `cloudparent`
  card in every deck chapter (C1's 626, C1B's 1,620, C1C's 1,056, C4's 1,453) is authored
  `lighting: false` and carries no normal array at all, so the original's directional light reaches
  none of them and `SceneBuilder`, which honours the flag, does not multiply them by
  `csky_world_light` either. Re-check what the measured p90 above is actually reading (fog mix, not
  the world light) before building on it. A directional term for this population has to come from
  some other mechanism; the flag does buy a per-vertex `N·L` on the `fvol` cards, which C1B ships
  none of.

- `BL-328` `[Tuning]` `[S]` `[Next: decide]` `[Impact: none]` `[Evidence: data]` **The deck floor's 20,480 m annulus half-span was sized against a mechanism
  that no longer exists, re-derive it, or decide it does not need one** (minted at
  `PLAN-weather-decompile-match` `D31`, 2026-08-09; `WorldBuilder.AddDeckAnnulus`). `C26`
  (PLAN-overcast-match) picked 20,480 m from the rim formula `f·K/halfSpan` with `K` =
  `DeckCeilingHeight` = 135 m, i.e. against a ceiling **anchored to the camera**, which made the
  floor's far edge sit at a constant **3.95 px** below the horizon at every altitude. `B13` then
  made the floor world-fixed at the tiles' authored altitude and `B14` deleted `K` outright, so the
  edge's elevation is now `f·(cameraY − 960)/20480` and **grows with altitude**: measured at C1
  looking level west, **6 px** below the horizon at y = 1192 (predicted 6.79) and **33 px** at
  y = 2000 (predicted 30.4), where the 144-tile sheet's own 6,144 m edge would put them at 22.6 and
  101 px. At y = 1192 the edge reads as a soft ~19-unit ramp over ~10 rows from the `zone2` dome
  (mean 193.9) onto the floor (212.6). Nothing is broken today, the extension is still doing more
  work than the bare sheet at every above-deck altitude, and `D31` closed the below-deck strip it
  was built for by a different mechanism entirely (the zone-1 dome; the ladder's dip fell from
  `C26`'s 2.07/+1.11 to **0.15/0.14** at every rung), but the NUMBER now rests on a dead
  derivation.
  ⚠ Traps: **below the deck the annulus is unreachable**, the tiles are `zone_id 2` and `B12`
  culls the whole sheet at camera state 1, so any below-deck measurement of it is measuring a
  forced `--sky-zone=zone2`, not play. The ceiling constraint is the flown dome, not the map:
  C1's `zone2` dome renders at 8,744 m × 2.5 = **21.86 km** unclamped, so 20.48 km already sits
  only 1.38 km inside it and a larger annulus needs `HorizonScaleFor` checked first (`B14` fits the
  scale per dome now). Do not reinstate `DeckCeilingHeight` or any camera-anchored floor to make
  the old formula apply again, `B14` deleted it on decompiled evidence.
  *Playtest after fix:* climb C1 from 1,100 m to the ceiling looking level at a clean horizon
  (`--pos=-7323,<y>,-3829 --direction=-1,0,0`) and say whether the floor's far edge is ever
  visible as an edge.
  *Cross-refs:* `docs/architecture.md`'s `WorldBuilder.cs` entry, `PLAN-weather-decompile-match`
  `D31`/`B13`/`B14`.

- `BL-329` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The D32 in-cloud flicker's rate and ramp are declared
  TUNE, not decoded** (`PLAN-weather-decompile-match` D32, 2026-08-09;
  `Session/WeatherRig.BandFlicker`). `FUN_0042ee40`'s drift rate multiplies a per-mission
  weather-struct field ≈ `+0x934` that no reader decodes and no capture pins a value for, so
  `BandFlicker.DefaultRate = 5.5f` is picked, not measured: at the re-randomized drift speed's
  midpoint (0.2..1.0, mean 0.6) it traverses the blend parameter's full [0,1] range in `5.5 * 0.6 *
  0.1 = 0.33`/s, i.e. ~3 s at the mean and 1.8–9.2 s across the randomized range, "a full traverse
  in a few seconds", not a decoded figure. `BandFlicker.RampFrames = 30` (0.5 s at the fixed 60 Hz
  step) is the amplitude ramp that keeps a session's frame 0 (and any static probe/golden capture
  at a rig's first tick) reading the unremapped `WeatherState.WhiteoutAmount` exactly, its length
  is also a guess, chosen only to be short next to a flight and long next to one frame.
  *Evidence:* `PLAN-weather-decompile-match`'s D32 entry; the curve shapes themselves
  (`BandFlicker.LogCurve`/`AtanCurve`/`Remap`) ARE decoded from `FUN_0042ee40` and are not part of
  this TUNE, only the rate and ramp length are a judgement call.
  *Fix shape:* none pending, needs in-cloud footage of the original with visible timing (a static
  screenshot cannot show a drift rate) before either constant can move off a guess.
  *Playtest after fix:* fly into C1's cloud band and hold in the RAMP, not the opaque core, the
  core (1032–1062 m) is fully whited out and the flicker's own guard skips it there by design
  (`--freecam --chapter=C1 --pos=-2000,1000,-1792 --direction=0,0,-1` sits in the bottom ramp),
  and compare the shimmer's pace against any original in-cloud footage (`CAP-12`'s C4 take has
  in-cloud frames) once such footage is reviewed for timing rather than just colour.
  *Cross-refs:* `docs/architecture.md`'s `Session/WeatherRig.cs` entry (D32 bullet).

- `BL-341` `[Research]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: data]` `[C5]` **Reopened `BL-250`: with the real `no_clutter` gate landed, 7.6% of C5's ground
  (13.8 million m², the flagged overlay area with no base layer beneath it) renders bare, and
  whether that is what the original does is untested.** `BL-250` closed 2026-08-07 on a curated
  list (`ClutterBuilder.BuriedClutterDistricts`, excluding `cblock4/5/6` map-wide) that turned out
  to be standing in for a mechanism nobody had decoded yet. B13/B15
  (`PLAN-clutter-uv-placement`) landed that mechanism, `PlaceOnMesh` now skips a
  polygon carrying the decoded `no_clutter` flag, the flag SELECTS which of two coplanar layers
  decorates rather than meaning "bare here", and the curated list was retired because it was wrong
  on 14.1% of the map even though it happened to be right where `CAP-22` looked (78.3% of C5's
  ground by area is genuinely tower country). *Evidence:* B15's landing commit
  (`git log --grep="B15: land BL-305"`) measured the gate against every flagged/base pair in C5
  and found 35% of flagged overlay area has no coplanar base polygon underneath it at all, for
  that ground the gate now has nothing left to fall back on and leaves it undecorated. *Fix
  shape:* find what the original actually draws on that 7.6%, either a third layering mechanism
  this plan didn't decode, or the original genuinely leaves it bare too (which would close this
  outright). Start from a located landmark pose, the gate itself was confirmed by the user's own
  flyover 1 km north of C5's `brooklynbridge` node, not by a nadir (`SHOT-28`,
  `docs/verification.md`), and not from `CAP-22`'s pose, which
  cannot resolve this question (it already reads correctly). *⚠ Traps:* (a) do not re-curate a
  list as a stopgap, that is exactly the mistake this item exists to not repeat. (b) A nadir
  shot cannot distinguish a painted rooftop from bare ground any better than it could distinguish
  a rooftop from a building (`SHOT-28`); use a low oblique. *Cross-refs:* `BL-250` and `BL-305`
  (both closed, the doubled-district curation and the C5 packing bug the gate that surfaced this
  replaced; `git log --grep=BL-305`. Do not reopen either ID; IDs are never reused, per this
  file's own rule).

- `BL-508` `[Research]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The original never alpha-tests, so every alpha texture we scissor is an
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
  thousands of coplanar foliage and railing cards a hard cutout currently keeps in the opaque pass,
  the coastline sheets were safe because they are few and flat, and that does not generalise.
  A period video card without a 4444 or 8888 texture format collapsed `Full` alpha to 1 bit at
  threshold 128, so a 1-bit look in reference footage may be the hardware and not the intent; check
  which the shot is before treating it as the target. *Playtest after fix:* a low pass over C1's
  tree lines and C2's Eiffel replica, where the erosion this rule was written to prevent would show
  first. *Cross-refs:* `analysis/alpha-classification/FINDINGS.md`, which carries the decode and the
  install-wide census; the coastline commit that filed this (`git log --grep=SoftAlphaCoastline`).
  The sort those cards would join is Godot's own per-camera transparent-object pass, already
  carrying every particle emitter, and it costs nothing per object; what it does NOT do is order two
  interpenetrating objects' polygons against each other, which is the risk named above and is
  unchanged by the particle work ([`docs/org/textures.md`](docs/org/textures.md), "The depth order,
  and where ours stops being the original's").

- `BL-683` `[Bug]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: trace]` **The faithful path's aircraft ambient cannot be driven by the mission, because
  `AmbientLightEnergy` never reaches the shader.** *Evidence (traced and measured):* the faithful
  arm writes the zone's authored `SUNLIGHT_AMBIENT` onto the Environment, but
  `AmbientLightSource.Sky` at full sky contribution makes Godot take the ambient off the sky
  cubemap scaled by the background energy multiplier, so the value is inert. Taking the launcher's
  0.9 to 0.0 left all 18 goldens byte-identical, while the same experiment on the sun, 1.6 to 0.5,
  moved 7 ([`docs/verification.md`](docs/verification.md) `WORLD-32`). So an aircraft's fill light
  is a daytime procedural sky at night as well as by day, and only the sun half of the authored
  pair is visible. *Fix shape:* decide whether the faithful Environment should take a
  mission-coloured ambient instead of a sky-sourced one, and what that does to the fullbright world
  it sits in. *⚠ Traps:* this is a rendering-design question and not a bug to patch by turning the
  energy up, since the energy is not read at all. Do not reach for `csky_world_light`, which is the
  fullbright world's scalar and is `CAP-11`-calibrated on terrain, not on the aircraft. The sun half
  is landed and works; only the ambient half is inert. *Cross-refs:* `BL-332`'s closing record in
  `PLAN-M5-polish-10` `B13`, `CAP-54`.

- `BL-803` `[Bug]` `[S]` `[Next: data]` `[Impact: high]` `[Evidence: feel]` **Enhanced Graphics lays a
  dithering pattern over the whole screen.** *Evidence:* reported at the controls under Enhanced
  Graphics as a "dithering effect over the screen"; which chapter, view and window size are not
  recorded, and the faithful presentation is not reported to show it. Nothing in the Enhanced
  environment asks for a dither on purpose: `Launcher.cs` builds the `WorldEnvironment` with SSAO,
  screen-space reflection, glow and an AgX tonemap (`CSVM/src/Session/Launcher.cs:1120-1132`), the
  sun's shadow with a blur of 1.0 whose own comment records that raising it "dithered the lit
  water" (`Launcher.cs:91-94`), and the clutter fade dithers only its own fragments
  (`SceneBuilder.cs:1557`). *Fix shape:* reproduce it, then bisect the effect by disabling SSAO,
  SSR, glow and the shadow blur one at a time (a `--no-*` style door if none exists) until the
  pattern goes, and fix that one pass: Godot's SSAO and soft-shadow passes both resolve with a
  screen-space noise that a low resolution scale or a half-resolution buffer leaves visible.
  *⚠ Traps:* Godot's own debanding is not enabled here, so the pattern is not that; a screenshot
  captured through `--shots` is a previous frame's render and carries the burst camera's dither
  (`CaptureDirector.cs:96`), so judge this at the controls or on an undithered capture.
  *Playtest after fix:* any chapter under Enhanced Graphics, still and moving, over water and over
  ground. *Cross-refs:* `docs/architecture/Utils.md` (`GraphicsMode`).

- `BL-899` `[Bug]` `[S]` `[Next: data]` `[Impact: low]` `[Evidence: data]` `[C5]` **C5 builds
  19,197 `fvol` cloud sprites that drew no pixels at either probed pose, before and after the fade
  law landed.** C5's `fvol` geometry is seventeen polygonal street prisms rather than a deck slab,
  and the field it scatters over their tops and ramps is the chapter's second largest by count.
  *Evidence:* two freecam poses, street level (`--chapter=C5 --pos=-9256,178,-3155
  --direction=-0.588,-0.1,-0.809`) and above (the same bearing at `y=600`), render byte-identical
  with and without the view-angle fade (0 px changed at both), where the same comparison at C1
  moves 40 to 47 % of the frame. A `--tex-override` on `cloud1.tif`/`cloud2.tif` at both poses
  paints **0** pixels, against 92,520 at the C1 1,700 m pose. So the sprites are built and their
  normals are right (the `cloud-field-fade` suite reads 17,095 up and 2,102 sloped of 19,197, none
  on a wall), and nothing of them reaches the frame. ⚠ **The face scatter did not change this
  reading**: the street-level pose is still byte-identical across it and `--tex-override` still
  paints 0, though the same override at `--pos=-7392,400,200 --direction=0,-0.604,-0.797 --no-fog`
  paints most of the frame, so the field does draw from some poses. *Where to look:* whether the
  prisms' authored 1000-1500..1200-1800 m band plus the view angle can ever admit a card from
  inside the street canyon, whether the cards sit inside solid geometry, and whether a per-view
  cull (`GameSession`/`WorldBuilder`/`WeatherRig`) drops the field in that chapter.
  ⚠ Two poses are not the chapter: sweep C5 from several altitudes and bearings before concluding
  the field never draws. ⚠ Not caused by the fade law, which is why this is its own item: the same
  two poses read 0 on the build before it. *Cross-refs:* `docs/formats/fogvol.md`,
  `docs/org/cloudCards.md`.
- `BL-905` `[Fidelity]` `[M]` `[Next: decide]` `[Impact: high]` `[Evidence: feel]` **Aircraft read
  too glossy under sunlight beside the original's screenshots.** *Evidence:* every aircraft
  surface (skin, canopy, props, cockpit interior) takes one procedural shader from
  `SceneBuilder.GetBiasShader`'s shaded branch with roughness 0.85, metallic 0.0, specular 0.5,
  the same in Original and Enhanced mode; terrain deliberately sets specular 0.0 with a comment
  that any sheen there is invented. No decode says how the original lights an aircraft (no
  material specular power is recorded in `docs/org`), so the 0.5 is a remake default, not a
  reading. *Fix shape:* a specular sweep on the viewer lab, the same plane at specular 0.5, 0.25,
  0.1 and 0.0 at roughness 0.85 plus one at roughness 1.0, rendered beside an original screenshot
  of that plane in sun, for the user to pick by eye; then a decode of the original's aircraft
  material (the D3D material the plane draw sets, and whether it is lit with a specular term at
  all) so the picked value has a reading behind it. *⚠ Traps:* do not settle it on a luminance
  distance; the pick is the user's. Water's roughness and specular are `BL-804`'s and stay. The
  change moves `viewer-bhawk`, `c1-flight-kill`, `c1-cockpit`, `c1-destroy-effects` and
  `empty-stage`; re-pin them with the picked value only. *Cross-refs:*
  `CSVM/src/Mech3/SceneBuilder.cs` (the shaded branch), `BL-804` (the water terms beside it),
  `docs/org/textures.md` (`SHADEMODE`).


## Effects & animation runtime

- `BL-796` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: data]` `[CM01]` **The two hangar hand-over props cannot take the player's livery: their subtrees carry no decal placeholder.**
  *Evidence:* `anim_bloodhawk`, the Bloodhawk standing on the hangar floor in CM01's drop, and
  `bloodhawk_gear`, the undercarriage the flown aeroplane wears on the lift, are both the player's own
  aeroplane and should wear the player's skins. `BL-690` built the route that dresses a staged prop in
  its aeroplane's livery, and it cannot reach these two: `PlanePainter.PrefixFor` reads the skin prefix
  off a subtree's own materials, and measured across the staged set only `balmoral` (`bal`) and
  `piratefighter` (`dev`) carry one. *Fix shape:* resolve the prefix some other way for these two, a
  node-name to prefix mapping or a read off the airframe the prop stands for, then hand them through
  the same `AircraftStage.Paint`. *⚠ Traps:* judge it at the controls first. The gear rides a painted
  aeroplane and the wrong-livery gear may not read on screen at all, in which case the mapping is
  not worth carrying. Do not invent a prefix for a subtree that has none: check what its materials
  actually name before mapping anything. *Cross-refs:* `BL-690`'s closing commit,
  `docs/formats/anim-definitions/cutscenes.md` "What a staged prop is painted in".
- `BL-674` `[Bug]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: data]` `[CM10]` **CM10's attack-balloon wave flies from 990 m down to water level and back up
  during its scripted entrance.** *Evidence:* driving C1/M05's shipped `OBJECTIVE10` wake and
  sampling the assembly every 0.1 s for 70 s traces its world Y from 990 m (the hidden entrance
  altitude) to -0.26 m at about t = 57.3 s, then climbing again at the `rise` sequence's authored
  3.33 units per second. The objective marker follows it down, which is the symptom `BL-656` was
  filed on; that item is disproven because the marker is tracking the geometry correctly, and the
  geometry is what goes to the water. The motion is the entrance's own SiScript-to-`rise` handoff,
  so it lives in the animation runtime (`PoseChannel.cs`, `FromToMotion.cs`, `ScriptPlayback.cs`),
  not in `ObjectiveSites.cs`. *Fix shape:* first decide whether the original does this at all, by
  watching a CM10 wave arrive in footage or at the controls; a balloon that dips to the sea on its
  way in may be authored. Only if it does not, find whether the handoff between the entrance script
  and `rise` drops an altitude the original keeps. *⚠ Traps:* do not add an altitude floor to the
  assembly, and do not offset the marker upward; both were removed on decoded evidence and the
  balloons descend as they attack, so no constant is right at two altitudes. The anchor rule itself
  is the original's (`FUN_004cf2c0`, midpoint of the node's active bounding box) and is correct.
  *Cross-refs:* `BL-656`'s closing commit, `PT-111`, `docs/org/targeting.md` "Where a mission
  structure is".

- `BL-293` `[Tuning]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **Rocket impact rings: the fixed-axis upper ring is faithful but reads
  poorly, parked** (PT-35). Faithfulness versus feels-good, decide later: the original
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
  (scale/opacity only, `docs/formats/weapon-effects.md`), so any change is engine-side and a
  remake-only rule over a decoded one.
  Cross-link: `BL-292` (crash-splash orientation, different spawn path; scheduled in
  `PLAN-m3-polish-10` A3).

- `BL-535` `[Bug]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` **A repeat sonic burst pays a 10 to 14 ms slot re-reset once the pool recycles a still-live slot.** With every emitter pre-built at bind (`AnimRuntime.PrewarmEmitters`), the weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08 --weapon-fire --infinite-ammo --weapon-surface=default --weapon-standoff=90 --no-det --no-vsync --seed=1 --no-pads --frames=1200 --screenshot=<path>`) with `hitchMonitor.floorMs` 10 and `medianMultiple` 1.2 in `CSVM/config.json` records `effect_checkout` samples of 15.6 ms and 12.2 ms in `.hitches.jsonl`; under the stock monitor it never trips. `AnimRuntime.ResetCheckedOutCopies`'s own def/anchor loop is cheap and constant (17 defs, 17 matched anchors every burst); the cost sits inside `RESET_STATE`'s `ObjectOpacityState` dispatch on the ring defs, `PoseChannel.SetSubtreeOpacity` → `ApplyOpacity`'s recursive subtree walk plus `WorldCollision.SetFaded` → `SyncSubtree`/`FadedAbove`, and lands exactly at the pool wrap (a per-burst `Stopwatch` shows burst 4 cheap, then the log's own `anim: effect pool for 'sonic_ground_effect' recycled slot 0 of 4 while it was still live` line, then burst 5 onward expensive), not two bursts before it as first measured.
  *Where to look:* isolate `ApplyOpacity`'s material/shader-param cost from `SetFaded`'s collider-resync cost before changing either, `EnsureOpacityPath`'s own `Shader.Code.Contains` scan is measured NOT to be the bottleneck (under 0.1 ms typically). Both are shared machinery well beyond the sonic burst; `WorldCollision._fadedRoots` is a single process-wide counter, so `FadedAbove`'s ancestor walk degrades for every currently-faded object in the world, not just this one, once more than one is faded at a time.
  *Cross-refs:* `BL-231` (closed; the pool-size judgement this was measured under), the `effect-pool-reset` suite (the pose contract the re-reset keeps).
- `BL-537` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Effect pools at four players, judged in play.** The pool sizes in `CSVM/data/effect_pools.json` were re-judged on a build with no first-use construction cost: rockets and the sonic burst never wrap, a four-object simultaneous death wraps `flame_ball_01` at 4 and 6 slots and is quiet at 8 (now shipped), and seven or more identical deaths in one frame wrap at the 16 ceiling and cannot be sized away. At the controls the single-player half reads right: four fireballs burn out in place, and the seven-death wrap is not visible under the debris. Still owed: a 4-player splitscreen session with everyone firing, judged for anything that reads as shared between panes, and the ceiling for many-player builds (at 16 players the default root wants 19 and gets 16). The instrument is `AnimRuntime.PoolRecycles` and the `anim: effect pool for '<name>' recycled slot` DEBUG line in the log file sink; the sizes staged print on the world-effects build line. ⚠ Raise only a root that logs a recycle, never the default; the three gun roots stay at 1; a root sized 0 clamps to 1. Each slot copies the root's subtree (155 templates at 1 player, 263 at 4).
  *Cross-refs:* `PT-129` (the four-player flight that judges it), `BL-535` (the per-burst re-reset cost measured under the same instrument), `BL-296`/`BL-299` (the other splitscreen-scoped items).

- `BL-720` `[Bug]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: data]` `[CM24]` **The Dante's
  engine fires moved between the engines because the effect-template pool was shorter than the
  engine bank; the sizing is fixed and the report is owed a look.** *Evidence:* the mechanism is
  settled from the mission data and measured headless. The Dante has its OWN engine definitions,
  not the pirate zeppelin's: `damage1_..3_dtz?engNN` and `destroy_dtz?engNN`
  (`extracted/C5/M04/mis_anim/`), each binding its engine's nodes by compiled pointer, so no anchor
  ever resolved to a sibling engine. An engine death plays `dt?eng_destroyed.flt` WITH its
  `lengNN`, `dtzep_rocks{left,right}`, `zep_engine_boom` AT that engine at 0 s and again at 5 s,
  and `large_30sec_fire` WITH that engine's own `supports`. Only the last of those carries a
  lasting puffer (`fire_n_smoke` at `INPUT_NODE`, half a minute with no authored stop), so the
  concurrency is one template copy per burning engine and the bank is fourteen. `effect_pools.json`
  sized `fire_here` at the default four, and that pool being finite at all is the remake's own
  approximation: the original's start gate refuses a repeat call only while the concurrency bit
  `0x100` at `+0x9c` is clear, and the per-start reset sets that bit for a template-rooted
  definition, at which point every further call clones a whole record and deep-copies the template's
  nodes with no cap (`docs/org/sequences.md`, which had the bit recorded as unattributed).
  `dante-engine-fires` reads the whole bank killed in turn
  and logged ten pool recycles with four fires alive for fourteen dead engines, the first engine's
  fire having moved to the newest kill; the same suite is green at one copy per engine. *⚠ Traps:*
  the hull's own `dtzep_rocks*` roll swings an engine at the ends of the hull metres per frame, so
  a fire's fed position must be read against its `supports` node at the SAME instant; a stale
  expected position reads as a misplaced puffer. `large_10sec_fire` shares the `fire_here` root and
  therefore its cursor, and `TemplateStage.TakeNextSlot`'s liveness test is per definition, so a
  crate's ten-second fire can still wrap onto a live thirty-second one; that hole is untouched here
  and is what to suspect if the symptom survives with the pool deep enough. *Playtest:* `PT-139`.
  *Cross-refs:* `BL-231` (the pool's tuning entry), `BL-121` (the `trail-world-anchor` suite),
  `BL-700` (the same zeppelin family's wreck rest).

- `BL-867` `[Tuning]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **The speed cue's wisps read more opaque than the
  original's.** *Evidence:* reported at the controls against mission recordings: the pale wisps
  each chapter's `speed_cue.zrd` emits ahead of the player (`Flight/SpeedCue.cs`, three authored
  `cuepufferN` states selected by altitude) are far more prominent than in the footage; size reads
  right, opacity does not. The alpha comes from the authored state, so a halved constant would be
  a departure from data; CSVM's own contribution is the puffer's judged sprite-size stand-in
  (`Puffer.SizeScaleDefault`) and the blend the texture header names. *Fix shape:* a montage of
  the cue beside a mission recording at matched altitude first, the user judges it, then move the
  constant the montage names: the blend or the size stand-in before the authored alpha, a
  cue-only alpha factor last. *⚠ Traps:* three cloud populations share textures
  (`BL-118`'s note under the cloud items): judge the wisps by altitude and by their position
  ahead of the aircraft, never by texture. *Cross-refs:* [`docs/formats/effects.md`](docs/formats/effects.md)
  (the cue's data), `docs/org/puffer.md`.


## Audio

- `BL-252` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Overspeed-whine volume** (`prop_sound`). `CAP-10` plus a
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
  (Hoplite and autogyro): hold straight and level at 100% throttle, which by definition settles at
  max speed, then dive, and the sound starts exactly as the speed goes past it. That is `1.0×
  `fd_speed``, i.e. precisely the foot of the shipped `prop_sound` curve (volume 0→0.5 over 1.0→1.1×
  `fd_speed`), so **the gating needs no change**. ⚠ **Do not read a threshold off the airspeed dial:
  the gauge art, including its red arc and its `300` mark, is the same for every plane** and so
  cannot express a per-plane limit, a trap this entry walked into once already.

  `CAP-10`'s Bloodhawk footage times the edges and agrees: the 400–900 Hz band steps up at t≈5.2 s
  and back down at t≈11.7 s in `CAP-10.mp4`, while the needle crosses the corresponding dial position
  at t≈5.0–5.5 and t≈11.5–12.0, both edges inside ~0.3 s, and sharp rather than a continuous swell,
  as a ramp band crossed in well under a second should look. `CAP-10 2.mp4` repeats it (audio on
  t≈7.5, off t≈17.0).

  What is *not* settled is the volume: the +2 dB measured here is a band-limited figure, not a
  loudness, and the mix ratio differs by view because the original's **cockpit** engine is damped
  while ours is not, so it cannot be read across. **Needs a level match by ear against the
  original, not another measurement** (user, 2026-08-04: "the only tune parameter would be volume").
  *Cross-refs:* `PT-126` (the flight that names the sound and matches its level).

- `BL-269` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The 3D sound falloff curve between the authored `RANGE` radii is an admitted
  approximation** (`WorldSounds.cs:159-161`, endpoints authored, curve "an approximation of
  the original's, hence TUNE"). Low stakes per sound but global: every positional sound's
  audible footprint. A calibrated fly-past recording of one loud fixed emitter (the C1
  refinery flare is a candidate) would trace the real curve.

- `BL-281` `[Tuning]` `[Blocked: CAP-27]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The ricochet sounds are audible but very faint.** `PT-25` (c), 2026-08-05:
  `snd_ricochet1–4` play under the per-impact spark burst but sit too low to read. A mix-gain
  question with no reference recording behind it, to be judged at the controls rather than derived. ⚠ Judge only after `CAP-27` decides
  whether the original has this effect at all: `BL-090` already calls the 0.99 `injure_anims`
  entry that drives it "plausibly an authoring leftover", present on 1 of 11 aircraft, so the
  capture may delete the feature rather than tune it.

- `BL-285` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Engine start/stop residues from `BL-267` (landed 2026-08-05)**, two constants
  pending the user, both in the same cockpit sitting. (a) `ThrottleSlamSmoke.SlamThreshold`
  0.25: the CAP-21 footage only bounds the slam gate to "a single 1/8 step never fires,
  idle→5/8 fires", the 2/8–4/8 band is unobserved, so 0.25 is the smallest threshold
  consistent with both and a declared TUNE. (b) The listen A/B: `EngineStartRamp` is now the
  `startprops` authored 2.0 s and the crash/destruction wind-down plays `snd_propstop`, judge
  both by ear. ⚠ Trap: `BL-268` (`PLAN-m3-polish-10` C21, landed 2026-08-06) removed
  the blanket ×0.2 mix scale on these same paths, raising the own-ship mix ~5×, judge the
  ramp/stop cue against the new, unscaled level, not the old ×0.2 one. ⚠ Second trap, added by
  `PLAN-splitscreen-polish` D32 (`BL-371`, landed 2026-08-15): `snd_propstop` (the wind-down
  half of this A/B) now carries splitscreen's `MixGain` too, 1 in 1P, so this pending single-pilot
  judgement is unaffected, but a splitscreen listen must judge it at whatever `N` the pilot is
  testing, not assume the 1P level. `snd_propstart` (the other half of this A/B) is unchanged,
  D32 kept it raw, "your prop" on respawn stays loud on purpose.
  *Cross-refs:* `PT-127` (the cockpit sitting that judges both).

- `BL-391` `[Tuning]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **Own-ship engine loop reads too loud, including single-player.** Found
  2026-08-15 at the `BL-126` splitscreen chrome playtest, a 4-player Dogfight session flagged the
  stacked engines as too loud, but the user confirmed on a follow-up single-player listen that the
  base engine level itself, not just the splitscreen stacking, is too hot. Not a splitscreen item:
  `FlightAudio.MixGain` is `1` in 1P (no attenuation applies), so this is the vehicle.json
  `engine_sound` mix level (or the detuned dual-voice stack's combined gain, `EngineDetuneRatio`)
  read too loud on its own terms. *Fix shape:* a level match by ear against the reference video,
  the same method `BL-269` already used for this signal chain.

- `BL-782` `[Feature]` `[Blocked: BL-455]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Built-in's Options screen carries no audio
  levels, so the mix is settable in the Original presentation alone.** *Evidence:*
  `PLAN-audio-preferences` builds the AUDIO page for Original and puts four levels (Master, Music,
  Effects, Voice) in the shared options store, which is where every setting both presentations show
  already lives. Built-in's Options screen holds seven steppers (difficulty, menu presentation,
  graphics mode and the four display settings) and the Controls door, and its apply hands back the
  four levels untouched, the only settings it does not show, so once the page lands a player on
  Built-in can hear the mix and not reach it. Blocked until the four levels exist in the
  store, which is the whole of the dependency: nothing else about this item waits on that plan.
  *Fix shape:* four rows on Built-in's Options screen reading and writing the same store fields the
  AUDIO page does, in the screen's own stepper convention, applied through the same
  `OptionsApplyExit` the seven current rows leave by. The four display rows landed on that screen
  are the worked model: read the store word, step over the row's own values, write the word back.
  *⚠ Traps:* Built-in has no continuous control of any kind, so a 0 to 100 level is a stepper with a
  chosen step rather than a slider, and the step size is a judgement the row has to make rather than
  inherit. Do not add a second writer: `Launcher.ApplyOptions` is the options file's one writer and
  both presentations reach it through the apply exit. Do not re-tune `MusicPlayer.ChannelLevel` on
  the way past; `BL-455` deletes it.
  *Cross-refs:* `BL-455` (the page this mirrors), `PLAN-audio-preferences`,
  `git log --grep=BL-783` (the same gap for the display settings, closed by four rows on this
  screen), `docs/menu-presentations.md`.
- `BL-837` `[Fidelity]` `[S]` `[Next: data]` `[Impact: low]` `[Evidence: decoded]` **The chase camera's throttle
  transient is authored per real second but is stepped on the sim clock, and the two clocks were
  never measured against each other.** *Evidence:* `BL-816`'s decode has the easing dt as
  `GetTickCount` wall time per rendered frame; `CSVM/src/Flight/CameraController.cs:526` (at
  `7d2e9881`) runs from `FlightController.cs:1743`'s sim step, so `dist_catch_up` is applied per
  sim second. Under `--det` and on a rig where the sim clock lags wall time the settle is slower
  than authored. *Fix shape:* log both clocks over the flown C1 chase shot; if they diverge, step
  the easing on the engine's wall clock (the pinned goldens hold under `--det` only if the
  deterministic clock is what the easing reads, so decide that first). *Cross-refs:* `BL-816`'s
  closing commit, `docs/verification.md` DET-11, `docs/formats/camparam.md`.

## Cameras & views

- `BL-150` `[Research]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The numpad camera scheme is the head-look controller,
  and it is built; what is left is the footage's own limits.** The nine "fixed views" are not
  authored poses: the chase placement `FUN_0042c7f0` runs the same head-look state machine the
  cockpit views run, floored at `−π/2` instead of level, and the pad cluster is that controller's
  own snap input. CSVM builds it that way, one `HeadLook` per pilot swung onto the chase offset by
  `CameraController.ChaseSwing`, so the entries below are now this scheme's *evidence* rather than
  its specification. The measurements stand and the reconciliation against them is recorded here.

  (a) **Layout, MEASURED 2026-08-04 from the `CAP-07` scripted re-take, all nine keys**, read off
  nine settled stills (method and confidence below). The snap table's angles, swung by
  `ChaseSwing`, reproduce every one of the nine:

  | key | camera sits | reproduced by the snap table |
  |---|---|---|
  | `Kp1` | **ahead + starboard, below** | elevation 45° up, azimuth 135° left ✓ |
  | `Kp2` | **dead ahead, level** | azimuth 180°, at the rig's own base elevation (`BL-885`) |
  | `Kp3` | **ahead + port, below** | elevation 45° up, azimuth 135° right ✓ |
  | `Kp4` | **starboard flank, level** | azimuth 90° left, at the base elevation (`BL-885`) |
  | `Kp5` | **the centre key, a no-op from base** | `LookCenter`, which recentres the head |
  | `Kp6` | **port flank, level** | azimuth 90° right, at the base elevation (`BL-885`) |
  | `Kp7` | **astern + starboard, below** | elevation 45° up, azimuth 45° left ✓ |
  | `Kp8` | **directly below (belly plan view)** | elevation 90° up, azimuth 0 ✓ |
  | `Kp9` | **astern + port, below** | elevation 45° up, azimuth 45° right ✓ |

  A pilot looking LEFT is what carries the camera to starboard, which is why the labels and the
  positions read mirrored. The four corners come out below the aircraft and carry the fore/aft term
  the stills show (bottom row forward, top row aft) with no fore/aft term in the table at all: it
  falls out of composing a 45° elevation with a 45°/135° azimuth. The three "level" entries are level
  only up to the chase rig's own base elevation, 15.7° in CSVM against the authored 0.29° the
  original places at, which is `BL-885` and not this item.

  *How it was measured.* Nose-in-image direction plus which surface is visible fixes the quadrant
  analytically: with image-right = `u × d`, the nose projects with horizontal component ∝ sin φ and
  vertical ∝ −sin ε · cos φ (φ = azimuth from dead astern toward starboard, ε = camera elevation
  *below*). The level side views calibrate the sign, a camera to starboard must show the nose
  pointing image-right, and `Kp4` does. The four corners all show belly, underwing ordnance and the
  ventral skull fin, so ε > 0 for each; their nose directions are up-right / up-left / down-right /
  down-left for 1 / 3 / 7 / 9, giving the four quadrants above.
  ⚠ **Read the limits.** These are *quadrants and signs, not degrees.* Getting degrees needs the
  render's field of view, and **a self-calibration attempt on this clip failed, do not repeat it.**
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
  known-geometry object in frame, the aircraft's own wingspan from the mesh at the known shipped
  `dist` would do it directly, which is the cheaper route and needs no new footage.
  `Kp8` is the weakest of the nine: at a near-vertical elevation the azimuth is degenerate, so
  "directly below" rests on the plan-form silhouette being unforeshortened plus visible underwing
  ordnance (occluded from above), not on the nose-direction solve. One take, one aircraft, the
  layout is a per-key constant so that is fine for the table, but do not read distances off it.
  ⚠ **Correction, 2026-08-04: `Kp0` is rudder-left, not a camera view.** The 2026-07-30 cockpit
  session read it as a second 45°-underside-front view alongside 7 and concluded "the original binds
  0; we bind none", that was a misattribution, and the *camera* half of it is withdrawn. Our
  omission of `Kp0` from `Views[]` is therefore **correct** and needs no change; the camera set is
  `Kp1`–`Kp9`. (Whether `Kp0`/`Kp.` should drive rudder at all is a separate input question this
  entry does not own.) The underside-front position stands for 7 on its own.
  (b) **Motion is an exponential ease, and the decoded rate is what CSVM runs.** **Measured
  2026-08-04 from the `CAP-07` scripted re-take, 16 transitions (a press and a release for each of
  the eight moving keys).** The measurement corroborates the decode rather than setting the rate:
  the head's azimuth smoothing is a decoded **5.0/s** (`docs/org/cameraViews.md`) against the
  **≈ 5.4/s** in sim seconds fitted below, so the constant no longer waits on an FOV calibration.
  ⚠ **The "ease reads linear" claim this entry carried is WRONG, the ease is exponential.** Sky
  travel was tracked as the cumulative frame-to-frame displacement of matched star points, which is
  a monotone proxy for camera rotation and needs no FOV. Normalised, the profile is heavily
  front-loaded: **33% of the travel in the first 10% of the move, 93% by the halfway point.** Fitting
  `v(t) = 1 − e^(−kt)` gives rms **0.012–0.080** against **0.37–0.50** for a linear ramp, the wrong
  model by a factor of 6–40, on every one of the 16 transitions. A smoothstep is worse than linear.
  - **Rate `k` = 7.50 ± 1.62 /s on the press, 7.70 ± 3.20 /s on the release** (wall seconds), the
    two agree well inside their spread, so **the ease is symmetric out and back**, which is the one
    part of this entry's original claim that survives. 90% of the way in ~0.30 s wall.
  - ⚠ **Those are WALL seconds and the original's clock runs fast (k = 1.390, `FINDINGS.md`).** In
    sim seconds the constant is **≈ 5.4 /s**, 90% in ≈ 0.43 s. Implementing 7.5 would run the ease
    39% quick, the same trap `BL-148` documents for the stall blink.
  - **The form is exactly the head's own smoothing**, `shown += (target − shown)·k·dt`, which is the
    law `HeadLook.Approach` runs at the decoded 5.0/s in azimuth and 3.0/s in elevation. The camera's
    own offset lerp (`CamSmooth` 8 /s, `CamRotSmooth` 7 /s) then sits on top of it.
  - ⚠ **`k` is a lower bound, the shape is not.** The tracker undercounts the fastest 1–2 frames of
    each slew (see the calibration note in (a)), and undercounting the early, fast part biases `k`
    *down* and makes the curve look *less* front-loaded than it is. The exponential-vs-linear verdict
    therefore only strengthens under the bias; the constant itself wants a re-measure once an FOV
    calibration exists and the rotation can be integrated as an angle rather than a pixel proxy.
  (c) Distance: resolved, a snapped chase view takes the per-plane shipped distance with the chase
  camera (`CameraController`).
  (d) **Combined keys ADD as numpad-direction vectors, MEASURED 2026-08-04 from the `CAP-08`
  scripted combo sweep, 14 staggered combinations.** Landed, and not as a rule of its own: the
  bindings put `Kp7`/`Kp8`/`Kp9` on Look Up and `Kp7`/`Kp4`/`Kp1` on Look Left, so several keys down
  compose one direction, which is the summed offset this measurement found.
  - **The second key is never ignored.** In all 14 steps the silhouette after adding the second key
    differs from the first key's own settled silhouette at mask IoU **0.096–0.530**, against a
    repeatability floor of **0.833–0.978** measured from the same key held alone in two different
    steps (7 such pairs, up to 90 s of hand-flown drift apart). Nothing is close to the floor, so
    "the first key wins while it is down" is refuted outright.
  - **The four OPPOSITE pairs return the camera to the base chase view**: `1+9`, `3+7`, `2+8`, `4+6`
    give IoU **0.992 / 0.986 / 0.995 / 0.988** against that step's own settled base frame, at or
    above the floor, i.e. *the same view*. Independently corroborated on two of them: releasing
    `2+8` and `4+6` produced no camera motion at all (0.7 and 3.2 MAD of silhouette rate, against
    33.6–102.1 for the 14 unambiguous press-alone slews), because the camera was already at base.
  - **The two TRIPLES collapse onto their middle key**: `7+8+9` matches `Kp8` at IoU **0.891** and
    `1+2+3` matches `Kp2` at **0.955**, both inside the floor band, and both confirmed by eye
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
  than roughly a quadrant, so it cannot say *which* intermediate position an adjacent pair reaches,
  the 8 blends confirm only "not any key, not base". What carries the law is its six *exact*
  predictions (four cancellations, two triples), all six of which hold. Pinning a blend's actual
  azimuth needs the FOV calibration (a) is still missing. (ii) **Mid-ease interrupt on RELEASE is
  still untested**, the rig leaves a 3.5 s gap, so every ease-back completes before the next press.
  What *is* tested is a mid-ease *press*: the triples' third key lands 15 ms after the second, while
  the camera is still slewing, and the endpoint is exactly the held-set's resultant, so an arriving
  key re-targets rather than restarting. (iii) One take, one aircraft; the layout is a per-key
  constant so that is fine, but do not read distances or degrees off it.
  *Method:* `analysis/numpad-combo-views/` (`pass1` → `sync` → `events` → `decide` → `match` →
  `settle` → `stills`). The clip is VFR (frame intervals 16.67–41.70 ms), so everything is placed on
  PTS, never `frame/fps`. The rig's tooltip did not survive the capture, so the log was aligned to the
  video by detecting slews and matching the schedule's asymmetric cadence: **video_t = log_t + 3.978 s**,
  26/28 anchors within 350 ms at sd 27 ms, against 15/28 at sd 54 ms for the press/release alias.
  Both clocks are wall clocks, so the sim-clock factor does not enter.
  (e) **No gamepad binding existed in the original** (a right-stick/right-stick+modifier scheme would
  be invention) and **`Kp5` moves the camera nowhere, measured 2026-08-04, not assumed.** Held for
  3.5 s in the `CAP-07` re-take, the frame deviation from its own pre-press baseline is **0.310**,
  *below* the 0.348–0.360 a no-key stretch of the same length scores, and against 2.5–10.5 for every
  key that does move the camera. ⚠ **That is not evidence the key is unbound**: `Kp5` is the
  controller's centre slot, labelled "Look Forward" in the original's own binding menu, and the
  camera was already at base when it was held, where recentring is a no-op. CSVM binds it to
  `LookCenter` for that reason.
  (f) **Numpad + / − trim camera distance slightly**: landed as the decoded External Camera Zoom
  axis (`BL-433`), not as a trim of this scheme's own.
  *What is left:* the pieces the footage cannot settle. (i) The exact azimuths and elevations of the
  eight blended two-key positions, which mask IoU only bounds; the implementation reaches them by
  composition and nothing measured contradicts it. (ii) (b)'s fitted constant as an independent
  check on the decoded 5.0/s, which wants the FOV calibration (a) is missing. (iii) The feel of the
  whole scheme at the controls, which is what `[Owed-playtest]` is for.
  ⚠ **`CAP-07` took two takes; the first is rejected and must not be re-analysed.** In
  `CAP-07 Numpad 1,2,3,6,9,8,7,4.mp4` the presses overlap: 10 camera transitions for 8 keys in
  20.9 s, with direct position-to-position lerps that never pass through base, so only 6–7 of the 8
  holds come to rest and the filename's key order cannot be mapped onto them one-to-one. The usable
  take is `CAP-07 scripted Run.mp4`, driven by `analysis/capture-rigs/NumpadViewSweep.ahk`, one key
  held alone at a time with a return to base between, and a `sweep-log.txt` that timestamps every
  press, so key windows are read from the log rather than inferred from motion.
  All nine holds *do* settle: frame-to-frame motion over the last 1.2 s of each is 0.055–0.278
  against a 0.090 baseline.
  ⚠ **Traps.** (a) **Only `--view=` is machine-verifiable**, live held-key input cannot be scripted
  here, so the built scheme is correct-by-construction until it is played; do not close this off a
  passing `--view=` capture alone. (b) The table above is quadrants, not degrees, so the exact
  azimuth/elevation still wants an FOV-calibrated solve, and the "level" rows are level only up to
  the chase rig's base elevation (`BL-885`). (c) Don't retune the chase radius here in isolation, a
  snapped view and the chase camera share one number in `CameraController` by design; a fix landing
  only in one place desyncs the two cameras again. (d) The combination law is pinned by six exact
  predictions but the eight blended positions are only bounded, so **do not quote a blend's azimuth**
  as if it had been measured; the four cancellations and the two triples are the cases with a right
  answer to check against.

- `BL-266` `[Research]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **Plane wobble: residual decode questions after the
  wiring landed.** The oscillators are wired (`ShakeDefs`/`PlaneShake`, visual-only roll on the
  plane node; law and measurement in [`docs/formats/shakes.md`](docs/formats/shakes.md) and
  `analysis/gun-wobble-shake/`). The dressing behind the shake is a decoded camera
  random-walk (`crimson.exe`), not the remake's dated sawtooth, details below.
  **Resolved (landed on `main`):**
  - **(a) made faithful**, 2026-08-19 the fire source now IS the original's random-walk
    accumulator (`PlaneShake.FireBullet` steps `Walk += (rand−0.5)×2·(factor×caliber)·2.0·6.2832·1.2`
    = uniform ±7.54·(factor×caliber)/shot, wep40 ±2.11e-2 rad, decoded from `FUN_0042be10`; decayed
    by the authored `damp` τ≈80 ms). Merged to `main` (`eba69782`, branch experiment `bl266-random-walk`).
    **`GunBuzzKickScale` (default 1.0 = faithful) is the one tune knob, dial it, never
    `magnitude_factor`.** The `camera+0x24` consumer traced NEGATIVE (2026-08-19), that negative is
    the FIRE block only; the high_speed finding below (same `FUN_0042be10` writer on block 4,
    `camera+0xd4/+0xd8/+0xdc`) shows the mechanism is live, so the fire gap is a **mechanism/law
    mismatch, not a render-pipeline loss**, this port is the first real feel of the kick law.
    (The old approach-(B) suspects are also settled: fire-rate is one round per tick at authored
    `FIRE_RATE` (8.0 for wep_40), the "12–13/s" was a redraw-window artifact, and 60 fps
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
  - (c) the `ON_CALL` `small/medium/large` `damage_shakes` defs are the AI half of the five camera
    kicks, not script calls: `FUN_00473430(index)` plays them on any vehicle that is not the
    player's, from the same five sites ([`docs/org/shakes.md`](docs/org/shakes.md), "The seven
    component blocks and every kicker"). The nitro engage is wired
    (`FlightController.AdvanceNitro`); a round fired, a round taken, the overspeed arm and a
    collision contact are decoded and still unwired.
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
  - **(e) `nitro` is the third source with the same modelling gap.** Judged at the controls on a
    nitro engage: "it wobbles the plane, amplitude and frequency not quite right". ⚠ **The plane
    wobbling is the decode, not the defect**, [`docs/org/shakes.md`](docs/org/shakes.md):17-22 has
    the plane wobble with a plane-mounted camera inheriting it, so only the magnitude and rate are
    in question, and `PT-86` asked for a *camera* shake it should not have. `shakes.zrd` authors
    `nitro` as `frequency 4.0, damp 3.0, sawtooth 1, magnitude 0.05`, read unchanged, and
    `PlaneShake` renders it as a sawtooth under an envelope, the same envelope-versus-random-walk
    mismatch as (a) and (d), which is why this is a clause here and not its own item.
    **Judged at the controls, and the answer settles the shape rather than the scale:** "Janky at
    the beginning (larger but very fast) and then too small but still very fast." That is three
    facts at once, the opening kick is too big, the decay to too-small is too quick, and the RATE
    is wrong for the whole duration. A magnitude constant cannot produce that; it is the sawtooth
    standing in for a random walk, which reads as a fast regular buzz where the original wanders.
    So the mechanism is the fix here, exactly as in (a) and (d), and the factor-of-two ambiguity in
    the magnitude is secondary, do not spend another pass on it before the walk lands.
    ⚠ **Still do not wire a number:** two repo sources contradict each other on which triple is
    position and which is velocity (`docs/org/shakes.md`:168-173 against
    `analysis/gun-wobble-shake/FINDINGS.md`:168-182, which says the reverse twice), and the
    `sawtooth 1` branch constant coincides with the authored `frequency` of 4.0, which is exactly
    the coincidence the trap below warns about.
    *Still unanswered:* whether the original's engage moves the nose or only the roll.
  - **(fidelity) judge the port, then dial.** Playtest owed: fly the merged build and judge
    `GunBuzzKickScale` (1.0 default = faithful step) against the original clip before touching it.
    Two honest caveats: the random-walk **decay model (τ≈80 ms) is an engineering guess, not a
    decode** (the original `camera+0x24` consumer is negative, but that negative is the FIRE block
    only; `high_speed`'s accumulator is at `camera+0xd4/+0xd8/+0xdc`, a different block, see (d) above);
    and with the buzz now ~7× louder, the quiet `554edcee` dive rattle reads ~6× softer than the gun
    because the engine's `_speed` is a sawtooth where the original is a random-walk, track the (d)
    engine-port + playtest.
  ⚠ Traps: `SHAKES_CAMERA` is NOT the fire-path shake mechanism, its sole carrier among all
  48 weapons is `wep_26` "FW", a zero-damage scripted fake weapon (a scripted detonation-shake
  marker); the fire path is the unflagged `fire_bullet` source. And the near-match trap: several
  magnitude candidates coincide with authored constants, wire nothing on one coincidence (the
  caliber law stood because the candidates separated by an order of magnitude each way).
- `BL-420` `[Research]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **Decode the original's per-view base FOV from `crimson.exe` and record it under `docs/org/`, the engine holds a single 62° assumption that the binary refutes.** The original's camera projection has **exactly two base horizontal FOVs, 60° and 80°, both stored in radians as half-angle constants** (`1.0471976` = `92 0a 86 3f` and `1.3962634`), and **which one applies is gated per-camera-mode** (live mode at `camera+0x14c`, selected in `FUN_0042b660`): mode **6** → 80° (`FUN_006024d9`), every other mode (0–5, 7, 8, 9) → 60° (`FUN_00602508`). Modes 6 and 7 are the only two first-person views (both set the `DAT_009fd17c` first-person flag via `FUN_004e7100`, both route through the first-person placement `FUN_0042d980`, neither uses chase-position math, `FUN_0042dc20`/`FUN_0042c5c0` dispatch). So the three named views resolve definitively: **3rd Person / chase = 60°; Cockpit view = mode 6 = 80°; Nose view = mode 7 = 60°**. The cockpit/nose assignment is pinned by a direct render gate: `FUN_0049fb00` (the per-frame player render, sole caller `FUN_004a0220` = main tick) draws the cockpit interior model `cockpit1` (`DAT_0071c314`) **only when mode == 6**, so mode 6 is the interior cockpit view (80°), and mode 7 is the no-interior forward view (60°). The two first-person views also share the **same camera position**, both place the camera at the plane's `cockpit_camera` marker (`DAT_0071c328/32c/330`), so there is **no separate nose-camera offset**; mode 7 differs only in hiding the interior/hull, keeping player head-look without autohead, and using 60°. The constants are **horizontal**, and the correction `FUN_006024d9`/`FUN_00602508` apply is the plain one at the 4:3 the original ran: `atan(tan(H/2) / (4/3))` → 60°→46.8° vertical, 80°→64.4° vertical, both **4:3** figures rather than 16:9 ones. ⚠ The factor 0.75 reads equally as `1/(4:3)` and as `(4/3)/(16/9)`, so arithmetic cannot separate them and the 4:3-only mode list is what decides it (`docs/org/cameraViews.md`, "FOV constants and aspect correction"). The project's current single **62° vertical assumption does not exist in the binary**, the 62°-in-radians constant `1.082104` (`63 82 8a 3f`) is absent, so the assumed number is unsupported and the correct base is 60°.
  *Evidence:* ghidra-mcp read of the open `crimson.exe` (`/crimson.exe`): `FUN_0049fb00` (player render; draws `cockpit1` `DAT_0071c314` only when mode==6 via `FUN_004cca30(x,1/0)` around the interior draw), `FUN_0042b660` (mode gate), `FUN_00602508` (60° H-FOV; writes `_DAT_00a1eff0`/`_DAT_00a1eff4`), `FUN_006024d9` (80° H-FOV, mode 6), `FUN_0042b570` (frustum/projection, contains `0.5235987755982` = 30° = 60°/2), plus the 60°/80°/50.0/2.5 constants side-by-side at the data table `0060409c`. FOV is stored in radians (anim loader `FUN_00502da0` converts degrees→radians via `0.017453292`). The `0x3f860a92` 60° literal is also used by `FUN_0049d940` (player aim camera) and `FUN_004a0220`. Camera object is `DAT_0064ef78`. Placing the camera: both first-person modes run the same placement `FUN_0042d980`, which sets the camera to `plane_pos + plane_rot · (DAT_0071c328,32c,330)`, i.e. the plane's `cockpit_camera` marker offset (bound in `FUN_00473480` from the `cockpit_camera` node; default fallback `DAT_0075d1b8/bc/c0` = `(0,0,0)`). Plane-model `cockpit_camera` node translations (decoded from `extracted/C1/... planes/nodes.json`) put the camera on the fuselage centerline a bit above the local origin, default fighter `player_pfighter`: `(0, +0.75, −0.2)`, with +Y up, ±X the wingspan (ailerons at ±63, elevators/tail at −Z ≈ −37), so +Z = nose/forward and the marker is centered, ~0.75 up, marginally aft of the origin. There is **no `nose_camera` node or per-mode offset**, mode 7 reuses the cockpit_camera point. The `cam_anim` ZAN cockpit sequence (`player-gi_1stperson`) carries no FOV (it shows the interior/hides the plane via `cockpit1`/`camera1`), so the base FOV is not authored in `.ani` data.
  *Fix shape:* **the decoded facts landed as [`docs/org/cameraViews.md`](org/cameraViews.md) (2026-08-18), and the mode-6/mode-7 first-person half of the model landed in code** (`PLAN-cockpit-view` A3): `CameraController.HorizontalToVerticalFovDeg` renders Cockpit at 80° H and Nose at 60° H, both aspect-corrected off the live viewport, deliberately scoped to those two new modes only (Decision 3, "new modes only") and never touching the engine's 62° global. **What remains is the EXTERNAL half.** `GameSession.cs:475`/`:2624` and `Launcher.cs:490` still write the single 62° vertical global to every chase/fixed-numpad/back/pad-look/crash camera. Migrating those three sites to the decoded 60° horizontal base (with the aspect-corrected 46.8° vertical this page already pins) is the remaining work, and it unsettles two judgements made against the current 62°: `PLAN-overcast-match` line 1463's overcast sky match and `docs/org/tracers.md:258`'s tracer calibration. Carry that warning into whichever session does the migration, both need re-judging after the base FOV moves, not just re-measuring against the same footage.
  *⚠ Traps:* (i) **The two `CAMERA_STATE`/`CAMERA_FROM_TO` functions (`FUN_00502da0`, `FUN_00503e70`) are animated/in-script FOV changes only (`.ani` H/V_FOV events), not the base per-view FOV; do not wire the engine's base FOV to them.** (ii) **The 80° is attached to camera mode 6 specifically, not "first person" generally**, mode 7 is also first-person but is 60°, so gating on "is first person" alone would read the mode-7 number wrong. (iii) The `Virtual Cockpit` string is a HUD/perf/zoning label (`FUN_0059c340`), not a view, ruled out. (iv) ~~Which of cockpit vs nose is mode 6 (80°) vs mode 7 (60°) was not pinned~~, **resolved**: the `FUN_0049fb00` render gate (`cockpit1` drawn only when mode==6) pins mode 6 = Cockpit (80°) and mode 7 = Nose (60°). The remaining subtlety is that **both modes share the same `cockpit_camera` position** (no separate nose offset exists), so "nose" is a render/head-look/FOV variant of the same camera point, not a physically different marker. (v) "62°" invariants elsewhere are the assumption being corrected, not corroboration.
  *Cross-refs:* `docs/org/cameraViews.md` (the landed Nose view shares the Cockpit camera point but uses the 60° base), `BL-150` (numpad fixed-view FOV calibration is still missing, a documented 60°/80° base + the aspect conversion is the calibration input it needs), `docs/formats/camparam.md` (chase/tuning only; does not cover FOV), `PLAN-overcast-match` line 1463 and `docs/org/tracers.md:258` (the 62° assumption to correct), `PLAN-cockpit-view` A3 (landed the mode-6/7 half of this model).

- `BL-432` `[Feature]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The original selects head-look behaviour by key; CSVM infers it from which
  device moved, a recorded behavioural difference.** `OriginalScreenshots/Keybinds Views 1.png`
  binds `K` **Access Snap Look Mode** and `J` **Access Smooth Look Mode**, both free in CSVM's
  flight scheme today. The look-state byte the head-look controller reads (`DAT_0064ef68`,
  `PLAN-cockpit-view`'s decode of `FUN_0042d010`) is a mode selector with three values, `0`
  snap, `1` free-look, `2` padlock (`BL-399`), that the original's player flips explicitly with
  these two keys. `PLAN-cockpit-view` C21 instead infers the mode from the input source: the
  numpad snap cluster snaps, the mouse/right stick pans smoothly, and both are live at once rather
  than one active mode at a time. That is a genuine behavioural difference, not just an unbound key:
  the original's player cannot free-look while snap is the active mode (or vice versa), and CSVM's
  player always can.
  *Fix shape:* either wire `K`/`J` as an explicit mode toggle gating which input path
  `HeadLook.Step` honours that frame, or judge the device-inferred behaviour as the better port and
  record why. Build beside `BL-399`, it is the same byte's third state.
  *Cross-refs:* `BL-399` (padlock, the byte's third state), `PLAN-cockpit-view` C21 (`HeadLook`,
  `src/Flight/HeadLook.cs`).

- `BL-436` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The cockpit view's whole feel is unjudged at the controls,
  one sitting owes seven separate decisions `PLAN-cockpit-view` made without one.** (a)
  `PlaneBuilder.InteriorScale` (B11) is a declared TUNE, framed static (0.04, "the panel ~0.7 m
  ahead of the eye") and never flown. (b) Head-look feel (C21): the snap directions, the 2 rad/s
  free-look pan rate, and the elevation/azimuth smoothing rates (3.0/5.0 per second) are all
  decoded constants, none flown. (c) The azimuth-sign port decision (C21): the original's two input
  paths disagree on sign and CSVM picked one convention for both (positive = left), confirm it
  reads right rather than backwards. (d) The autohead sub-cap port decision (C22): only the
  plane-local X/Y velocity drives the lean, the forward (Z) component dropped before scaling, a
  port choice made without decoded evidence either way. (e) The damaged-over-cockpit engine-sound
  precedence (D31): when both the damage swap and the cockpit swap are live, damaged wins, an
  evidence-gapped port decision, no shipped def authors the conflicting case. (f) Wobble in first
  person against the original: the cockpit camera inherits the plane node's wobble by riding the
  drawn pose (A2, `docs/org/shakes.md`), compare amplitude and character in the cockpit at the
  controls against the original's own cockpit view. (g) The Nose head-look confirm:
  `PLAN-cockpit-view`'s ⚠ table row 2 retired `docs/org/cameraViews.md`'s old "head fixed in
  Nose" reading in favour of "head-look runs, only autohead is gated", fly Nose and confirm the
  free-look is really there, since the retired reading may have been a live impression rather than
  a misread decompile.
  *Fix shape:* one cockpit sitting across a couple of airframes covers all seven; each is a
  judgement call, not a re-decode.
  *Cross-refs:* `PLAN-cockpit-view` (every decision above, by wave: B11, C21, C22, D31), `BL-391`
  (engine level, kept separate from (f)).

- `BL-885` `[Fidelity]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` **The chase camera's base elevation is authored
  (`thirdp_pitch`), where CSVM holds a hand-picked 15.7°.** The chase placement builds its direction
  from the head's shown azimuth and the head's shown elevation PLUS the camparam block's `+0x28`:
  `FUN_0042c7f0` loads `DAT_0064ef58` at `0042c881`, adds `[ECX + 0x28]` at `0042c88d` and hands the
  pair to the direction builder `FUN_0053f550` at `0042c8a2`. The reader `FUN_0042f700` puts
  `thirdp_pitch` there in radians (`0042f81d`-`0042f83b`, the authored degrees × π/180). Shipped that
  is **0.29°** on every airframe and **0.2°** on Balmoral's block, i.e. the original's settled chase
  camera sits essentially dead astern, level with the aeroplane. CSVM instead normalises a
  hand-picked `BaseUp` 4.5 / `BaseBack` 16 pair, which is **15.7°** above the tail, and
  `CameraController`'s own comment calls the direction hand-picked because
  `docs/formats/camparam.md` had read 0.29° as too small to be an elevation. It is the elevation.
  *Fix shape:* take the base elevation off `CamParams` (`thirdp_pitch`, already parsed) and feed it
  as the resting elevation the head's own angle adds to, so `ChaseSwing` keeps working unchanged;
  `BaseUp`/`BaseBack` then reduce to "behind the tail" plus that angle. `thirdp_height`'s units are
  still unknown, so the radius stays where it is (`dist` + `dist_factor`).
  ⚠ **This moves every pinned golden shot with a chase camera**, so it is a re-pin, and the new pose
  drops the framing of every flight capture by 15°. Judge it at the controls before re-pinning: a
  camera level with the aeroplane sees less ground and more sky, and the original's own footage is
  the reference. ⚠ **Do not read the 15.7° as wrong-by-construction**: it was picked to look right
  and has never been judged against the authored figure side by side.
  *Cross-refs:* `BL-150` (its measured "level" rows are level only up to this angle),
  `docs/formats/camparam.md` (`thirdp_pitch`, and the Known limits paragraph this corrects),
  `docs/org/cameraViews.md` (head-look controller, the chase placement's own elevation).
- `BL-906` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **The spyglass picture
  jitters and shows the pilot's own aircraft; the original's disc shows neither.** *Evidence:*
  `SpyglassView.Aim` copies the pane camera's cull mask verbatim, so nothing keeps the own mesh
  out of the disc; the user knows from the original that parts of the player's plane are not
  drawn in it. The jitter: `TargetHud.StepSpyglass` runs in `_Process`, but the eye it hands
  `Spyglass.Pose` is the raw last physics pose (`FlightController.BuildHudState` and `Attitude`
  read `_model`), while the chase camera and the plane's visual follow the interpolated
  `_renderPose`; the target point is already interpolated through `TryRenderPosition`, so the disc
  frames a smooth target from a tick-quantised eye, the same shake `TryRenderPosition` was written
  to remove from markers. *Fix shape:* feed the interpolated render pose (origin and basis) into
  the spyglass eye; give the pilot's own aircraft mesh a render layer the spyglass camera masks
  out (per pane in splitscreen, the layer band `SplitScreen.PlayerCullMask` reserves is the
  place). *⚠ Traps:* `docs/org/spyglass.md` says the camera stands at the plane's own position
  and does not mention own-mesh culling either way; the exclusion rests on the user's recall, say
  so in the code. `c1-stunt-marker` pins the disc's layout and should not move. *Playtest after
  fix:* Instant Action, select a target and hold a turn: the disc holds steady and no wing or tail
  of the own plane crosses it. *Cross-refs:* `CSVM/src/Flight/SpyglassView.cs`,
  `CSVM/src/Flight/TargetHud.cs`, `FlightController.TryRenderPosition`, `docs/org/spyglass.md`.
- `BL-915` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Mouse look snaps back
  to centre the moment the mouse stops moving, with the right button still held.** *Evidence:*
  `FlightController.MouseLookDelta` returns a direction only on frames where the cursor moved, and
  `HeadLook.Step` reads "no free-look input" as the idle case and chases the centre, so a held but
  stationary mouse is indistinguishable from a released button. Under mouse flying the right
  button is a toggle rather than a hold (`StepFreeLookFlag`), which is the other half of the
  complaint. *Fix shape:* the head-look input carries a "looking" flag set while the right button
  is held, independent of this frame's motion; while it is set and the mouse is still the head
  holds its pose, and the return to centre starts on release. The right button becomes hold-to-look
  under both mouse schemes. *⚠ Traps:* the pad's aim stick and the centre key keep their arms
  above the free-look arm in `HeadLook.Step`. *Playtest after fix:* hold the right button, look
  over a shoulder, stop moving the mouse: the view stays; release: it returns. *Cross-refs:*
  `CSVM/src/Flight/HeadLook.cs`, `BL-447`'s closing commit, `BL-914`, `BL-916`.


## HUD & UI

- `BL-113` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Compass tape**, `TileOverscan` / `RimGain` / the nearest-tick look remain TUNE
  (north = −Z is now confirmed against the original, 2026-07-30, do not reopen).
  *Cross-refs:* `PT-121` (the flight that judges the three).

- `BL-181` `[Tuning]` `[Blocked: a shared type scale]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **Marker HUD + scoreboard layout is a provisional pass, not a
  fidelity sign-off.** Playtested 2026-07-30
  (`./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`): the stunt run HUD and scoreboard placement,
  fonts and distance units "work for now." The verdict is explicitly contingent: it says these read
  acceptably in isolation, and a fidelity sign-off needs them read against the chrome the rest of the
  game's UI uses, which does not exist yet. ⚠ **The composed campaign boards do not discharge this,
  and should not be read as doing so.** They are painted original artwork positioned at authored
  pixels with a per-background ink palette (`BoardPalette`), so they carry no type scale, no distance
  units and no shared font choice for an in-flight overlay to match. What this waits on is a UI
  surface that defines those three things for chrome the original did not paint, which is what the
  menu-hub milestone was standing in for. Blocked on that surface, not on data.
  *Fix shape:* re-review `StuntRunHud.cs`/`TargetHud.cs`/`StuntScoreboard.cs` placement once such a type scale exists,
  against it rather than in isolation. *Cross-refs:* `BL-449`, whose landing prompted this wording.

- `BL-431` `[Feature]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The screen-space `GaugeCluster` doubles up over the driven 3D panel in
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
  *Cross-refs:* `PLAN-cockpit-view` B11 (parked the states; also settles that the `gauges` child
  itself must stay visible, it is not a needle overlay). The windshield bullet-hole decals
  (`bullet1`-`bullet5`) share the same parked-state mechanism but are driven by the unrelated
  `cockpit_bulletholes` anim-def family (`PLAN-m3-polish-5` line 453), not this item.
  Neither that family nor either other `PLAYER_1ST_PERSON` def (`muzzle_burst`, `player-1`'s
  `pdpanel4`/`pdpanel6`) targets any node inside `gauges`, and no runtime binds a plane's own
  subtree apart from the crash rig's narrow subset, so nothing animates the panel per frame.

- `BL-751` `[Feature]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Original's seat
  strip is a monochrome device list on the left where Built-in's is a colourised P1 to P4 chip row
  on the right.** *Evidence:* reported at the controls over `PLAN-M5-polish-12`'s closing sortie,
  "the strip showing the players should be the same as in Built-in, colorized P1 P2 P3 P4 on the
  right side". `CampaignSeatPanel` (`CSVM/src/UI/Menu/Original/OriginalSeats.cs:165-187`) draws
  `P<n>  <device>` rows at the top left over a scrim, focused row in `BoardInk.RowFocused` and the
  rest in `BoardInk.Detail`; Built-in's chip strip draws `SplitScreen.PlayerTag(i)` in
  `SplitScreen.PlayerColor(i)` at the top right (`CSVM/src/UI/LaunchMenu.cs:272`, `:2860-2885`).
  *Fix shape:* Original's strip takes the same tags, the same per-player colours and the same
  top-right corner. *⚠ Traps:* the strip's left-top band was chosen because no campaign board
  puts a plaque there (`OriginalSeats.cs:49-55`); moving it right has to clear the book tab at
  x 558 and the Instant Action screen's own paper. The `onPaper` light-scrim case exists because
  the Instant Action palette swallows the dark one, so keep a ground behind coloured text there.
  *Cross-refs:* `BL-703`'s landing (`git log --grep=BL-703`), which added the strip.

- `BL-755` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The ammo screen's two
  description panes trade the cursor's words instead of each holding its own subject.** *Evidence:*
  reported at the controls over `PLAN-M5-polish-13`'s closing sortie, "the description can be
  displayed on both parts. Currently it switches depending on what is selected". Both authored panes
  are filled on every repaint (`CSVM/src/UI/CampaignBoards.cs:343-359`), but the half the cursor is
  not in falls back to the first armed gun group or the first fitted pylon (`CampaignAmmoPage`
  `DetailRow`, `CSVM/src/UI/CampaignAmmoPage.cs:314-327`), so moving the cursor between the halves
  moves which pane answers it and leaves the other on a default that is empty when nothing is armed
  or fitted. *Fix shape:* settle what the idle pane holds against
  `OriginalScreenshots/Campaign Ammo Selection.png`, which carries Slugs over High-Explosive Rockets
  with the pointer in neither half, then make the rule match. *⚠ Traps:* the two panes are authored
  and their y is pinned four pixels over the shipped row, so do not move them while changing what
  they say. An empty pane is the fallback finding nothing armed, not a missing pane.
  *Cross-refs:* `BL-658`'s landing (`git log --grep=BL-658`), which filled the lower pane.

- `BL-765` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The inventory writes
  the plane's name and its airframe as one line at the airframe's row, leaving the name's own row
  unused.** *Evidence:* reported at the controls over `PLAN-M5-polish-13`'s closing sortie, "The
  Plane name is not aligned correctly". The section authors two text rows a couple of pixels apart,
  `HA_T_PLANE` at 138,108 with no width and `HA_T_PILOTPLANE` at 236,110 across 400
  (`extracted/rof/ASSETS/LAYOUT.CSV`, `[@Hangar@]`); ours concatenates both texts into
  `HA_T_PILOTPLANE` with three spaces between them
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1612`) and never draws `HA_T_PLANE`, whose key
  appears nowhere in the tree. *Fix shape:* one text per authored row. *⚠ Traps:* **which row takes
  which text is not settled by the keys' names**, and there is no capture of the INVENTORY screen
  under `OriginalScreenshots/`, so the first step is a shot of it; `HA_T_PLANE` is the left row and
  the one with no width, which is the shape of a short label rather than a name. *Cross-refs:*
  `BL-764`'s landing (`git log --grep=BL-764`), which settled the same screen's buttons.

- `BL-780` `[Feature]` `[S]` `[Next: decide]` `[Impact: low]`
  `[Evidence: decoded]` **The About box's product identification number reads `???`, because
  CSVM does not read the registry value the original reads.**
  *Evidence:* `uiData` 2108's handler calls `FUN_004073d0(HKEY_LOCAL_MACHINE, "PID", "???")`, which
  opens `SOFTWARE\Microsoft\Microsoft Games\Crimson Skies\1.0`, queries `PID` into a 512-byte static
  buffer at `0x006464cc` and returns it, copying the default in and returning that when either the
  open or the query fails. langui 1301's one `%1!s!` placeholder takes that string.
  *Fix shape:* read the same key and value, falling back to `???` exactly as the original does.
  *⚠ Traps:* this is a dependency decision before it is a code change. `CSVM/CSVM.csproj` targets
  plain `net8.0` rather than `net8.0-windows`, so `Microsoft.Win32.Registry` is a package reference
  the project does not carry today and the call needs a platform guard the analyzers will ask for;
  nothing else in `CSVM/src` reads the registry. Weigh that against what it buys: a player who
  installed the retail game sees their own id and everyone else sees `???`, and both readings are
  faithful. Do not source the id from anywhere else, since it is the installer's and not the game
  files'.
  *Cross-refs:* `git log --grep=BL-779` (the box this text stands in, and what it draws today),
  `docs/org/menu-inventory.md`.

- `BL-784` `[Feature]` `[L]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The Game Options page drops the original's own Default View and
  Auto Head Turn rows, and neither presentation offers either setting.** *Evidence:* the section
  authors three option rows, Difficulty (`GO_D_DIFFICULTY`), Default View (`GO_T_VIEWTITLE` and
  `GO_T_VIEWDESC` over the `GO_D_VIEW` dropdown) and Auto Head Turn (`GO_T_HEADTITLE` and
  `GO_T_HEADDESC` over the `GO_B_HEADTURN` checkbox). The port's table carries Difficulty and the
  remake-only Menu row alone, while `ReadGameOptionsPage` already reads both dropped rows' widgets
  for the page's row shape (`CSVM/src/UI/Menu/Original/OriginalOptionsScreen.cs`), so the geometry is
  present and the options are not. Both settings exist in the engine with no way to them: autohead
  runs behind the `headLook.autohead` config key, default off
  (`CSVM/src/Utils/Config.cs:224-227`, `CSVM/src/Flight/FlightController.cs:3796-3806`), and the
  opening view is `PilotViewMode.Chase` seeded only by `--view=`
  (`CSVM/src/Flight/CameraController.cs:162-185`). Built-in's Options screen shows neither.
  *Fix shape:* two entries in the `GameOptions` table with the store fields behind them, the same two
  rows on Built-in's Options screen in its stepper convention, and each setting read where it is
  decided, the autohead gate and the flight's opening view.
  *⚠ Traps:* (a) **The plate holds three rows and the port already spends two.** A fourth row at the
  authored 62-pixel pitch from the first row's Y 283 reaches the plaque row at Y 457, so this needs a
  decision before it needs code: a taller plate (one image, so a stretched or tiled drawing is a
  remake reading to record), paged rows, or a different home for the remake-only Menu row
  (`docs/org/menu-inventory.md`'s GameOptions row). (b) **The Default View dropdown's words are not
  decoded.** `docs/org/cameraViews.md` has the options menu labelling camera positions "external"
  (`MSG_OPT_3RD_PERSON`), "cockpit" (`MSG_OPT_COCKPIT`) and "default view" (`MSG_OPT_DEF_VIEW`),
  which is a lead and not `GO_D_VIEW`'s item list, and this port's own views are Chase, Cockpit and
  Nose. (c) A row does not settle autohead's port decision: its default is off because the original's
  cockpit footage reads that way, and the sub-cap choice behind it is still unjudged at the controls
  under `BL-436`(d).
  *Cross-refs:* `BL-436` (the cockpit sitting that judges autohead), `BL-782` (the same
  both-presentations gap for the audio levels), `git log --grep=BL-783` (the display settings' half,
  closed with four rows on Built-in's screen), `docs/org/menu-inventory.md`,
  `docs/org/cameraViews.md`.

- `BL-809` `[Bug]` `[S]` `[Next: data]` `[Impact: high]` `[Evidence: footage]` **An opened scrap
  reads differently from the original's: the typeface differs, and where ours shows the scrap's
  picture the original shows a newspaper-header-like image and then the text.** *Evidence:* reported
  at the controls as "Scrapbook Texts look different in the original", detailed as "Typeface, the
  image not showing but another one like the header of a newspaper and then the text". `CAP-52.mkv`
  t=46 to 88.8 opens six scraps full page (`playtest/CAP-52/scrapclick.png` holds one), which is the
  A/B to read them against. Ours composes the zoom family's background, the scrap's inset image and
  up to three text lines at the family's boxes, with TUNE font sizes and no font-face decode
  (`CSVM/src/UI/CampaignScrapbookZoomPage.cs:27-31`, `ScrapbookComposition.ZoomFamily`). *Fix
  shape:* still the six opened scraps from CAP-52, put each beside ours at the same scrap, and say
  per family what the original draws where (which image, which text boxes, which face); then align
  the composition and pick the nearest shipped face. *⚠ Traps:* the look is the user's call, so a
  montage goes in front of them before anything is parked on a distance; `SCRAPBOOK.CSV` and the
  scripts are the decode for positions, the film only for the look. *Cross-refs:* `CAP-52`,
  `docs/org/menu-inventory.md`, `docs/formats/menu-layout.md` (`SCRAPBOOK.CSV`).

- `BL-834` `[Fidelity]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The warning-shot shield
  arms on every human-piloted aeroplane, where the original ticks it only for the player vehicle
  while the `Network` key is zero.** *Evidence:* `BL-826`'s decode: the world tick calls the
  accumulator only for the player vehicle and only while `*DAT_0064f750` is zero, so a
  multiplayer session shields nobody; `CSVM/src/Flight/FlightController.cs:3708` (at `7d2e9881`)
  gates on `IsHumanPiloted` alone, so all four splitscreen pilots carry it, and no doc records the
  difference. *Fix shape:* decide whether a splitscreen session shields every human pilot (a
  remake-only rule, then written on the shield's docs page) or none; either is one condition.
  *Cross-refs:* `BL-826`'s closing commit, `docs/verification.md` SRC-7, `BL-389` (the
  splitscreen weapon mix, the same family).
- `BL-838` `[Bug]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The ground shadow's forward
  skew, threefold growth and fade exemption key on any human-piloted aeroplane, so a four-pilot
  launch draws four player-shaped shadows in every pane.** *Evidence:*
  `CSVM/src/Flight/GroundShadowPass.cs:228` (at `7d2e9881`) tests `IsHumanPiloted`; the decode
  (`docs/org/shadows.md`) keys the skew on the one local player. *Fix shape:* decide whether each
  pane's own pilot takes the player shape (per-pane, the decoded intent read per viewer) or only
  seat 0; then key on the pane's viewer, pinned in the `ground-shadow` suite over a two-pane
  session. *Cross-refs:* `docs/org/shadows.md`, `git log --grep=BL-331` (the shadow's landed
  placement and silhouette, and the halves dropped with it), `docs/verification.md` SRC-12.

- `BL-878` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Every control prompt
  names its control in words, where a pad player would read a glyph, and the pause and results boards
  carry no control hint at all.** *Evidence:* both flight prompts compose off the seat's own bindings
  and its active device, the auto-dock offer and the crashed pilot's respawn line
  (`FlightHud.ComposeAutoLandPrompt` and `ComposeRespawnPrompt` over
  `CSVM/src/Bindings/ActiveDevice.cs`), and each fills a `%1` slot with `BindingLabels.Describe`'s
  words, so a pad seat reads "Pad Y" and "Pad Left Stick" rather than the button it is looking at; the
  boards (`PauseBoard`, `OriginalPauseBoard`, the two results boards) name no control anywhere, so a
  pad-only seat guesses at the menu. *Fix shape:* a per-control texture set keyed the way
  `BindingControl` is (kind, index, sign), and a text-with-icon line that draws the glyph in the slot
  the control name fills today, so the composition seam itself does not move; then the boards' own
  hints over that same seam. *⚠ Traps:* the `%1` placeholder is what makes the glyph a drop-in, so do
  not compose a prompt by concatenation. A seat names ONE device at a time
  (`ActiveDevice.PromptBinding`'s `readsKeyboard` gate), so a sheet that puts a key cap and a pad
  button in one line brings back what the device gate removed. The original ships no such art, so the
  set is this port's own and its look is the user's call at the controls, not a luminance distance.
  *Cross-refs:* `CSVM/src/Flight/PromptLine.cs`, `CSVM/src/Bindings/BindingLabels.cs`,
  `docs/controls.md`.
- `BL-907` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Instant Action's
  kill line names the pilot with the last used campaign profile's name, as the original's does;
  CSVM prints "Wingman was shot down" for the player's own death.** *Evidence:* tested in the
  original with two profiles: an Instant Action sortie carries the last used profile's name and
  the line on the player's death reads "<name> was shot down". In CSVM `GameSession` hands
  `HudMessages.PostKill` `_campaign?.PilotName`, null outside a campaign, so the named arm fails
  and the team arm prints the wingman line (`HudMessages.KillLine`, the fall-through
  `HudKillLineSuites` pins). *Fix shape:* an Instant Action session reads the pilot name from the
  profile store's last used profile, the same field the campaign feeds the kill line, and passes
  it to `PostKill`; with no profile on disk the fall-through stays. *⚠ Traps:* the decode's
  "PlayerName is only set by a campaign profile" reading described the setter, not its lifetime;
  the global survives the campaign screen, do not re-litigate it. Keep the suite's unset case.
  *Playtest after fix:* Instant Action after a campaign session, get shot down: the line carries
  the profile's name. *Cross-refs:* `CSVM/src/Flight/HudMessages.cs`,
  `CSVM/src/Session/CampaignDirector.cs` (the name's comment), `docs/org/vehicleDamage.md`.
- `BL-908` `[Feature]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: data]` **The Original UI
  ends an Instant Action on the `IA_WRAPUP` notepad page, not on the in-flight board.** After the
  3 s hold the original leaves the world for the menu shell's wrap-up page: the Air Spicy Tales
  magazine art, a notepad titled with the mission name, the four decoded rows and a Continue
  button (`Z:\CSVM\OriginalScreenshots\Instant Action End Screen Stunt Flight.png` and
  `... End Screen Fail.png`). CSVM draws the four rows as `IaWrapupBoard` over the world in both
  presentations. *Evidence:* `docs/formats/instant-action/wrap-up.md` (the `LAYOUT.CSV` panes and
  the four value texts), the two stills. *Fix shape:* in the Original UI the ending goes from the
  hold to a shell page built from `LAYOUT.CSV`'s `IAWU_*` rows, in the shape of the other Original
  screen modules; it carries the built-in board's extra lines (the complete or failed title, the
  context line and the stunt splits) as further lines on the same pad, and Continue returns to the
  Instant Action screen. The built-in UI keeps its board. *⚠ Traps:* the four values are the
  frozen snapshot `InstantActionRuntime.MissionEnded` takes, never re-read live. *Playtest after
  fix:* Original UI, fly an Instant Action to its end: the notepad page, then Continue back to the
  screen. *Cross-refs:* `CSVM/src/Flight/IaWrapupBoard.cs`,
  `CSVM/src/UI/Menu/Original/OriginalInstantActionScreen.cs`, `BL-892`'s closing commit,
  `BL-911`.
- `BL-912` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **At 4:3 the HUD gauge
  columns sit too far in from the screen edges.** *Evidence:* `HudMetrics.ReadingBox` is the whole
  pane at or under 16:9, and `GaugeCluster` places each dial at a fixed pixel offset from the box
  edges scaled by height only, so a 4:3 frame, narrower at the same height, gives the same offset a
  larger share of the width and the columns read as pulled inward. `OriginalScreenshots` holds no
  4:3 reference (the captures run through dgVoodoo at 16:9). *Fix shape:* at 4:3 place both
  columns at a minimal offset from the left and right borders, no measurement owed; wider panes
  keep their placement. *⚠ Traps:* the 16:9 goldens must not move; add a 4:3 shot if the
  placement gets a golden. *Playtest after fix:* 1024x768, the gauges hug the borders.
  *Cross-refs:* `CSVM/src/Flight/GaugeCluster.cs`, `CSVM/src/Flight/HudMetrics.cs`, `BL-913`.
- `BL-913` `[Feature]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **The resolution
  picker offers one 4:3 size and snaps a hand-written size back to its list.** *Evidence:*
  `ResolutionSetting.Standard` carries 1024x768 as the only 4:3 entry, filtered by what the screen
  holds; `options.json` stores the size as a width-by-height word. *Fix shape:* add 800x600,
  1280x960 and 1600x1200 to the table, and let a size written by hand into `options.json` survive
  as its own entry in the picker (shown where it sorts, kept on save) instead of being replaced by
  the nearest listed one. No custom-size dialog. *⚠ Traps:* the display mode decides what the
  saved size means (`BL-896`'s closing commit); a custom size follows the same rule.
  *Cross-refs:* `CSVM/src/Utils/ResolutionSetting.cs`, `BL-896`'s closing commit, `BL-912`.
- `BL-917` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **The Mouse switch on
  the Original Controls page draws misaligned against its neighbours.** *Evidence:* the row takes
  its position from the mouse widget's own layout entry but its size from the Controller Type box
  (the Original options screen builds it with `ControlsPlayerBox(screen)`'s width and height), so where the
  original's Mouse Sensitivity slot differs in size the row sits off its neighbours. *Fix shape:*
  size the row from its own layout entry. *Cross-refs:*
  `CSVM/src/UI/Menu/Original/OriginalOptionsScreen.cs` (the controls page), `BL-447`'s closing commit, `BL-914`.
- `BL-919` `[Fidelity]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: feel]` **The Ammo
  Selection screen offers a rocket row for a pylon the build never bought; the original hides
  it.** *Evidence:* the rows come from the airframe's stock fit, and since `BL-841` a pick on an
  unbought pylon's row is dropped silently rather than the row greyed or hidden. The user recalls
  the original hiding such a row; not decoded. *Fix shape:* decode what the original's screen does
  with an unbought pylon's row (`docs/formats/campaign-screens.md`'s ammo screen, the row's
  visibility against the build's fit), expected hidden, and do the same. *Cross-refs:* `BL-841`'s
  closing commit, `CSVM/src/UI/Menu/HangarFeature.cs`, `docs/formats/saved-games.md`.


## Splitscreen

Our splitscreen mode (2–4 players) has no counterpart in the original, so every rule it authored
against "the player" or "the camera" needs an explicit splitscreen verdict: generalise it, take a
nearest/union rule, or record it as deliberately single/global. This theme collects that work
(sweep of 2026-08-15). **Route fixes through the two existing seams instead of minting new ones:**
`GameSession.PlayerPositions` (nearest human) for gameplay rules that say "the player", and the
viewer set behind `ProjectilePool.Viewers` / `ScreenSize.NearestFloor` for draw rules that say
"the camera". Sim state stays global, the mission wind is the worked example
(`Session/WeatherRig.Tick`, stepped once per frame outside the per-rig loop on purpose). Splitscreen-scoped items that live with
their own system: `BL-537` (the 4-player pool judgement), `BL-296` (per-player ActionMap), `BL-299`
(MP spawn maps), `BL-301` (Dogfight tuning), `BL-314` (race countdown).

The theme's first batch (`BL-126`, `BL-365`–`BL-376`) landed via
`PLAN-splitscreen-polish` (2026-08-15,
complete, the chrome playtest F52/`BL-126` closed it out). New splitscreen findings mint here as
usual.

- `BL-380` `[Bug]` `[Blocked: per-instance fog shader uniforms]` `[L]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Fog-zone selection stays
  player-1-only in splitscreen: `csky_fog_color`/`_range`/`_alt`/`csky_world_light` are one GLOBAL
  shader uniform set, written from rig 0's camera weather state alone
  (`Session/WeatherRig.cs:459-466`), so a pane on the other side of a fog-zone boundary from P1
  renders P1's fog, not its own.** Split out of the `BL-338` residual sweep 2026-08-15 (plan B14):
  the whiteout overlay and the deck regime are already per-rig (the same `WeatherRig.Tick` loop),
  only the fog GLOBALS lag behind, because `ApplyFogGlobals` writes session-wide shader uniforms,
  never per-instance ones.
  *Evidence:* `WeatherRig.cs:459`'s own comment: "Driven by rig 0, because the fog parameters this
  writes are GLOBAL shader uniforms, one set for the whole session ... In splitscreen with one
  player under the deck and one over it, both panes therefore wear player 1's fog." Pre-existing
  (`SetupWeather` always wrote one global set before splitscreen existed), not introduced by it.
  *Fix shape:* per-instance fog uniforms on every fogged mesh instance, selected by whichever
  pane's camera the instance should answer to, a shader-architecture change (per-instance state
  keyed off the viewer set), not a wiring change.
  *⚠ Traps:* a second `RenderingServer.GlobalShaderParameterSet` call does not fix this, that is
  still one value for the whole process, not one per viewport. Any new `instance uniform` this adds
  to `shaders/csky_instance_uniforms.gdshaderinc` must be APPENDED, never inserted, Godot assigns
  instance-uniform slots by declaration order per shader, and the file's own header names the
  2026-07-17 `csky_fog_on` index-collision bug this ordering contract exists to prevent.

- `BL-389` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Splitscreen weapon mix needs a retune: rockets too quiet, guns too loud,
  especially four guns firing at once.** Found at the `BL-126` chrome playtest (F52,
  2026-08-15), `FlightAudio.MixGain`/`Projectile.cs`'s pool `MixGain` (the `1/sqrt(N)` equal-power
  attenuation D31/D32 landed) reads right in isolation but the per-weapon balance under it does
  not: a 4-player Dogfight with simultaneous gunfire is too loud relative to rocket explosions,
  which read as too quiet against it. *Look for:* rocket vs. gun relative level across 2P/4P,
  specifically four guns firing together. *Fix shape:* a judgement call at the controls on the
  per-def volume terms feeding `Projectile.cs`'s `def.Volume * 0.2f * MixGain * distanceGain`
  (line ~2238), not the `1/sqrt(N)` splitscreen term itself, which is confirmed correct.

- `BL-434` `[Research]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: trace]` **Splitscreen cockpit interior/audio behaviour is unprofiled and unjudged
  past one pilot.** `PLAN-cockpit-view` (Decision 5) built cockpit rendering and the
  `cockpit_engine_sound` swap verified single-player-only, no further. (a) **Per-viewport interior
  draw cost is now profiled, not yet judged**: `analysis/campaign-coop-4p-perf/FINDINGS.md` (D33)
  measured CM18 (C4/M03) at 1P/4P x external/cockpit: cockpit view adds ~5% more draws at both
  player counts (697 to 733 at 1P, 3972 to 4169 at 4P) and ~0.4-2 ms of frame time, both within or
  just past this machine's measured noise floor (`docs/verification.md` PERF-9…PERF-11); going 1P
  to 4P moves draws 5.7x (697 to 3972, more than the 4x pane count) while `render_cpu_ms`/`gpu_ms`
  stay flat, so the draw-count growth outpaces panes and has not yet been isolated to the cockpit
  subtree specifically vs. the rest of the per-pane rig. (b) **The per-pilot `cockpit_engine_sound`
  swap against splitscreen's `MixGain` term is unjudged at the controls**, `BL-391`'s "own-ship
  engine loop too loud" finding predates the cockpit swap and never isolated the `_cp` def
  specifically. (c) **Today's hiding mechanism is node visibility on a shared plane node, not a
  per-viewport render flag**: `CockpitVisibility` hides the OWN rig's `healthy` body node, so a
  pilot sitting in the cockpit hides THAT AIRCRAFT'S body in every pane that can see it, not just
  their own, a cross-pane effect unjudged at `N > 1`.
  *Fix shape:* isolate the draw-count growth's split between the cockpit subtree and the rest of a
  4P rig; a splitscreen listen for the cockpit-swap/`MixGain` interaction; confirm or fix the
  cross-pane body-hide visually at the controls with 2+ cockpit-view pilots in the same session.
  *Cross-refs:* `PLAN-cockpit-view` B11 ("Splitscreen posture"), `BL-391` (base engine level,
  the audio half of (b)), `BL-389` (splitscreen weapon mix, same playtest family).

## Missions, modes & campaign

- `BL-469` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: data]` **An escort cannot hold station on a leader using nitro, and nothing measures
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

- `BL-314` `[Feature]` `[Blocked: PT-45]` `[L]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **Race countdown, a rolling start on rails before the run clock
  opens.** The abreast starting grid landed 2026-08-08 (`StartGrid`), so every pilot in a splitscreen
  stunt race now begins on one line, on one heading, at one altitude. What is still missing is the
  moment a race starts: today the clock is running the instant the world appears, so whoever's
  loading screen ends first is flying first. Deliberately split off from the grid rather than landing
  with it, because it changes a **deliberate** clock rule and that rule deserves its own scrutiny.
  Do not start it before `PT-45` has judged the grid at the controls, a countdown over a grid nobody
  has flown tunes the presentation of an unvalidated start.

  **The shape, as decided.** (a) A **rolling start**, not a full freeze: the aircraft stay
  physics-alive and moving through the count, which reads as a race start rather than four parked
  planes popping into motion, and is exactly as fair as a freeze since nobody may manoeuvre. (b) The
  countdown flight is **on rails**, a kinematic level walk of the field, driven straight into
  `_model.Reset(...)` (`FlightController.cs:649` is the existing call shape:
  `_model.Reset(pos, attitude, SpawnSpeed, throttle)`), arranged so that **GO is exactly today's
  spawn state**. Nothing is simulated during the count, so there is no sink to fight, no per-plane
  divergence, and the handoff is the pose the flight model already starts from. (c) `--det` never
  sees any of this: like the grid, the countdown is reached only through the race path, so a scripted
  run must remain byte-identical and the whole feature stays hand-flown verification only.

  **⚠ Traps, read before touching this.**

  1. **Do not derive the pre-GO setback from a speed.** Spawn speed is the mission's own
     (`PLAYER_INIT[4] × 0.1`, 18 m/s in nearly every mission), resolved per session by
     `SpawnPicker.StartState` and carried on `FlightStart`
     ([`docs/formats/spawns.md`](docs/formats/spawns.md)). It is data, so it differs
     between missions and can differ again whenever a mission is re-read; any "start N seconds back
     at the spawn speed" arithmetic therefore hard-codes one map's number into a rule meant to hold
     on all of them. The on-rails walk above avoids this by construction: it simulates nothing and
     it ends on the spawn pose whatever the speed is.
  2. **This changes `StuntMission.Elapsed`'s documented rule.** "The clock never stops" is stated
     twice and on purpose (`StuntMission.cs:109-113` on the property, `:247-249` on `Tick`), it is
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
  race has a defined start (`StuntRace.cs`, `ScoreStore.GetBest`/`RecordIfBest`), and would want
  their own key namespace, since a countdown makes race and solo totals diverge again.

- `BL-299` `[Research]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` **Decode `net.zrd.json` as the multiplayer spawn table → the retail MP1–MP3 maps for
  Dogfight.** 45 files, one flat group each, node counts quantised by mission type (MP1→80,
  MP2/MP3→48, campaign→8), 23 distinct payloads shared across files, shape and distribution say
  *spawn table*, not patrol route (`PLAN-M4-ai` survey; its "do not build patrol on it"
  warning stands). Now there is a consumer to validate a decode against: Dogfight (`--vs`) plays
  the IA1 `dogfight_ace` list today; a confirmed spawn decode gives it the maps the original
  authored for exactly this mode. MP worlds already load (`--mission=MP1`); only their spawns fall
  back today (`SpawnPicker` warns).

- `BL-301` `[Tuning]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Dogfight (VS mode) tuning**, every deliberate v1 deferral, to be re-judged from
  `PT-43` evidence, not speculation. **Aim-assist strength settled 2026-08-13** from `PT-43`(a)/(b):
  the shipped `sticky_bullet_*` constants (decoded in
  [`docs/org/aim-assist.md`](docs/org/aim-assist.md), built by `BL-342`) read right at the
  controls, damage balance plane-vs-plane felt good and guns are now a practical kill weapon
  without rockets, a marked improvement over firing with no assist at all. No retune.
  **Spawn camping is settled**, which `PT-43`(c) had confirmed a real problem: spawn rotation is
  in, so the opening spawn is still the scenario list walk (one seat per point) and a downed seat
  then comes back on a point drawn between the roomiest entries against the living field, never
  the one it was downed at and never one a living seat holds, with the killer weighed heaviest
  (`CSVM/src/Flight/VersusSpawnRotation.cs`, fed the live field by `GameSession` through
  `FlightController.RespawnPlacement`). Still open: suicide penalty and last-damager credit (0 /
  none in v1), sudden-death overtime on a drawn time-out (draw declared in v1; `PT-43`(f) found
  draw frequency fine at the 5-kills/5-min defaults, so this stays low priority), menu-side match
  options (kill target and time limit are CLI-only), `dogfight_ace` vs `zeppelin_run` spawn
  spacing, the self-blast exemption (own rockets can't hurt you, the guns invariant applied
  consistently, not a balance call), VS HUD line/arrow sizing at 4-player panes (`PT-43`(d):
  confirmed readable and correctly edge-flipping at both 2 and 4 players, `BL-126` chrome playtest
  2026-08-15, no retune owed; the general HUD text-scale config covers the separate font-size preference). Related,
  not absorbed: `BL-126` (splitscreen chrome, closed 2026-08-15). ⚠ The stunt race's
  abreast starting grid deliberately does **not** touch `--vs`, it is selected only when a race
  exists, so Dogfight walks the scattered `dogfight_ace` list and the rotation keeps it that way.
  Copying the grid over is the wrong reflex: four dogfighters 60 m apart on one heading is an
  instant head-on merge every round.

- `BL-256` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **Stunt screenshot feature, triggered off `DzRadius`, much later, by user decision
  (2026-08-04).** `DzRadius` (15 m, user-hand-tuned) is settled
  as the **marker-centre radius**: scoring crosses the authored `dzpathN` gate pair
  (the archived development log, 2026-08-01 entry "M3 Wave C C9"), and the constant's remaining roles are the `dzN`
  marker centre and, eventually, the trigger for a stunt screenshot feature. No design beyond
  this sentence exists yet, recorded so the constant's purpose and the feature intent survive.
  The screenshot latch should also play `snd_dangerzone_camera` (`dangerzone_camera.wav`, a
  data-orphan SFX named by no `SOUND_GROUPS` entry and no world data; the user confirms it is
  the automatic-screenshot sting, not a zone-cleared cue, formerly `BL-090` item 5, closed).
  ⚠ Do not retune or delete `DzRadius` as dead code, it is reserved, and the 15 m is the user's.

- `BL-789` `[Research]` `[M]` `[Next: decide]` `[Impact: high]` `[Evidence: decoded]` **Which vehicle volume gates AI target admission: CSVM reads the activation radius where the original reads the attack cylinder.**
  *Evidence:* `FlightController.cs:3344` hands `AiModeMachine.ActivationRange` to
  `AiTargetRanking.Score`, which refuses a candidate beyond it (`AiTargetRanking.cs:156`), while the
  original admits candidates on the cylinder `FUN_00421ad0` reads at
  `+0x328`/`+0x32c`/`+0x330`, the fields the roster map assigns to the ATTACK volume; the activation
  triple `+0x318` to `+0x320` is read by the AI state update `FUN_004897c0`. Both volumes ship at
  2,000 m, so the mapping never showed until `BL-565` landed the `DEDG` widening: a watched member
  now admits candidates out to 9,000 m where the original would still admit only inside its attack
  cylinder. Engaging is unchanged, `AiModeMachine.cs:321` gating that on
  `min(ActivationRange, AttackRange)`, so what is at stake is selection and pursuit range, not
  firing range. *Fix shape:* settle which volume each consumer should read, then align the consumer;
  the answer decides whether `BL-565`'s widening should reach acquisition at all. **It also decides
  half of `BL-565`'s own goal:** the pursue ENTRY gate is `min(activation, attack)` and no `DEDG`
  widens the 2,000 m attack radius, so the widening keeps an engaged member engaged and does not make
  one parked 8 km away set off, which is what "a watched group comes to the player from anywhere on
  the map" asked for. Whether the original's own entry gate reads the widened triple is the same
  question about `FUN_004897c0`.
  *⚠ Traps:* the negative half of this decode is the weak half. `FUN_00421ad0` was read as the only
  admission path and `FUN_004897c0` as the only reader of the activation triple; re-read both writers
  and map their offsets before acting, since a second reader would change the answer.
  `docs/org/aiPilot.md` has been corrected on the field identity, but no code moved on it.
  *Cross-refs:* `BL-565`'s closing commit, which found this and left it; `BL-523`, the mode cycle
  this range feeds.
- `BL-523` `[Bug]` `[L]` `[Next: data]` `[Impact: high]` `[Evidence: feel]` **The AI's patrol/pursue/lay-off cycle does not match the original: CM05's
  second patrol never pursues, CM07's friendly flights hold their net while enemies attack them,
  and CM09's enemies fly up to 80 km away.** *Evidence:* three
  symptoms of one mode machine, reported at the controls. In CM07 (C1/M02) friendly aircraft keep
  flying their net instead of engaging enemies that are shooting at them, so the promotion gate is
  wrong on the friendly side too, not only for the enemy patrol below. In CM05 (C3/M04) the second enemy patrol
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
  `AiModeMachine`'s source functions. *⚠ Traps:* `PLAN-M5-polish` line 3450 recorded the
  never-pursues impression for wingmen and it closed on a different cause; check the log rather
  than reusing that answer. Do not add a leash constant; `docs/org/aiPilot.md` records that patrol
  is built on a spawn table, and the return rule has to come from the decode.
  `BL-524`'s half of the fly-away is settled and is not a lay-off question. Neither CM05 nor CM07
  has a netless friendly patrol, and the original has no netless-patrol and no leaderless-wingman
  branch at all: `FUN_0041d1f0` indexes -1 on an unresolved net with no guard, `FUN_0041e760`
  dereferences its leader at `+0x2fc` with no null check, and `FUN_0049c880`, which would release a
  wingman onto the chapter's first net, has no callers. The netless fly-away that remained,
  `AiPilot.FlyPatrol`'s netless arm holding the pair `FlyPursuit` last wrote, is closed on the host
  side: a wingman whose leader leaves play is seated on that leader's own net
  (`CampaignDirector.TakeLostLeadersNets`), so no campaign pilot reaches that arm with a dead
  quarry's bearing. The take-off hand-off is settled (`BL-594`'s closing commit): a launch is released 300 m
  past its last waypoint at 53 m/s, climbing, and lives; the net-nearest snap the original skips
  (`FUN_004b0f40`) stays undecoded.
  Two more sightings of the same cycle, unflown against this tree: CM08 (C1B/M03) "third wave of
  enemy fighters only patrol and don't attack" and CM20 (C4/M05) "enemies were patrolling and not
  pursuing".
  *Cross-refs:* `BL-524`, `docs/org/aiPilot.md`, `BL-522`'s closing commit (`git log --grep=BL-522`).

- `BL-550` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The AI's altitude floor is enforced at one site in CSVM and at three in
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

- `BL-695` `[Bug]` `[M]` `[Next: data]` `[Impact: high]` `[Evidence: data]` `[CM14]` **CM14's cannon-hatch ladder can complete with no Gemini cannon destroyed at
  all.** *Evidence (traced from a sortie log):* on one CM14 run the whole primary ladder completed
  without a single `[anim] damage:` line on any `lbroadNN` node anywhere in the mission window, and
  with the Gemini still alive at the end (`engines 9/14`, never `DESTROYED`); the mission was Won at
  208.7 s through `hooked_to_klondike`. The six `lbroad11..32` guns deploy normally in that window,
  so the nodes exist and are reachable. The completions track the hull's ENGINE count rather than
  its hatches: `objective 12` (authored `COMPLETION_COUNT 3`) completes on the line after
  `geminizep' engines 11/14`, the third engine kill, and `objective 13` (`COMPLETION_COUNT 5`) on
  the line after `engines 9/14`, the fifth. Ten `wep_07` FLAK launches and five engine kills over the
  window, no hatch damage.
  A second logged run shows the ladder working correctly off real hatch kills, so the fault is
  conditional rather than constant, and finding what distinguishes the two runs is the first step.
  *⚠ Traps:* **the evidence above is stale and has to be re-flown before anything is changed.** It
  was taken on a build without `KillCalledDestructible`, and `BL-694`'s landing commit
  (`git log --grep=BL-694`) is where the whole cannon-hatch chain is decoded: a gasbag section owns
  its four bays AND its four engines, so a section demolition moves both counts together and a bay
  and engine correlation is what the authored data produces rather than a defect. Two of this
  entry's own readings are unsafe on any build: an absent `[anim] damage:` line is not evidence a
  bay survived, because a bay killed by another definition's call logs none
  (only `AnimRuntime.DamageAt` writes that line), and a ladder completing with no hatch shot is authored
  behaviour once the hull dies, since `all_gmzep_gasbags` demolishes every section. What is left to
  answer is the run's own shape: the Gemini alive at the end with five engines gone and the ladder
  complete. Do not tighten the objective count; `gemini-gasbag-bays` asserts the authored six.
  *Cross-refs:* `BL-694`'s landing commit, `BL-639`,
  [`docs/formats/mission-entities.md`](docs/formats/mission-entities.md) "A gasbag section owns its
  bays and its engines".

- `BL-699` `[Perf]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Every AI wave spawn hitches at the controls; the launch frame
  is now measured, and the cost still has to come off it.** *Evidence:* the author feels a
  hitch on every wave spawn in every mission, not only CM18's generator launches. The launch frame
  is measured by `ai-wave-launch-hitch` (`CSVM/src/Testing/AiWaveLaunchHitchSuites.cs`), which
  drives C4/M03's `cargozep1` through `FlightRoster.SpawnAi` over the mission's own built world and
  times each sim step on the wall clock `HitchMonitor` reads: median launch frame 65.2 ms at 1P and
  65.1 ms at 4P against that monitor's own 40 ms floor, the cold first launch 110 to 303 ms,
  the worst frame carrying no launch 55 to 112 ms about fifteen steps behind the first launch,
  which is the deferred crash rig's `AnimRuntime.PrewarmEmitters` phase. The suite holds regression
  bars at the measured level (median 130 ms, worst warm launch 190 ms, worst idle frame 160 ms,
  each about 1.4x the worst reading over repeated runs) and names the 40 ms floor in its note as
  the target this entry owes; the bars drop to the floor when the removals below land, and until
  then a suite failing on the cost it exposes would block every later change instead of this one.
  The cost does not grow with the human
  field, so it is all in the arriving aeroplane. Inside the warm launch frame, the model build is
  18 to 29 ms and `FlightController.Bind` 24 to 40 ms, and the bind is `PlaneCollider.Build` almost
  entirely (35.9 ms of a 39.5 ms bind in the sampled launch); props, wing lights, control surfaces
  and damage together stay under 1.5 ms, and the loadout, the turrets, the tree insert and the
  crash-rig arm under 6 ms.
  *Fix shape:* the whole build moves into the loading screen, where there is no frame budget, and
  the launch frame claims a finished aeroplane. `AiFlightAssembler.Assemble` already builds the
  spawn-independent parts first (the painted model, `PlaneCollider.Build`, the prop and surface
  animators) and only then binds them into the controller with the spawn's own stats, loadout and
  marker; the pool holds those parts per roster block, detached from the tree (a hidden subtree
  still has bodies in the space and nodes the anim runtime walks, a detached one costs nothing per
  frame), and the launch frame keeps the bind, the loadout and the tree insert, which measure
  under 6 ms together. The crash rig's `PrewarmEmitters` (194 `ShaderMaterial` and `MultiMesh`
  builds in `Effects/EmitterRenderer.cs`, 55 to 112 ms on the deferred frames) is built at load
  for the same entry. Campaign rosters author their waves, so the pool is sized off the roster;
  Instant Action generates its waves during play, so its pool refills one aeroplane per quiet
  frame, and a refill is where the per-airframe removals still pay: `PlaneCollider.Build` is a
  pure function of the model's triangles, so the hulls and their `ConvexPolygonShape3D`s belong to
  the process keyed by airframe (PERF-22's shape; check `AircraftBody`'s struck shape index to
  part name mapping before sharing a shape resource between two bodies), and the model build
  shares the airframe's immutable parts or splits along its own mesh grain. Building ahead during
  play without those removals only moves the lump onto a quieter frame (PERF-25, PERF-31).
  *Precondition:* the livery is fixed before loading. Today `Assemble` resolves the scheme from
  `_humanCount + index` where `index` is `FlightRoster._spawned` at launch, so the livery a block
  wears depends on the order the player wakes the waves in, and a pool built at load could not
  paint it. The scheme becomes a function of the roster block itself (the mission and the block's
  position, not the running spawn count), which is the author's decision; blocks that launch out
  of roster order then wear a different livery than before, so re-pin the goldens that show AI
  liveries in the same change and say so in the `exercises` field.
  *⚠ Traps:* the aeroplanes a session builds BEFORE its first frame keep their rigs built in
  place, and deferring them moved the goldens that fly AI aircraft; the pool's claim path must
  produce the same tree the in-place build does, or those goldens move again. The spawn index
  still feeds the spawn jitter (`Rng.NewSystemRandom(Rng.Spawn, index, 0)`) at launch and stays
  there: the pool holds models and colliders, never indices, since allocating indices ahead of the
  launch moves `c1-flight-kill`. The loading screen has to be a real yield of several frames, not
  one: ten aeroplanes with their crash rigs are about 1.5 s of build. The sim clock lags wall
  time on physics-bound late missions, so compare a hitch in sim frames under `--det` and read the
  hitch lines before blaming a spawn (`docs/verification.md` PERF-12/13). `--det`'s numbers are
  with nobody at the controls; the author's feel report is the acceptance.
  *Cross-refs:* `BL-434` (the per-viewport splitscreen cost the same pass profiled), `BL-657`
  (CM18's generator launching at the wrong time, which is where the measured case is flown).

- `BL-728` `[Bug]` `[M]` `[Next: data]` `[Impact: high]` `[Evidence: feel]` **AI aircraft rarely or never evade
  under fire.** *Evidence:* reported at the controls as "recheck evasive maneuvers", clarified as
  "AI never or rarely evades". The damage path itself is now decoded end to end and the reaction it
  produces is faithful (`docs/org/aiControlLaw.md` "The evade flag"), so what is left is frequency:
  the evade fires only on a failed steady-hand roll, which a high-rating pilot passes, and the
  reaction is now one roll per episode rather than one per hit, which can only make it rarer.
  *Fix shape:* fly one Instant Action sortie against a low- and a high-rating pilot with the AI
  trace on, count `NotifyDamage` calls against `EvasiveManeuver` entries and against the
  `steady hand test passed` lines, and read the roll's resolved `steady_hand_chance` for the ratings
  the missions author. The decoded damage weighting on the roll is still unmodelled and is the first
  suspect if the count comes out too low.
  *⚠ Traps:* the report is a feel over a whole sitting on campaign missions; do not tune the roll's
  threshold before the count says which way it errs. A pilot with no eligible maneuver in its
  library reacts by setting the flag and flying on, which reads at the controls as no evade at all
  and is the original's own behaviour; count the flag, not the mode.
  *Cross-refs:* `docs/org/aiControlLaw.md`.

- `BL-840` `[Feature]` `[M]` `[Next: decide]` `[Impact: high]` `[Evidence: trace]` **Six of the eight stored
  campaign profiles, `Gab` with 18 missions done among them, are schema version 2 and the
  version-3 store refuses them.** *Evidence:* `BL-675`'s close (`git log --grep=BL-675`): the
  refusal is by `BL-662`'s design and the store now reports the version and path instead of "no
  such profile". *Fix shape:* a decision first, a one-shot v2 to v3 converter (read the v2 shape
  from the store's history) or accepting the loss; the converter is one reader plus the existing
  v3 writer. *⚠ Traps:* not a path fault, and a worktree shares the main checkout's `user://`.
  *Cross-refs:* `BL-662` (the v3 store), `BL-675`'s closing commit.
- `BL-909` `[Bug]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **The frames between
  the load screen and the cutscene or the flight show the world assembling and the chase view;
  the original cuts straight in and fades up from dark.** *Evidence:* `Launcher.RunOwedLaunch`
  tears the load screen down in the tick the build returns and the next rendered frame is the
  world, with no overlay bridging the two; the first frames show the world still building and the
  player plane in the chase view before the cutscene takes over. In the original the load screen
  goes directly into the cutscene's first frame (or the Instant Action flight) with a short fade
  from dark, not black. No fade exists at any session start (the only fades are the campaign's
  mission-end blackout and the cutscene's leaving fade). *Fix shape:* keep an opaque cover up
  until the session's first real frame is ready (the cutscene camera bound, or the plane placed
  and the HUD live), then fade that cover up from dark over about 1 s, in the shape of
  `MissionEndFade`; every session start, cutscene or not. *⚠ Traps:* the cover must not delay the
  `--det` frame count or move the goldens (`--frames=N` counts from the first world frame); gate
  it off under `--det`, and say so. *Playtest after fix:* start a campaign mission and an Instant
  Action: no assembling frame, no chase view before the cutscene, a fade up from dark.
  *Cross-refs:* `CSVM/src/Session/Launcher.cs`, `CSVM/src/UI/MissionEndFade.cs`,
  `CSVM/src/Session/CutsceneController.cs`, `BL-812`'s closing commit (the load screen's own
  motion).
- `BL-911` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **During the 3 s after
  an Instant Action win the stick is live in the original; CSVM holds the controls.** Flown in the
  original: the player keeps flying through the hold. *Evidence:* `BL-892` landed the hold with
  `FlightController.ControlsHeld`, which synthesises a throttle-only input and swallows every
  discrete command, filed as the change's one judgement with the evidence owed as `PT-145`, which
  this verdict settles. *Fix shape:* through the win's hold the stick and throttle read the real
  input; fire, selectors and respawn stay swallowed; a crash inside the hold does not turn the win
  into a loss (the result is frozen at the ending, the wreck falls, the board still reports the
  win). The death path keeps its hold as it is. *⚠ Traps:* the board's Restart row must still
  answer the first press after it appears, no press left over from the hold. *Playtest after
  fix:* Instant Action ace duel with 1 life, win it and roll during the 3 s, then win again and
  fly into the ground during the 3 s: the board reports the win both times. *Cross-refs:*
  `BL-892`'s closing commit, `CSVM/src/Session/InstantActionDirector.cs`,
  `docs/formats/instant-action/wrap-up.md` ("The hold after the ending"), `BL-908`.



## Tooling, platform & docs

- `BL-033` `[Cleanup]` `[Blocked: SDL >= 3.4.4]` `[S]` `[Next: decide]` `[Impact: none]` `[Evidence: data]` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects, the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effects while active: DirectInput-only controllers (non-XInput sticks
  without an SDL HIDAPI driver) are invisible in-game, and, since the var is set by the launch
  scripts and never by the export, a shipped build enumerates DirectInput devices that no dev or
  test run sees. An 8BitDo Ultimate 2 arrives as three joypads there, which is how a device that
  holds a roster position without producing input came to take the seat `AssignPads` fills by
  position; `Pads.LogPads` records the roster so the next one reads off the log rather than being
  inferred. Dropping the var also closes that divergence.

- `BL-843` `[Cleanup]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Three `GD.Print` calls remain under `CSVM/src`, all in the effects files another item is holding open.** *Evidence:* `Effects/Precipitation.cs:227` (the `precipitation:` line, which a headless C4/M03 probe prints as `fall 5,0 m/s ... alpha 0,50` on a German machine), and `Session/WorldEffectsFactory.cs:529` and `:820` (`world-effects runtime:` and `data-crash:`). All three reach stdout and neither the file sink nor the `--log=` filter, so the same probe's sink log is missing them while every other session line is in it. `Utils/Log.cs`'s own four calls are the console tier the rest of the tree logs through and stay. *Fix shape:* the conversion the rest of `CSVM/src` took, `Log.Info` under the file's category (the shipped console threshold, so nothing leaves stdout), one interpolated string per call since `Log` takes a `FormattableString`, and each pre-built piece rechecked for culture. *⚠ Traps:* a unit test that reaches a `GD.Print` kills the xUnit host outright; a pre-formatted summary string keeps its culture (`docs/verification.md` LOG-23). *Cross-refs:* PLAN-code-review-orch A3 (the before/after log diff method).

- `BL-844` `[Cleanup]` `[S]` `[Next: decide]` `[Impact: none]` `[Evidence: trace]` **654 em dashes remain inside string literals under `CSVM/src` and `CSVM.Tests`: log messages, CLI notes and HUD text.** *Evidence:* the repo-wide sweep rewrote comments and docs and skipped literals, since suites match log lines and a HUD string is a display choice (`VersusHud` draws the glyph for a tie). The writing rule speaks of prose; whether a log message is prose is the decision. *Fix shape:* if yes, a second pass over literals only, with every suite that matches a rewritten line moved in the same change and the goldens re-pinned where a HUD string changes; if no, record the exemption on the writing rule. *Cross-refs:* PLAN-code-review-orch A7.

- `BL-848` `[Cleanup]` `[L]` `[Next: decide]` `[Impact: none]` `[Evidence: trace]` **About 140 dated clauses remain in `backlog.md` entries that predate the no-dates rule.** *Evidence:* `Select-String '\d{4}-\d\d-\d\d'` over the file; every one is event narration ("landed 2026-08-05", "measured 2026-08-04 from the CAP-07 re-take") of the kind the writing rule sends to the closing commit's message. *Fix shape:* decide whether the old entries are swept (each date dropped, the standing fact kept, the evidence findable through `git log --grep`) or grandfathered until the entry closes. A sweep is mechanical but every clause needs a reading. *Cross-refs:* PLAN-code-review-orch A5 (the one dated clause the review found).

## Misc

- `BL-077` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: data]` **Visual prop spin-up/down** (`startprops`/`stopprops` disc crossfade), spawning mid-air
  already turning is by design; becomes relevant with a landing/shutdown flow
  (`FlightAudio.OnEngineStop` is already wired for the audio half).

- `BL-284` `[Bug]` `[Blocked: CAP-34]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Wing-light flare: soft round glow vs the original's sharp star burst; view-dependence
  unproven.** Follow-up from `BL-119` (landed 2026-08-05): with the authored one-sided quad restored
  and the blink at the measured ~1 frame, the flare reads as a compact soft amber glow, much closer
  than the old billboard blob, but the PT-03 reference still shows sharp radiating star points that
  our plain radial `oil_liteflare` sprite does not produce. Whether the original draws the flare
  from every angle (a one-sided quad is roughly chase-view-only) is also unmeasured, one orbit
  clip of a lit plane in the original settles both, any player plane works: `vehicle.zrd.json`
  wires `wing_lights_blink` (or `brigand`'s own `wing_lights_brigand`) into every player craft's
  `start_anims` except the Bloodhawk, which has neither the anim nor flare nodes. (Earlier notes
  here said only piratefighter/brigand carried it, that read `wing_light.zrd.json`'s two
  `ANIMATION_DEFINITION`s alone; `vehicle.zrd.json`'s per-plane `start_anims` is the wider
  wiring and is what the runtime actually plays from, per `WingLights.cs`'s doc comment.) Also riding
  here: `WingLightBlinker.LightEnergy = 1.0` is a declared TUNE, the def authors the point
  lights' range/colour only, no intensity.
  ⚠ Traps: (a) re-adding the billboard is the rejected fix, PT-03's screenshot is against it.
  (b) don't edit or swap the sprite to fake the star: the star points may be the original engine's
  flare *rendering* (a cross-flare pass), not the texture asset, the orbit clip decides first.
