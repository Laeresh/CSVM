# Backlog — unscheduled future work

Everything known-but-not-scheduled, so it survives between polish runs. Which plan is active, if
any, is `PROJECT_CONTEXT.md`'s "Current status" — never restated here. Per-item history/diagnosis
detail is in commit messages (`git log --grep=BL-NNN`; earlier in the archived development log's
dated entries) and `docs/architecture.md` (module bullets); how to
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

    - `BL-NNN` `[Type]` `[Status?]` `[Size]` `[Next: …]` `[Impact: …]` `[Evidence: …]` `[Scope?]` **One-sentence claim — the symptom or goal.** body…

`[Type]` is exactly one of: `[Bug]` (behaviour is wrong vs the original or vs intent),
`[Feature]` (something the engine does not do yet), `[Research]` (the deliverable is an answer,
not code), `[Tuning]` (a hand-tuned constant needing judgement at the controls), `[Cleanup]`
(debt with no player-visible behaviour change), `[Fidelity]` (the mechanism is decoded and ours
differs, with no symptom yet), `[Perf]` (frame time or memory), `[Tooling]` (scripts, hooks and
the verification loop), `[Testing]` (a missing test or test aid). The optional status tag is `[Owed-playtest]`
(the code/constant side is done; what is missing is a human at the controls) or
`[Blocked: <blocker>]` (cannot start regardless of priority — the blocker is named: a capture
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
  ledger"). `FlightScenarios` is 4.

## Damage & destruction

- `BL-787` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: trace]` **A breakup piece that lands after its wreck has settled dispatches no water splash.**
  *Evidence:* on C2B/M04's Gemini, two of the five sections play no `hit_waterN` when they reach the
  sea, the two that land last, and `gemini-breakup-rest` records them resting there all the same.
  `MotionSet.OwesBounce` can only hold a def instance open once a bounce is already recorded, and a
  body records one at its landing, so a body still in the air holds nothing: by the time the last
  sections land, `killgmzep` has gone INVALID and there is no instance left to dispatch their
  branch on. *Fix shape:* let a def instance stay open while any of its bodies is still ballistic,
  or give a landing body its own dispatch path that does not depend on the def instance's life.
  *⚠ Traps:* the pirate zeppelin hides this entirely, because all six of its sections land inside
  the wreck's own descent, so a probe on `piratezep` reads green. Do not chase it in the contact
  solver: the resting height is correct on both hulls, and the missing piece is the dispatch, not
  the landing. *Cross-refs:* `BL-698`/`BL-700`'s closing commit, which found it and left it.
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
- `BL-060` `[Feature]` `[L]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **Improve on the original crash — the bespoke "breaking apart" (branch `bespoke-crash-animation`).**
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

- `BL-121` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Damage (Run-2 item 10)** — breakup scatter, and whether
  the 10c panel-flip and smoke-trail look right in real flight. ⚠ The invented contact constants
  this item used to name are gone: the crash speed, the stop speed, the graze friction and the
  fitted kick are retired against the decoded response and the decoded death rule
  (`git log --grep=BL-271`, `git log --grep=BL-381`), and the graze feel is judged at the controls
  as matching (`git log --grep=BL-120`), so nothing contact-side remains here. What is this item's
  own is the breakup-scatter feel judgement.
  Rendering at real spawns is verified (the `TopLevel` anchor fix, the archived development log,
  2026-08-03 entry, `trail-world-anchor` suite); this item is a magnitude/feel judgement. Tree softness is retired dead code
  (the archived development log, 2026-07-23 entry), not a TUNE — do not re-add it here.
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
  *Cross-refs:* `PT-123` (the flight that judges the scatter).

- `BL-122` `[Tuning]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Data-driven crash (PLAN-data-driven-crash, default since Wave 4)** — several playtest-gated TUNEs,
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
  *Cross-refs:* `PT-124` (the flight that judges it, both surfaces and the sound).

- `BL-297` `[Research]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **Panel-damage semantics: what the original actually
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
  voice line is flavour and this closes. *Cross-refs:*

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

- `BL-066` `[Feature]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` **M3-deferred — ammo pickups.** `MSG_AMMO_PICKUP` / `MSG_AMMO_PICKUPS` strings exist
  (`messages.json` 126–129), implying world pickups that restore ammo. **Carries research
  risk:** the pickup entities have not been located, and they may be mission-scripted rather
  than placed in the world data. Locate them before scheduling.

- `BL-226` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: data]` **The incoming-fire cue set's other two halves are now wireable.** The
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

- `BL-230` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Near-miss trigger distance (B7, 2026-08-02).** `WarningShotCue.PassRadius` = **15 m**,
  the distance a round's swept step must pass within to sound `bullet_warning_sg`. Chosen, not read:
  the shipped `warning_shot_*` block rates the cue but says nothing about how close is close, and the
  sound def's `RANGE [20,200]` is the 3D falloff window, not a trigger radius. Judge it at the
  controls — `--incoming=<metres>` walks a burst past at a chosen distance, and
  `weapons.warningShotRadius` moves the threshold without a rebuild (config.json, so **not** under
  `--det`).
  ⚠ Traps: `CANNON_SPREAD` scatters each round several metres over any real firing range, so the
  achieved distance is a distribution — judge over a burst, never off one pass. Raising it far enough
  that a round crossing the sky sounds is the failure mode, not a louder cue.
  *Cross-refs:* `PT-125` (the flight that judges it).

- `BL-233` `[Feature]` `[Blocked: M4]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **Extend the proximity fuse to zeppelins (and any other M4 flyer) when they get
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

- `BL-286` `[Tuning]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Muzzle-flash residues after the `BL-263` pick (triad kept, 2026-08-05)** — two
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
  *Playtest after fix:* the anchoring, judged in flight rather than in the lab: the flash sits on
  the muzzle at speed and the light travels with the plane over its two frames.

- `BL-289` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Gun-impact looks (A2/`BL-203`, landed 2026-08-01)** —
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
  *Cross-refs:* `PT-128` (the flight that judges the ricochet).

- `BL-357` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The hardpoint selector steps one way only; the original cycles in both
  directions.** *Evidence:* the user at the controls of the original, 2026-08-14: the player selects
  an individual hardpoint (the half that settled `BL-062`, closed the same day), and the selection
  can be stepped clockwise *and* counter-clockwise. Ours has exactly one selector input per weapon,
  `H` / D-pad Left for pylons and `G` / D-pad Right for gun groups
  (`FlightController.cs:1456`, `docs/controls.md`), and `WeaponCursor.NextSelectable`
  (`WeaponCursor.cs:34`) only ever scans forward. **Direct corroboration, `PLAN-targeting` A1
  (2026-08-16):** the original's own Weapons keybind page carries `Cycle guns clockwise` (`F3`) and
  `Cycle guns counterclockwise` (`F4`) as two separate actions, same for rockets (`F5`/`F6`) — the
  original has two selectors per weapon class where we have one, confirmed from the keybind page
  itself rather than from watching a play session. *Fix shape:* a `PrevSelectable` backward scan
  with the same empty-slot skipping, plus a second binding per selector, which is where this stops
  being a two-line change: the flight keymap has no spare paired keys and the pad's D-pad is
  already spent on the two forward steps. The seam is built and the four named actions belong in it:
  `InputAction` gains a reverse member per selector, `DefaultBindings` authors it, and the rebinding
  screen is what finds a player their second pair, so the key space that blocked this is no longer
  the blocker (`CSVM/src/Bindings/`, `docs/controls.md`).
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
  *Cross-refs:* the hangar's custom loadouts (`PLAN-hangar`, landed) are where
  mixed fits make the direction matter, so this item's value went up when that shipped;
  `BL-296` (ActionMap/rebinding seam), `git log --grep=BL-062` for what settled the
  per-hardpoint half.

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

- `BL-696` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The Original presentation has no way into the rebinding screen, so the
  keymap is editable in Built-in alone.** *Decided:* the door is built, and it is a fidelity fix
  rather than a divergence. The author settled it at the controls over `PLAN-M5-polish-13`'s closing
  sortie: "The original has a binding screen in the Prefences: 'Controls' with multiple sub screens.
  We even have a greyed out button in our Prefences screen". *Evidence (traced):* `PF_B_CONTROLS` draws disabled
  (`CSVM/src/UI/Menu/Original/OriginalShell.cs:178`), and the two pages behind it are decoded and
  unbuilt: `ControlsPrefs` (12 rows, layout only) and `Keys` behind `CP_B_KEYS` (17 rows, the
  composition of all seven category tabs, with `KB_B_ACCEPTCHANGES` / `KB_B_CANCELCHANGES` returning
  to `ControlsPrefs`) — `docs/org/menu-inventory.md`:392-394, :482-483. Both were put out of scope of
  the menu plan because no shared option stood behind them; `ControlsFeature` is now that option, so
  the reason has expired. The feature, the capture, the staged Accept/Cancel, the steal rule and the
  persistence are all shared and built, so what is missing is the presentation's screen graph alone.
  *Fix shape:* the two pages over the extracted artwork, the seven tabs, the two control columns, the
  three buttons, and `PF_B_CONTROLS` wired live.
  *⚠ Traps:* **The seven tabs are the original's action groups, not this port's three input
  contexts.** Movement, Throttle, Weapons, Targeting, Views 1, Views 2 and Other have no home for the
  menu and free-camera actions, which the original does not bind at all, so a faithful tab strip
  strands two contexts and needs a decision rather than a mapping. **Control A and Control B are
  positions in a four-slot row, not two fields** (`docs/org/input.md`), and this port holds an
  unbounded list: Built-in shows four and appends "+N more", while two authored columns would hide
  bindings, which is the trap `BL-398`'s screen was written against.
  *Cross-refs:* `BL-296`, `BL-398`, `docs/org/menu-inventory.md`, `docs/org/input.md`,
  `docs/menu-presentations.md`.

- `BL-399` `[Feature]` `[L]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **Track Target's camera behaviour — `L` is reserved, the camera itself is
  undecided.** *Evidence:* the player-targeting plan's out-of-scope call (b), 2026-08-15: the
  original's `Views 1 → Track Target` binds `L` (free in our flight keymap; our `L` is the
  viewer-only livery lab), decoded in `docs/org/targeting.md`, but "keep the target framed" hides a
  pile of camera decisions that plan deliberately deferred: snap vs smooth follow, override vs
  blend with the chase camera, behaviour with no target selected or a target behind the pilot, and
  interaction with the right-stick free look (`BL-372`). `L` is reserved in `docs/controls.md` but
  bound to nothing. `PLAN-cockpit-view`'s head-look decode names the mechanism this camera would
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
  `PLAN-cockpit-view` (`HeadLook`, `src/Flight/HeadLook.cs`).

- `BL-400` `[Feature]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: decoded]` **`Structures` as a selectable Non-Aircraft target — needs a curated
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

- `BL-397` `[Feature]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **A modernized target marker: brackets only PAST range, not under it — the
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

- `BL-413` `[Feature]` `[L]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **Ground-attack ordnance leaves no crater.** *Evidence:*
  six weapons author `CRATER` and the original carves terrain geometry for it; we do nothing. The
  mechanism is decoded in [`docs/org/craters.md`](docs/org/craters.md): every crater in the shipped
  game is the same shape (a 7-vertex rim clipped against the ground, radius 20, floor 6 below the
  impact), it destroys every decoration inside the radius, it is permanent for the mission, and a
  second crater whose footprint comes within 5 units of an existing one is refused outright, so a
  mission accumulates a bounded scatter of non-overlapping bowls rather than a growing mesh.
  ⚠ *Trap:* this is a **terrain and renderer** change triggered by ordnance, not an ordnance change.
  Scope it against the terrain system's constraints (chunking, LOD, the golden manifest's mesh
  counts), not against the weapon table.
  *Cross-refs:* `BL-406` (closed).

- `BL-603` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The human rig sweeps the mesh hull where the original sweeps its def's six
  `collision` probes.** *Evidence:* decoded for `BL-601` (`git log --grep=BL-601`): `FUN_0048d7f0`
  carries the def's `collision` list as rays from the previous pose, six points on the `p*` player
  defs; `FlightController.SweepProbes` does that for an AI rig and keeps the mesh-derived hull sweep
  for the human rig, which is wider than the six points. *Fix shape:* fly a slot the six points
  clear and the hull does not (CM13's dbase arch on dzpath2) in both games; if the original passes,
  sweep the player's probes too. *Cross-refs:* `PlaneStats.CollisionProbes`, `docs/formats/vehicle.md`.

- `BL-717` `[Bug]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: feel]` `[CM02]` **The Pandora's turrets fire
  at the Balmoral the player is about to capture.** *Evidence:* reported at the controls on CM02
  (C3/M05): the player's own zeppelin's turrets shot at the last Balmoral, the bomber the mission
  wants boarded. The three `britbalmoral_*` blocks are enemy-team aircraft in `aiv.zrd.json`, so
  the turret acquisition (aircraft only, `BL-626`'s closing commit) sees them as any other hostile.
  The Pandora's record names `player` alone under `targets`, and that field is the broadside's,
  not the turrets'. *Fix shape:* decode whether the original's turret acquisition skips an
  objective-flagged aircraft, a capture subject, or an aircraft the script has marked for pickup,
  or whether the Pandora fires at Balmorals in the original too and the report is a preference.
  Read the turret target scan the ring uses before adding a rule. *⚠ Traps:* a Balmoral carrying
  the objective flag is one candidate on the Enemy cycle, not two, and that is a HUD matter; this
  entry is about rounds leaving the Pandora. Do not fix it by making the Balmorals friendly, since
  the player has to shoot them to the capture threshold. *Playtest after fix:* CM02, let the third
  Balmoral reach the Pandora and watch the rings. *Cross-refs:* `BL-714` (the same rings,
  the hull question), `docs/formats/anim-definitions/cutscenes.md` ("The airframe swap codes",
  967).

- `BL-741` `[Bug]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: data]` **A roster
  `rating_biases` exclusion naming a mission structure never reaches the structure's parts, so
  C4/M03's Black Swan wingman attacks the cargo zeppelin its own block excludes instead of the
  Black Hat fighters.** *Evidence:* `bswingman_1`'s block in
  `extracted/C4/M03/zrdr/aiv.zrd.json` authors `["cargozep1", -1.0]`, the hard exclusion, and
  the user's flight had the Fury take the cargozep's engine `leng31` at bias 0 and launch rockets
  at it. `cargozep1` is not a zeppelin record on this mission but a docked mission structure
  (`targets.zrd`, `MSG_TRGT_CARGO_ZEP`, nodes `cargozep1`), so its engines and turrets are
  separate `DestructibleRegistry` pools named `leng31`/`rturN` with no `Owner`, and
  `AiTargetRanking.ObjectiveBiasFor` matches a pattern against the pool's own name and its
  owner only; a zeppelin record fans its node name onto its zones, a structure fans nothing. The
  cargozep's parts carry an enemy team from the gamez ownership slot, which is why the wingman
  sees them at all and why the designers excluded them by name. The AI's structure pool is the
  registry's every-pool list where the original's `TargetStruct` list is the mission-structure
  objects (`docs/org/aiPilot.md` "Target acquisition"), so a part that is one struct object in
  the original is many candidates here, none answering to the object's name. *Fix shape:* decode
  what a `TargetStruct` candidate IS for a docked airship: one object per `targets.zrd` node or
  one per damage pool, and what name `FUN_0041ae40` matches for it (a turret walks its parent
  chain at `+0x54`/`+0x58`). Then either fan the structure's node name onto every pool under it
  as `Owner`, the way `ZeppelinRuntime.WireZones` fans a record's, or offer the structure as one
  candidate. *⚠ Traps:* `BL-400`'s curated `targets.zrd` list is the player's Non-Aircraft
  cycle and a separate question; do not merge the two. The wingman +0.4 and the structure
  handicap `struct_bias` (def `+0x13c`, applied to every turret and structure candidate as the
  scorer's `+0x344`, −200 on the `w*` wingman defs and 0 elsewhere) are separate terms, not this
  bug. *How you'd know it worked:* a unit on the bias match for a structure part, and the
  `campaign-roster` shape on C4/M03: `bswingman_1` ranks no cargozep pool at all while the
  Brigands still rank. *Cross-refs:* `BL-400`, `BL-476`'s closing commit (the zeppelin owner
  identity), and `git log --grep=BL-740` for the acquisition sweep this rides on: it is what put
  turrets and structures in front of the flight law, and its `ZeppelinRuntime.WireZones` fan is
  the pattern a docked structure lacks.

- `BL-799` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **An AI aeroplane
  cannot rank a surface hull as a target at all, so a boat or a turret truck is invisible to every
  AI pilot and the `patrolboat*` / `t_truck*` exclusions nine missions author are inert.**
  *Evidence (decoded):* the original's target scan walks `VehicleList` (`DAT_0071dabc`)
  unconditionally, and that list holds the AI ground and sea vehicles beside the aircraft, built by
  the same spawn (`docs/org/targeting.md`, "Ships and vessels are in the turret's set, and they are
  there as vehicles"). CSVM's ranked pick drops one: `FlightController.SelectRankedTarget`'s
  vehicle loop skips every candidate with `c.Source is not FlightController`, so a hull never
  reaches `_rankCandidates` and `AiTargetRanking.ObjectiveBiasFor` is never asked about it. The
  turret path does admit hulls (`TurretController.AcquireTarget`), so this is the aeroplane's gap
  alone. *Impact:* small today, since every shipped bias naming a hull is the hard exclusion
  `-1.0` (C1B/M03's `wingman_1..3` and `devastator_2/3` on `patrolboat*`; C3/M01, C3/M04, C3/M05
  and C4/M04 on `t_truck*`), so the fix mostly makes an authored "leave it alone" mean something.
  *Fix shape:* admit a hull to the vehicle loop the way the turret and structure loop already
  admits its sources. ⚠ It carries no `FlightController`, so the loop's `primary_target` name term,
  the wingman and human flags and the allied-attacker count each need a source-typed read rather
  than the `fc` cast they share now. *⚠ Traps:* do not register a hull as an aircraft to get it in;
  the same page warns that a port reaching this by promoting a hull has ported the wrong mechanism.
  *Cross-refs:* `BL-741` (the other half of `rating_biases` coverage), `git log --grep=BL-637`
  (the hull's own name line, found through the same missing switch arm).

## Flight model & collision physics

- `BL-443` `[Fidelity]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The G ramp reads the same tick's delivered lift; CSVM's is one step
  late.** `FUN_0048fc40` (call `0x48c883`) writes the delivered body-up G and the ramp reads it at
  `0x48ca1e` in the same tick, before the torques; `FlightModel.Step` rotates before it translates
  and reads the previous step's. Porting is the force-from-entering-attitude order of `Step`, which
  moves every envelope row, so it needs its own eleven-airframe `--dump-flight=all` A/B with each
  moved row attributed. Bounded: the ramp bites near `highGs` (9) and the stock full pull peaks at
  5.83 G. Ledger row "the G ramp reads the SAME tick's delivered lift" in
  [`docs/org/flightModel.md`](docs/org/flightModel.md).
- `BL-447` `[Fidelity]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The five unsupported nitro and shake edges in the parity ledger.**
  `docs/org/flightModel.md` "Parity ledger", class unsupported, beyond `BL-443`: the
  `level_off_rate` auto-level torque, the AI's `medium_aishake` on a nitro engage, the AI's
  positional `snd_nitro` blip, the nitro decay lockout on a runtime callback, and the mouse-flying
  arm's `is_autogyro` roll/yaw exchange. The thin atmosphere band above 2000 m has left this list:
  it is decoded and live, and it is the flight ceiling (`git log --grep=BL-448`).
  Each is small and independently landable; each names its address in the table.
- `BL-774` `[Fidelity]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: data]` **The sustained climb plateaus at 204 mph against the original's
  filmed 163, and the ceiling now shows it.** `--dump-flight`'s sustained climb settles at 204.03 mph
  on a 56.3° path where the footage reads 163.05 at 55.5°, so CSVM crosses the 2000 m band edge at
  75.8 m/s where the original crosses at about 61. With no clamp cutting every climb off at 2003 m
  (`git log --grep=BL-448`), that residual reads as altitude: the apex above the edge is
  `v_y² / 2g`, so a 24% fast climb reads as a ceiling several hundred feet high. *Lead:* the residual
  is the same α question as the pitch rate, recorded in
  [`docs/org/flightModel.md`](docs/org/flightModel.md) "The sustained climb"; the entry angle is free
  after the first step, so the candidates are the lift-demand lag and the attitude-thrust terms rather
  than a climb-gravity scale, which the decode already disproved. *⚠ Do not* close the gap with a
  constant on the ceiling: the height above the edge is bought with the climb and has no term of its
  own. *How you'd know it worked:* the plateau moves toward 163 mph and `altitude-ceiling` falls
  toward the ~7,000 ft the original reaches at the controls.
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
  is dead** — the entry used to claim 72, 102 and 177 ms ticks discarding about six sim steps each
  against Godot's default `max_physics_steps_per_frame` of 8; over four paired 83-second runs on the
  current build no window's worst tick reaches 133 ms, so nothing exhausts the cap. **Do not chase
  the sustained step** either: the ~39 ms step and half-speed sim the entry once claimed were a
  misreading of Godot's `physics_ms` monitor, which holds the WORST tick of the last wall second
  (`docs/verification.md` PERF-21). The recurring 18 to 25 ms band that dominated every earlier
  reading was the ungated once-a-sim-second telemetry print, one write per live aircraft in the same
  tick (PERF-23); it is gated and gone, so do not re-derive it. Compare durations in sim seconds,
  never wall seconds; state which mode a re-measurement flew (the numbers here are `--no-det` with
  nobody at the controls, so they under-weight projectiles and destruction cascades); and do not
  raise `max_physics_steps_per_frame`, which deepens the catch-up spiral rather than recovering
  lost steps. *Cross-refs:* `PLAN-M5-polish-6` C22, `BL-606` (the same per-sim-step
  suspects seen as an allocator), `docs/verification.md` PERF-21 and PERF-23.

## Environment & world

- `BL-070` `[Bug]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` `[C5]` **C5's `poleflare` clutter renders with the wrong billboard axis** (one of two residuals
  from polish-3 item 5, 2026-07-22; the other — the static collider probe's off-by-6/11 — closed
  2026-08-04, the archived development log's "M3 polish-6 C22" entry, with a rewritten probe now committed at
  `analysis/collider-probe/`). The `cblock*` templates ship `lightpole` (`CylindricalY`) posts
  *and* `poleflare` (`SphericalY`) glows — 33,682 of each in `cblock1` alone. `ClutterBuilder.Kind`
  carries no per-kind billboard mode, so every kind goes through the one Y-axis shader: the glows
  spin upright instead of facing the camera. Now *detectable* (the shared
  `SceneBuilder.ClassifyBillboard` distinguishes the two), but fixing it means giving `Kind` a
  billboard mode and a second material path, and it changes how 139,388 C5 sprites look with no
  reference shot to check against — so it needs an original-game A/B.
  *The "should be exempt from the SUNLIGHT dim" half is a data question, not a texture one.* The
  original's hardware draw has no per-texture lighting exemption (`docs/org/vertexLighting.md`,
  "The hardware draw"); the only exemption is the model's own `lighting` flag, and whether the
  `poleflare` and `lightpole` decoration models carry it is read off the templates, not fitted.
  What remains here is the billboard-axis question, and it still needs the A/B.

- `BL-076` `[Feature]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: data]` **Star twinkle + undecoded light fields** (flags 523/…, the 0.17 float) — stars/beacons
  render as fixed-size soft sprites, no twinkle.

- `BL-331` `[Feature]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` **Aircraft cast no ground shadow; the original draws one, straight down**
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

- `BL-272` `[Tuning]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Precipitation: every unit mapping from `weather.json` to a look is invented, and
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

- `BL-322` `[Bug]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` `[C5]` **C5's lit facades render ×0.58–0.66 of the original with WorldLight already at
  clamp 1.0** (split out of `BL-303` at its close, 2026-08-08; measured `CAP-11`: tower faces 10.2
  vs 15.5, low-rise 21.7 vs 37.6). Explicitly NOT fog — `BL-303`'s own adjunct note, and the Wave
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
  28, C4's 45); and the **plane-local ambient wisps** each chapter's `speed_cue.zrd` emits 60 m
  ahead of the player (`Flight.SpeedCue`, `docs/formats/effects.md`) are a third. A claim about one is not
  evidence about the others, and the first two **share their textures** — `--tex-override` on
  `cloud1.tif`/`cloud2.tif` paints both (`SHOT-21`), so separate them by altitude or cluster
  position, never by texture.
  ⚠ Traps: `csky_world_light` is CAP-11-calibrated on terrain — a directional cloud term must be
  cloud-local, the way `C22`'s deck fix was deck-local. And C1's own daytime cards were measured
  faithful at `lighting: false` (`C21`/`C23`), so this must not become a second global cloud
  brightness knob beside `FogVolumeClutter`'s `CardVertexColorTune`.
  *Playtest after fix:* the C1B night spawn above, against `playtest/CAP-11/`'s t5 and t16
  frames, saying for every measured puff which side of the moon it faces.
  *Cross-refs:* `docs/formats/effects.md`'s speed-cue section (the third population), `BL-327` (whether `lighting` gates `WorldLight`
  on a cloud card at all — if it does not, this item's arithmetic changes), `CAP-11`.

- `BL-327` `[Research]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: footage]` **Is `lighting: true` on a `Facade` cloud card a `WorldLight` gate at all —
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

- `BL-328` `[Tuning]` `[S]` `[Next: decide]` `[Impact: none]` `[Evidence: data]` **The deck floor's 20,480 m annulus half-span was sized against a mechanism
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

- `BL-329` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The D32 in-cloud flicker's rate and ramp are declared
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
  *Evidence:* `PLAN-weather-decompile-match`'s D32 entry; the curve shapes themselves
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

- `BL-341` `[Research]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: data]` `[C5]` **Reopened `BL-250`: with the real `no_clutter` gate landed, 7.6% of C5's ground
  (13.8 million m², the flagged overlay area with no base layer beneath it) renders bare, and
  whether that is what the original does is untested.** `BL-250` closed 2026-08-07 on a curated
  list (`ClutterBuilder.BuriedClutterDistricts`, excluding `cblock4/5/6` map-wide) that turned out
  to be standing in for a mechanism nobody had decoded yet. B13/B15
  (`PLAN-clutter-uv-placement`) landed that mechanism — `PlaceOnMesh` now skips a
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

- `BL-680` `[Research]` `[M]` `[Next: code]` `[Impact: none]` `[Evidence: trace]` **The compiled symbol table resolves nothing for `brig`/`fury`/`peace`'s
  nested hook arms at `ParkDockingHook`'s staging point, though the same lookup succeeds later.**
  *Evidence:* `_resolver.SymbolClaims` returns "claimed but unbuilt" for `l_arm1`/`r_arm1` under
  `brig_hook`, `fury_hook` and `peace_hook` when `ParkDockingHook` runs, while the identical
  node and definition pair resolves during ordinary animation dispatch afterwards.
  `AnimRuntime.ParkDockingHook` works around it with a scoped plain-name walk (`FindNamedChild`).
  *Fix shape:* find what the staging point has not yet built or indexed that the later dispatch
  has, then decide whether the park should move after it or the index should be complete earlier.
  *⚠ Traps:* the workaround is a name walk and will pick the wrong sibling if these airframes ever
  gain a duplicate arm name, so it is a stopgap rather than an answer. The three airframes that
  fail are exactly the three whose retract parks on the wrong axis, so check whether the two are
  the same underlying data shape before treating them as separate questions.
  *Cross-refs:* `BL-630`'s closing commit.
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
  thousands of coplanar foliage and railing cards a hard cutout currently keeps in the opaque pass
  — the coastline sheets were safe because they are few and flat, and that does not generalise.
  A period video card without a 4444 or 8888 texture format collapsed `Full` alpha to 1 bit at
  threshold 128, so a 1-bit look in reference footage may be the hardware and not the intent; check
  which the shot is before treating it as the target. *Playtest after fix:* a low pass over C1's
  tree lines and C2's Eiffel replica, where the erosion this rule was written to prevent would show
  first. *Cross-refs:* `analysis/alpha-classification/FINDINGS.md`, which carries the decode and the
  install-wide census; the coastline commit that filed this (`git log --grep=SoftAlphaCoastline`).

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

- `BL-735` `[Bug]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: trace]` **The multiplayer zeppelins' belly rings stand over no hull collider in six of the eight chapter worlds.**
  *Evidence:* on `multiplayer1zep` and `multiplayer2zep`, a plain unrestricted
  `CollisionLayers.World` ray cast straight up from the `WorldPosition` of `ctur1`, `ctur2` and
  `ctur3` finds nothing for over 100 m in the C1, C1B, C2, C2B, C3 and C4 worlds, while the same
  models read solid in C1C and C5, which reads as a per-chapter revision of the shared vehicle
  mesh rather than a runtime defect. The pirate zeppelin's seventeen rings all find hull where
  their arcs say they should (`turret-hull-blocks-own-fire`, `git log --grep=BL-714`). Reachability
  by a hostile ring in play is not confirmed: it needs the hull armed hostile in a skirmish.
  *Fix shape:* compare the two hull meshes' collision-bearing nodes across chapters against the
  gamez, say whether the six chapters ship a thinner mesh or the builder drops a node, and give
  the belly rings a hull to hit. *⚠ Traps:* a ray through the hull node's origin is a
  false-positive census, since a hull's origin is not its mesh centre; test on bearings inside a
  ring's own arc. *Playtest after fix:* a Dogfight against a `multiplayer1zep` hull, flying the
  belly. *Cross-refs:* `BL-714` (the pirate zeppelin's rings, cleared on a parked hull),
  `docs/architecture/Mech3.md` (`WorldCollision.cs`, `WorldBuilder.cs`).

- `BL-800` `[Fidelity]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: decoded]` **The world build applies a gamez node's `flags.active` only at world roots, while the original applies it to every node.**
  *Evidence:* `WorldBuilder.Add` writes `built.Visible = node.Active` on each child of `world1`
  and `SceneBuilder.BuildSubtree` never writes visibility, so a node below a world root builds
  shown whatever its flag says. The original copies the flag straight through:
  `gClsBlockReadNode` (`FUN_004e3690`, `cls_zbd.c`) reads the 212-byte node record and copies its
  first 208 bytes into the live node object, so bit 2 of `node+0x24` is the record's own
  `flags.active`, at every depth, and only `gwNodeSetActive` (`FUN_004cca30`) moves it afterwards.
  32 world nodes across the eight chapters sit in that gap: `letterbox` and `sunlight` in all
  eight, C1's nine barrage-balloon turret `healthy` variants (`lifesaver11`…`lifesaver33`, each
  `bbtur<nn>/healthy`, which `attack_balloon<nn>` is what switches on), C3's three
  `britbalmoral_<n>/markers/pylon8/bb_approach<n>/half_cone/land_on`, and C4's four parentless
  `anim2_autogyro`/`anim2_warhawk`/`anim2_brigand`/`anim_warhawk` roots. *Fix shape:* apply the
  flag where the subtree is built rather than at the walk root, then census what changes.
  *⚠ Traps:* several of the 32 are already switched off by another mechanism (`letterbox` by
  `WorldSession`, the destructible `healthy` variants by their own `RESET_STATE`), so the fix must
  be measured against what the world actually draws, not against the flag count. `sunlight` is a
  light rather than geometry and does not go through `Visible` at all. No prerequisite node is
  affected: every one of the 784 REQUIRED `ACTIVATION_PREREQUISITE` node entries names a node
  shipping `active: true`, so the animation gate reads the same state in both engines.
  *Cross-refs:* [`docs/formats/anim-definitions.md`](docs/formats/anim-definitions.md) ("The node
  form reads the node's own live flag"), [`docs/formats/gamez.md`](docs/formats/gamez.md)
  (`flags.active` as initial runtime visibility), `WorldBuilder.cs`, `SceneBuilder.cs`.

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

- `BL-335` `[Fidelity]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` **Our puffer blend verdict reads the sprite's darkness; the original reads a
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

- `BL-035` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: data]` **Animation event kinds that need weapons or cutscenes — `CALLBACK`, `OBJECT_CYCLE_TEXTURE`,
  one-shot `SOUND`** (triaged 2026-07-22, the last of `PLAN-anim-rendering-followups`
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

- `BL-293` `[Tuning]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **Rocket impact rings: the fixed-axis upper ring is faithful but reads
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
  `PLAN-m3-polish-10` A3).

- `BL-535` `[Bug]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` **A repeat sonic burst pays a 10 to 14 ms slot re-reset once the pool recycles a still-live slot.** With every emitter pre-built at bind (`AnimRuntime.PrewarmEmitters`), the weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08 --weapon-fire --infinite-ammo --weapon-surface=default --weapon-standoff=90 --no-det --no-vsync --seed=1 --no-pads --frames=1200 --screenshot=<path>`) with `hitchMonitor.floorMs` 10 and `medianMultiple` 1.2 in `CSVM/config.json` records `effect_checkout` samples of 15.6 ms and 12.2 ms in `.hitches.jsonl`; under the stock monitor it never trips. `AnimRuntime.ResetCheckedOutCopies`'s own def/anchor loop is cheap and constant (17 defs, 17 matched anchors every burst); the cost sits inside `RESET_STATE`'s `ObjectOpacityState` dispatch on the ring defs — `PoseChannel.SetSubtreeOpacity` → `ApplyOpacity`'s recursive subtree walk plus `WorldCollision.SetFaded` → `SyncSubtree`/`FadedAbove` — and lands exactly at the pool wrap (a per-burst `Stopwatch` shows burst 4 cheap, then the log's own `anim: effect pool for 'sonic_ground_effect' recycled slot 0 of 4 while it was still live` line, then burst 5 onward expensive), not two bursts before it as first measured.
  *Where to look:* isolate `ApplyOpacity`'s material/shader-param cost from `SetFaded`'s collider-resync cost before changing either — `EnsureOpacityPath`'s own `Shader.Code.Contains` scan is measured NOT to be the bottleneck (under 0.1 ms typically). Both are shared machinery well beyond the sonic burst; `WorldCollision._fadedRoots` is a single process-wide counter, so `FadedAbove`'s ancestor walk degrades for every currently-faded object in the world, not just this one, once more than one is faded at a time.
  *Cross-refs:* `BL-231` (closed; the pool-size judgement this was measured under), the `effect-pool-reset` suite (the pose contract the re-reset keeps).
- `BL-537` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Effect pools at four players, judged in play.** The pool sizes in `CSVM/data/effect_pools.json` were re-judged on a build with no first-use construction cost: rockets and the sonic burst never wrap, a four-object simultaneous death wraps `flame_ball_01` at 4 and 6 slots and is quiet at 8 (now shipped), and seven or more identical deaths in one frame wrap at the 16 ceiling and cannot be sized away. At the controls the single-player half reads right: four fireballs burn out in place, and the seven-death wrap is not visible under the debris. Still owed: a 4-player splitscreen session with everyone firing, judged for anything that reads as shared between panes, and the ceiling for many-player builds (at 16 players the default root wants 19 and gets 16). The instrument is `AnimRuntime.PoolRecycles` and the `anim: effect pool for '<name>' recycled slot` DEBUG line in the log file sink; the sizes staged print on the world-effects build line. ⚠ Raise only a root that logs a recycle, never the default; the three gun roots stay at 1; a root sized 0 clamps to 1. Each slot copies the root's subtree (155 templates at 1 player, 263 at 4).
  *Cross-refs:* `PT-129` (the four-player flight that judges it), `BL-535` (the per-burst re-reset cost measured under the same instrument), `BL-296`/`BL-299` (the other splitscreen-scoped items).
- `BL-538` `[Bug]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: data]` `[C5]` **C5's city draws a dark band across its facades that the original never draws.** The dark band reported at the controls in C5 is not the clutter fade: it is the shipped hand-authored mip chain of the `cblock*` facade textures, whose level 1 is a non-monotone dip. `--dump-mips` reads `cblock1` at mean luminance 15.62 (L0) → 4.41 (L1) → 6.77 (L2) and `cblock2` at 9.60 → 0.83 → 1.84, so a facade near enough for L0 reads bright, one in the L1 band three to eleven times darker, and one far enough for L2 brighter again. `--mips=generated` removes the band completely; `graphics.clutterFarFade=false` does not touch it. The chain installs correctly (every `installed` line in the dump reads `== authored`), and the levels are the original's own art that must not be regenerated, so what is wrong is *selection*. Flown against the original at the controls (`PT-85`, closed), the original shows no band at any distance and under any of its video settings: "The original does not have the dark band at all. Tried different video settings. But the building view distance in the original is only inside the ring so perhaps the darkened building are just never visible." The artists tuned these levels against a 640×480 DX7 pipeline, and the reading the flight leaves is that the original stops drawing a facade before it ever reaches the distance L1 takes over, while the remake draws the same building out past that distance and shows an L1 the original's own camera never reached.
  *Where to look:* the original's building draw distance first, since that is what the flight points at: find the cull range the original applies to ordinary gamez scene nodes (the clutter fade is ruled out below, and is a different path) and compare it against the camera distance at which our sampler reaches L1. Then the sampler and any LOD bias on the world shader (`SceneBuilder.GetBiasShader` emits `filter_linear_mipmap_anisotropic`), and whether the original point-selected a level where the remake trilinearly blends L0 into L1 across a range. Instrument: `--dump-mips` for the installed chain, `--mips=generated` for the A/B.
  *Reproduce:* `.\RunProbe.ps1 --freecam --chapter=C5 "--pos=-9491,140,-3479" "--direction=-0.588,-0.03,-0.809" --det --mute "--screenshot=.scratch\band.png"`, then the same with `--mips=generated`.
  *Ruled out, do not re-chase:* collapsed clutter cards writing depth or a dark fragment (the fade-on frame is pixel-identical to `--no-clutter` in every row where the fade has culled every instance); C5's fog-volume clutter overlapping the templates fade (its field is 16,170 cloud sprites at `fade 1200-1800 m`, outside the 200–900 m the templates author); the gamez buildings carrying an ignored `far_fade_range` (`FUN_004d5de0` is reached only from the clutter instance list `FUN_004d5d90` and the clutter quadtree `FUN_004d6010`, both behind `CameraRenderClutter` in the world walk `FUN_004d5910`, while ordinary scene nodes draw through `FUN_004d4a20` and never reach the fade test); a per-texture lighting term (the original's hardware draw has none, and the band is a function of camera distance that lifts under a mip-policy switch, which changes no lighting term); and decorrelating the dither lattice per stamp, which was probed and changes the frame barely at all.
  *Cross-refs:* `BL-337` (closed; the fade), `docs/org/clutter.md`, `docs/formats/gamez.md` on the authored mip levels.

- `BL-555` `[Feature]` `[Divergence]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **A held key fast-forwards a mid-mission cutscene instead of
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
  `docs/formats/anim-definitions/cutscenes.md` "Handoff and skip"; `PLAN-M5-polish-2`
  E24 (the decode that made the two scenes unskippable).

- `BL-606` `[Perf]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: data]` **The settled GC pause is set by the finalizable-object count, and about 2000
  finalizable Godot objects a second keep it there.** *Evidence (traced):* with the session's
  build-settling transient excluded, a GC-verbose capture of a C1 cruise shows the pause tracking
  the finalization-promoted COUNT at 0.4 to 0.5 ms per thousand objects, consistent across two
  builds and 26k to 85k objects per collection, while ms per promoted MB varies several-fold over
  the same collections. `MarkFinalizeQueueRoots` promotes 0.00 MB at every collection, so it is the
  per-object queue walk and not resurrection marking, and queue registration happens at allocation,
  outside the suspension window, so it cannot contribute. `Godot.StringName` is the largest single
  source, ahead of the physics query parameters. *Fix shape:* reduce the RATE of finalizable Godot
  object creation on the per-frame path, or take the objects off the finalization queue where the
  binding allows it; caching `StringName` instances at their construction sites is the first move.
  *⚠ Traps:* **cutting ordinary allocation does not shrink this pause, it only batches it.** Halving
  the allocation rate moved the settled collection from gen0 every 13 s at 10 to 13 ms to gen1
  every 31 to 36 s at 25 to 27 ms, leaving total pause per wall second unchanged at about 0.8 ms.
  Judge any change on pause per second, never on per-collection pause or on collection frequency
  alone. Exclude the first 50 s of process life, which is the world build tenuring and reproduces
  to the byte. `docs/verification.md` PERF-13 (compare within one vsync mode) and PERF-19/PERF-20
  apply. *Cross-refs:* `BL-536`'s closing commit (`git log --grep=BL-536`), which corrected the
  premise this succeeds and landed the two allocator fixes, `BL-562` (the CM11 physics tick, now
  rewritten onto single-tick spikes: its steady step is 1.81 ms and its ray casts are 13.4 % of it,
  so the per-sim-step query objects cost measurable physics time even though C21 showed they are
  1.6 % of allocation), `PLAN-perf-hitches`.


- `BL-720` `[Bug]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: feel]` `[CM24]` **The Dante's engine-explosion
  puffers drift between the engines, and some sit at the wrong place.** *Evidence:* reported at the
  controls on CM24 (C5/M04): "puffers moving between engine explosions of dante" and "some puffers
  on the wrong position". The Dante's engine damage stages are the shared `damage1_..3_pz?engNN`
  and `destroy_pz?engNN` definitions (`extracted/C5/M04/mis_anim/`), the rig every pirate zeppelin
  plays. *Fix shape:* freeze a Dante engine kill in the anim lab, list the puffers that spawn with
  their anchor node and world position each frame, and read whether an anchor resolves to a sibling
  engine, whether a puffer keeps a world position while its anchor moves with the zeppelin, or
  whether a `TopLevel` anchor is missing on a moving hull. *⚠ Traps:* the Dante moves during the
  fight, so a puffer authored in hull-local space that is spawned in world space reads as "moving
  between engines" from the chase view. Check on a parked Dante first and then on a moving one.
  *Playtest after fix:* CM24, kill two adjacent engines on one side and watch the smoke from the
  external view. *Cross-refs:* `BL-121` (the `trail-world-anchor` suite), `BL-700` (the same
  zeppelin family's wreck rest).

- `BL-734` `[Bug]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: trace]` `[CM19]` **CM19's launch hook shows no aircraft on it before a Black Hat launches.**
  *Evidence:* `ai_warhawk_place` (`extracted/C4/M04/mis_anim/warlaunchhook-*.json`) parents the
  display node `anim2_warhawk` under `bmhookpoint` and translates it, and `launch_warhawk`
  switches that node on, runs the hook's SI script and switches it off again around raising 801.
  No CM19 objective wakes the place definition and `launch_warhawk` never calls it, so the display
  node is never under the hook, and the mission build still lists `ObjectAddChild` among the event
  kinds not acted on. The launch itself is landed (`git log --grep=BL-730`); this is the hook's
  appearance. *Fix shape:* read what starts `ai_warhawk_place` and `ai_brigand_place` in the
  original (a startup pass, an anchored reset, or the hook's own bootstrap), act on
  `ObjectAddChild` for a display node, and pin the node's parent before the first launch.
  *⚠ Traps:* do not spawn the roster aircraft on the hook to fake it; the display node is a
  separate model. *Playtest after fix:* CM19 to the first warning, watch the hook. *Cross-refs:*
  `docs/formats/anim-definitions/cutscenes.md` (801 to 803), `BL-730`'s closing commit.

- `BL-797` `[Fidelity]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **A choked engine's propeller keeps spinning silently, where the original winds it down with a sound.**
  *Evidence:* the disabled-systems mask's bit-`0x2` edges call `FUN_004b15c0` and `FUN_004b1630`,
  which swap the airframe def's `stop_props_anim` and `spin_props_anim` on two anim slots at
  vehicle `+0x6c8`/`+0x6cc` (decode: `docs/org/ordnanceTypes.md`, "What the mask's bit-2 edges
  run"). `stopprops` fires the `snd_propstop` one-shot, activates `staticprop1`..`3` and fades them
  in over 2.0 s, and fades `prop1`..`prop3b` out over 1.5 s before deactivating them; `spinprops`
  reverses it silently and instantly. CSVM plays `stopprops` at Destroy and at Crash and
  `startprops` at spawn (`Flight/FlightController.cs`, `Session/HumanFlightAdapter.cs`,
  `Session/AiFlightAssembler.cs`) but runs neither on the choke, so a choked aircraft's blur discs
  turn on at full rate with no cue. *Fix shape:* play `stopprops` through `CrashRuntime` on
  `TryChokeEngine`'s rising edge and reverse it when `EngineDead` clears, the same call shape the
  nitro edges already use, on the human rig and the AI one alike, with a suite that asserts the
  slot state across both edges. *⚠ Traps:* the restart side is the decision this item is waiting
  on. `PropAnimator` turns the discs procedurally at the same authored `-220`/`60` rates, so
  playing `spinprops` puts a second writer on the same node transforms; either suppress its
  `OBJECT_MOTION` and keep `PropAnimator`, or hand the spin to the anim runtime for the whole
  flight. Do not reach for `startprops` on the restart: it carries `snd_propstart`, and the
  original's restart is silent. Do not double the death-time `stopprops` when a choked aircraft
  then crashes. *Playtest after fix:* fly into a `TANGLER` cloud and watch and listen to the prop
  through the choke and the recovery. *Cross-refs:* `docs/formats/vehicle.md`
  (`spin_props_anim`/`stop_props_anim`), `docs/org/vehicleDamage.md` (the mask), `BL-406` (the
  choke itself), `BL-285` (the engine loop's start/stop inputs).

## Audio

- `BL-792` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: trace]` **One AI aircraft answers two listener models: its weapon voice culls against the nearest human, its engine voice against a viewport camera.**
  *Evidence:* `AiWeaponAudio` is attached with `_world.HumanPositions`, the nearest-human seam
  `ProjectilePool` measures its weapon one-shots against; `AiEngineAudio` is attached with
  `listeners: null` and falls back to the node's own viewport camera. In splitscreen those two
  answers differ, so the same aeroplane can be audible on one measure and culled on the other, and
  which pane hears it depends on which voice you ask. *Fix shape:* wire the engine voice to the same
  seam, so one aircraft has one listener model. *⚠ Traps:* this changes existing cull behaviour on the
  engine voice, which is why `BL-079` left it alone rather than folding it in; expect the engine
  voice's audible set to move, and check a 2-pane and a 4-pane session, not just a lone camera.
  *Cross-refs:* `BL-079`'s closing commit, `docs/architecture/Flight.md`'s `AiEngineAudio` and
  `AiWeaponAudio` entries.
- `BL-793` `[Feature]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: data]` **No turret has a gun voice: a carried gunner, a zeppelin ring and a ground mount all fire silently.**
  *Evidence:* `snd_turretgun` and `snd_chaingun` both carry `3D`, `LOOPED` and a `RANGE` pair in
  `sounds.zrd.json`, so the data authors them as world sounds, and no turret path plays a loop at all.
  `BL-079` built the positional per-aircraft voice and stopped at aircraft, that item being another
  aeroplane's weapons. *Fix shape:* a carried AI turret gunner can take `AiWeaponAudio` as it stands;
  a world mount (a ring, a ground emplacement) needs its own emitter under `WorldSounds`, since it has
  no `FlightController` to hang under. *⚠ Traps:* the cull is the cue's own `RANGE` audible distance,
  not `EngineAudioCurves.CullDistance`; a zeppelin carries seventeen rings on one hull, so decide
  whether they share a voice before giving each one an emitter. *Cross-refs:* `BL-079`'s closing
  commit, `docs/formats/sounds.md`'s channel section.
- `BL-794` `[Cleanup]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Two small debts in the new weapon-audio seam: the dry cue's name is a constant beside the data read, and the loop plays from the aircraft node rather than the muzzle.**
  *Evidence:* both audio paths take `WeaponAudioCues.EmptyClipDef`, a `snd_emptyclip` literal, while
  `WeaponDefs.EmptyClipSound` is the actual `NO_AMMO_WARNING` read; they agree in the shipped data and
  should be one read. Separately, `AiWeaponAudio`'s players ride the aircraft node, a couple of metres
  from the muzzle against a 20 m full-volume radius, and where the original places a firing emitter is
  unverified. *Fix shape:* resolve the dry cue through `WeaponDefs`, and settle the emitter's origin
  against the executable before moving it. *⚠ Traps:* the muzzle half is not worth a fitted answer:
  measure what the original does, or leave it, since at these distances the difference may be
  inaudible. *Cross-refs:* `BL-079`'s closing commit.
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
  *Cross-refs:* `PT-126` (the flight that names the sound and matches its level).

- `BL-269` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The 3D sound falloff curve between the authored `RANGE` radii is an admitted
  approximation** (`WorldSounds.cs:159-161` — endpoints authored, curve "an approximation of
  the original's, hence TUNE"). Low stakes per sound but global: every positional sound's
  audible footprint. A calibrated fly-past recording of one loud fixed emitter (the C1
  refinery flare is a candidate) would trace the real curve.

- `BL-281` `[Tuning]` `[Blocked: CAP-27]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The ricochet sounds are audible but very faint.** `PT-25` (c), 2026-08-05:
  `snd_ricochet1–4` play under the per-impact spark burst but sit too low to read. A mix-gain
  question with no reference recording behind it, to be judged at the controls rather than derived. ⚠ Judge only after `CAP-27` decides
  whether the original has this effect at all: `BL-090` already calls the 0.99 `injure_anims`
  entry that drives it "plausibly an authoring leftover", present on 1 of 11 aircraft, so the
  capture may delete the feature rather than tune it.

- `BL-285` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Engine start/stop residues from `BL-267` (landed 2026-08-05)** — two constants
  pending the user, both in the same cockpit sitting. (a) `ThrottleSlamSmoke.SlamThreshold`
  0.25: the CAP-21 footage only bounds the slam gate to "a single 1/8 step never fires,
  idle→5/8 fires" — the 2/8–4/8 band is unobserved, so 0.25 is the smallest threshold
  consistent with both and a declared TUNE. (b) The listen A/B: `EngineStartRamp` is now the
  `startprops` authored 2.0 s and the crash/destruction wind-down plays `snd_propstop` — judge
  both by ear. ⚠ Trap: `BL-268` (`PLAN-m3-polish-10` C21, landed 2026-08-06) removed
  the blanket ×0.2 mix scale on these same paths, raising the own-ship mix ~5× — judge the
  ramp/stop cue against the new, unscaled level, not the old ×0.2 one. ⚠ Second trap, added by
  `PLAN-splitscreen-polish` D32 (`BL-371`, landed 2026-08-15): `snd_propstop` (the wind-down
  half of this A/B) now carries splitscreen's `MixGain` too — 1 in 1P, so this pending single-pilot
  judgement is unaffected, but a splitscreen listen must judge it at whatever `N` the pilot is
  testing, not assume the 1P level. `snd_propstart` (the other half of this A/B) is unchanged —
  D32 kept it raw, "your prop" on respawn stays loud on purpose.
  *Cross-refs:* `PT-127` (the cockpit sitting that judges both).

- `BL-391` `[Tuning]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **Own-ship engine loop reads too loud, including single-player.** Found
  2026-08-15 at the `BL-126` splitscreen chrome playtest — a 4-player Dogfight session flagged the
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
  already lives. Built-in's Options screen holds three steppers (difficulty, menu presentation,
  graphics mode) and the Controls door, and its apply hands back every setting it does not show
  untouched (`CSVM/src/UI/LaunchMenu.cs:181-191,1466-1560,2449-2456`), so once the page lands a
  player on Built-in can hear the mix and not reach it. Blocked until the four levels exist in the
  store, which is the whole of the dependency: nothing else about this item waits on that plan.
  *Fix shape:* four rows on Built-in's Options screen reading and writing the same store fields the
  AUDIO page does, in the screen's own stepper convention, applied through the same
  `OptionsApplyExit` the three current rows leave by.
  *⚠ Traps:* Built-in has no continuous control of any kind, so a 0 to 100 level is a stepper with a
  chosen step rather than a slider, and the step size is a judgement the row has to make rather than
  inherit. Do not add a second writer: `Launcher.ApplyOptions` is the options file's one writer and
  both presentations reach it through the apply exit. Do not re-tune `MusicPlayer.ChannelLevel` on
  the way past; `BL-455` deletes it.
  *Cross-refs:* `BL-455` (the page this mirrors), `PLAN-audio-preferences`, `BL-783` (the same gap
  for the display settings), `docs/menu-presentations.md`.

## Cameras & views

- `BL-150` `[Feature]` `[L]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` **plan-sized — not a TUNE. Numpad camera views — the whole scheme needs a rebuild, not a
  retune.** ⚠ **This IS the chase camera's head-look controller, not nine authored poses — read
  `BL-435` first.** `PLAN-cockpit-view`'s decode of `FUN_0042c7f0` found the numpad views run the
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

- `BL-260` `[Feature]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` **The decoded death and flyby cameras are not implemented; shared static-camera world clearance is unwired.**
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

- `BL-266` `[Research]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **Plane wobble: residual decode questions after the
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
  - **(e) `nitro` is the third source with the same modelling gap.** Judged at the controls on a
    nitro engage: "it wobbles the plane, amplitude and frequency not quite right". ⚠ **The plane
    wobbling is the decode, not the defect** — [`docs/org/shakes.md`](docs/org/shakes.md):17-22 has
    the plane wobble with a plane-mounted camera inheriting it, so only the magnitude and rate are
    in question, and `PT-86` asked for a *camera* shake it should not have. `shakes.zrd` authors
    `nitro` as `frequency 4.0, damp 3.0, sawtooth 1, magnitude 0.05`, read unchanged, and
    `PlaneShake` renders it as a sawtooth under an envelope — the same envelope-versus-random-walk
    mismatch as (a) and (d), which is why this is a clause here and not its own item.
    **Judged at the controls, and the answer settles the shape rather than the scale:** "Janky at
    the beginning (larger but very fast) and then too small but still very fast." That is three
    facts at once — the opening kick is too big, the decay to too-small is too quick, and the RATE
    is wrong for the whole duration. A magnitude constant cannot produce that; it is the sawtooth
    standing in for a random walk, which reads as a fast regular buzz where the original wanders.
    So the mechanism is the fix here, exactly as in (a) and (d), and the factor-of-two ambiguity in
    the magnitude is secondary — do not spend another pass on it before the walk lands.
    ⚠ **Still do not wire a number:** two repo sources contradict each other on which triple is
    position and which is velocity (`docs/org/shakes.md`:168-173 against
    `analysis/gun-wobble-shake/FINDINGS.md`:168-182, which says the reverse twice), and the
    `sawtooth 1` branch constant coincides with the authored `frequency` of 4.0, which is exactly
    the coincidence the trap below warns about.
    *Still unanswered:* whether the original's engage moves the nose or only the roll.
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
- `BL-420` `[Research]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **Decode the original's per-view base FOV from `crimson.exe` and record it under `docs/org/` — the engine holds a single 62° assumption that the binary refutes.** The original's camera projection has **exactly two base horizontal FOVs, 60° and 80°, both stored in radians as half-angle constants** (`1.0471976` = `92 0a 86 3f` and `1.3962634`), and **which one applies is gated per-camera-mode** (live mode at `camera+0x14c`, selected in `FUN_0042b660`): mode **6** → 80° (`FUN_006024d9`), every other mode (0–5, 7, 8, 9) → 60° (`FUN_00602508`). Modes 6 and 7 are the only two first-person views (both set the `DAT_009fd17c` first-person flag via `FUN_004e7100`, both route through the first-person placement `FUN_0042d980`, neither uses chase-position math — `FUN_0042dc20`/`FUN_0042c5c0` dispatch). So the three named views resolve definitively: **3rd Person / chase = 60°; Cockpit view = mode 6 = 80°; Nose view = mode 7 = 60°**. The cockpit/nose assignment is pinned by a direct render gate: `FUN_0049fb00` (the per-frame player render, sole caller `FUN_004a0220` = main tick) draws the cockpit interior model `cockpit1` (`DAT_0071c314`) **only when mode == 6**, so mode 6 is the interior cockpit view (80°), and mode 7 is the no-interior forward view (60°). The two first-person views also share the **same camera position** — both place the camera at the plane's `cockpit_camera` marker (`DAT_0071c328/32c/330`), so there is **no separate nose-camera offset**; mode 7 differs only in hiding the interior/hull, keeping player head-look without autohead, and using 60°. The constants are **horizontal**, and the correction `FUN_006024d9`/`FUN_00602508` apply is the plain one at the 4:3 the original ran: `atan(tan(H/2) / (4/3))` → 60°→46.8° vertical, 80°→64.4° vertical, both **4:3** figures rather than 16:9 ones. ⚠ The factor 0.75 reads equally as `1/(4:3)` and as `(4/3)/(16/9)`, so arithmetic cannot separate them and the 4:3-only mode list is what decides it (`docs/org/cameraViews.md`, "FOV constants and aspect correction"). The project's current single **62° vertical assumption does not exist in the binary** — the 62°-in-radians constant `1.082104` (`63 82 8a 3f`) is absent, so the assumed number is unsupported and the correct base is 60°.
  *Evidence:* ghidra-mcp read of the open `crimson.exe` (`/crimson.exe`): `FUN_0049fb00` (player render; draws `cockpit1` `DAT_0071c314` only when mode==6 via `FUN_004cca30(x,1/0)` around the interior draw), `FUN_0042b660` (mode gate), `FUN_00602508` (60° H-FOV; writes `_DAT_00a1eff0`/`_DAT_00a1eff4`), `FUN_006024d9` (80° H-FOV, mode 6), `FUN_0042b570` (frustum/projection, contains `0.5235987755982` = 30° = 60°/2), plus the 60°/80°/50.0/2.5 constants side-by-side at the data table `0060409c`. FOV is stored in radians (anim loader `FUN_00502da0` converts degrees→radians via `0.017453292`). The `0x3f860a92` 60° literal is also used by `FUN_0049d940` (player aim camera) and `FUN_004a0220`. Camera object is `DAT_0064ef78`. Placing the camera: both first-person modes run the same placement `FUN_0042d980`, which sets the camera to `plane_pos + plane_rot · (DAT_0071c328,32c,330)`, i.e. the plane's `cockpit_camera` marker offset (bound in `FUN_00473480` from the `cockpit_camera` node; default fallback `DAT_0075d1b8/bc/c0` = `(0,0,0)`). Plane-model `cockpit_camera` node translations (decoded from `extracted/C1/... planes/nodes.json`) put the camera on the fuselage centerline a bit above the local origin — default fighter `player_pfighter`: `(0, +0.75, −0.2)` — with +Y up, ±X the wingspan (ailerons at ±63, elevators/tail at −Z ≈ −37), so +Z = nose/forward and the marker is centered, ~0.75 up, marginally aft of the origin. There is **no `nose_camera` node or per-mode offset** — mode 7 reuses the cockpit_camera point. The `cam_anim` ZAN cockpit sequence (`player-gi_1stperson`) carries no FOV (it shows the interior/hides the plane via `cockpit1`/`camera1`), so the base FOV is not authored in `.ani` data.
  *Fix shape:* **the decoded facts landed as [`docs/org/cameraViews.md`](org/cameraViews.md) (2026-08-18), and the mode-6/mode-7 first-person half of the model landed in code** (`PLAN-cockpit-view` A3): `CameraController.HorizontalToVerticalFovDeg` renders Cockpit at 80° H and Nose at 60° H, both aspect-corrected off the live viewport, deliberately scoped to those two new modes only (Decision 3, "new modes only") and never touching the engine's 62° global. **What remains is the EXTERNAL half.** `GameSession.cs:475`/`:2624` and `Launcher.cs:490` still write the single 62° vertical global to every chase/fixed-numpad/back/pad-look/crash camera. Migrating those three sites to the decoded 60° horizontal base (with the aspect-corrected 46.8° vertical this page already pins) is the remaining work, and it unsettles two judgements made against the current 62°: `PLAN-overcast-match` line 1463's overcast sky match and `docs/org/tracers.md:258`'s tracer calibration. Carry that warning into whichever session does the migration — both need re-judging after the base FOV moves, not just re-measuring against the same footage.
  *⚠ Traps:* (i) **The two `CAMERA_STATE`/`CAMERA_FROM_TO` functions (`FUN_00502da0`, `FUN_00503e70`) are animated/in-script FOV changes only (`.ani` H/V_FOV events) — not the base per-view FOV; do not wire the engine's base FOV to them.** (ii) **The 80° is attached to camera mode 6 specifically, not "first person" generally** — mode 7 is also first-person but is 60°, so gating on "is first person" alone would read the mode-7 number wrong. (iii) The `Virtual Cockpit` string is a HUD/perf/zoning label (`FUN_0059c340`), not a view — ruled out. (iv) ~~Which of cockpit vs nose is mode 6 (80°) vs mode 7 (60°) was not pinned~~ — **resolved**: the `FUN_0049fb00` render gate (`cockpit1` drawn only when mode==6) pins mode 6 = Cockpit (80°) and mode 7 = Nose (60°). The remaining subtlety is that **both modes share the same `cockpit_camera` position** (no separate nose offset exists), so "nose" is a render/head-look/FOV variant of the same camera point, not a physically different marker. (v) "62°" invariants elsewhere are the assumption being corrected, not corroboration.
  *Cross-refs:* `docs/org/cameraViews.md` (the landed Nose view shares the Cockpit camera point but uses the 60° base), `BL-150` (numpad fixed-view FOV calibration is still missing — a documented 60°/80° base + the aspect conversion is the calibration input it needs), `docs/formats/camparam.md` (chase/tuning only; does not cover FOV), `PLAN-overcast-match` line 1463 and `docs/org/tracers.md:258` (the 62° assumption to correct), `PLAN-cockpit-view` A3 (landed the mode-6/7 half of this model).

- `BL-432` `[Feature]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The original selects head-look behaviour by key; CSVM infers it from which
  device moved — a recorded behavioural difference.** `OriginalScreenshots/Keybinds Views 1.png`
  binds `K` **Access Snap Look Mode** and `J` **Access Smooth Look Mode**, both free in CSVM's
  flight scheme today. The look-state byte the head-look controller reads (`DAT_0064ef68`,
  `PLAN-cockpit-view`'s decode of `FUN_0042d010`) is a mode selector with three values — `0`
  snap, `1` free-look, `2` padlock (`BL-399`) — that the original's player flips explicitly with
  these two keys. `PLAN-cockpit-view` C21 instead infers the mode from the input source: the
  numpad snap cluster snaps, the mouse/right stick pans smoothly, and both are live at once rather
  than one active mode at a time. That is a genuine behavioural difference, not just an unbound key:
  the original's player cannot free-look while snap is the active mode (or vice versa), and CSVM's
  player always can.
  *Fix shape:* either wire `K`/`J` as an explicit mode toggle gating which input path
  `HeadLook.Step` honours that frame, or judge the device-inferred behaviour as the better port and
  record why. Build beside `BL-399` — it is the same byte's third state.
  *Cross-refs:* `BL-399` (padlock, the byte's third state), `PLAN-cockpit-view` C21 (`HeadLook`,
  `src/Flight/HeadLook.cs`).

- `BL-435` `[Feature]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: decoded]` **The original drives the chase camera through the same head-look controller
  as the cockpit views; CSVM's chase view has no look-around at all.** `FUN_0042c7f0` (the chase
  placement dispatcher) calls the identical `FUN_0042d010(0xbfc90fdb, 0)` that
  `PLAN-cockpit-view` C21 already ported as `HeadLook` — same states, same snap table, same
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
  ⚠ **This item owns the KEYBOARD/MOUSE half only; the pad is already built and is deliberately
  not the decoded law.** CSVM splits the look-around by DEVICE, not by view: the right stick aims
  ABSOLUTELY in both the chase view and the two first-person ones, stick position mapping straight
  onto one shared envelope (`HeadLook.PadLookYawMaxDeg`/`PadLookPitchMaxDeg`) and releasing back to
  the settled pose, which is a UX call for this port and not in the original at all. The decoded
  relative controller above is what the numpad snap cluster, the centre key and the mouse ride, and
  extending it to the chase camera for THOSE inputs is the work still owed here. Building this must
  not take the pad off its absolute path, in either view: that is the behaviour the controls were
  judged on. The pad's own bound is the chase camera's gimbal margin (±60° pitch), which is why the
  stick cannot reach the straight-up the snap cluster can, and the shared pair must not be widened
  to close that gap.
  *Fix shape:* reuse `HeadLook` (`src/Flight/HeadLook.cs`, C21) on the chase camera with
  `PitchFloor = -π/2` instead of building a second controller, feeding it the snap/centre/mouse
  paths only; the snap cluster becomes `CameraController`'s numpad table per `BL-150`'s law once
  that item's rebuild lands.
  *Cross-refs:* `BL-150` (the fixed-view numpad table this supersedes as a mental model), `BL-433`
  (the same F9-F12/zoom cluster's `+`/`−` half), `PLAN-cockpit-view` (⚠ table row 2, C21
  `HeadLook`), `docs/org/cameraViews.md` (the F7/flyby correction), `docs/controls.md` (the
  device split, and `--look=` as its scripted twin).

- `BL-436` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The cockpit view's whole feel is unjudged at the controls
  — one sitting owes seven separate decisions `PLAN-cockpit-view` made without one.** (a)
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
  `PLAN-cockpit-view`'s ⚠ table row 2 retired `docs/org/cameraViews.md`'s old "head fixed in
  Nose" reading in favour of "head-look runs, only autohead is gated" — fly Nose and confirm the
  free-look is really there, since the retired reading may have been a live impression rather than
  a misread decompile.
  *Fix shape:* one cockpit sitting across a couple of airframes covers all seven; each is a
  judgement call, not a re-decode.
  *Cross-refs:* `PLAN-cockpit-view` (every decision above, by wave: B11, C21, C22, D31), `BL-391`
  (engine level, kept separate from (f)).

- `BL-702` `[Bug]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: feel]` **The chase camera's
  zoom range sits inside the original's rather than on it: the original starts at its closest and
  only zooms out, while ours starts mid-range and zooms both ways.** *Evidence:* reported at the
  controls on the build that landed `BL-433`, which gave the axis its decoded keybind (numpad plus
  and minus) but took the clamp ends from CSVM's existing chase distance rather than from the
  original's. The axis works; the two ends and the rest position are what disagree.
  *Fix shape:* decode the chase camera's authored default distance and its clamp pair, then set the
  rest pose to the near end so the only travel available is outward. *⚠ Traps:* do not derive the
  near end from the current default by subtracting the observed travel. The report says the
  original's default IS the near end, so the near end is a datum to be read and the default follows
  from it, not the reverse. *Playtest after fix:* chase view held at both ends of the clamp, and the
  pose the view opens on before any zoom input. *Cross-refs:* `BL-433`'s closing commit
  (`git log --grep=BL-433`), which carries the keybind decode.

## HUD & UI

- `BL-795` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **A sortie screen with the aircraft picked but no map still tells the pilot to pick an aircraft.**
  *Evidence:* `OriginalShell.SortieHint` tests the chapter first and returns "Pick a map, then an
  aircraft" whenever no map is picked, however far the rest of the screen has got, so with seat 0's
  aircraft chosen and every joined seat confirmed the hint names the one thing already done and not
  the press that is missing. The wording predates `BL-749`, which made it reachable in a new place: a
  walk whose FLY gate is unmet now drops the pilots back on this screen, where the hint should read
  "Pick a map, then FLY". *Fix shape:* order `SortieHint`'s branches by what is actually outstanding,
  and name FLY when only the press is left. *⚠ Traps:* the hint is remake-only text on a remake-only
  screen, so there is nothing to decode and nothing to match; keep it one short line, as the other
  arms are. *Cross-refs:* `BL-749`'s closing commit.

- `BL-113` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Compass tape** — `TileOverscan` / `RimGain` / the nearest-tick look remain TUNE
  (north = −Z is now confirmed against the original, 2026-07-30 — do not reopen).
  *Cross-refs:* `PT-121` (the flight that judges the three).

- `BL-181` `[Tuning]` `[Blocked: a shared type scale]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **Marker HUD + scoreboard layout is a provisional pass, not a
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

- `BL-351` `[Feature]` `[M]` `[Next: data]` `[Impact: high]` `[Evidence: data]` **Generalise the targeting HUD: target-cycling keybindings for the
  original's target classes.** Requested 2026-08-14 alongside M4 H22 (which extends the VS
  targeting elements to AI enemy planes but picks the target automatically). The original ships
  several bindings to cycle the tracked target by class: enemies/objectives, allies, and
  non-aircraft (ground/sea vehicles, turrets, zeppelins). Wanted: the same class-cycling on our
  targeting HUD — per pane in splitscreen, reusing the VS/H22 drawing elements unchanged, only
  the selection source generalises. Ground work: pull the original's exact bindings and cycle
  order from its input config/manual before designing ours; wire through the named-action seam,
  which is built: a class-cycle action per class in `InputAction`, authored in `DefaultBindings` and
  rebindable, with the selection source generalising behind it (`CSVM/src/Bindings/`).
  *How you'd know it worked:* in a session with AI planes, a zeppelin and turrets, the target
  key cycles hostile aircraft; the non-aircraft key walks the zeppelin and turrets; each pane
  tracks its own pick. Depends on H22's target-tracking plumbing; `docs/controls.md` gains the
  bindings when it lands.

- `BL-686` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **One target HUD: the stunt marker and the objective marker are the same
  thing shown by two systems.** *Evidence:* the user's call, made while `PLAN-input-rebinding`'s
  `B11` was migrating `FlightController`. A stunt target IS an objective marker, so cycling one is
  the same action as stepping the objective cycle, and the two should not be separate features with
  separate keys and separate drawing. Today they are: `CycleStuntTarget` is its own action on `Tab`
  (`docs/controls.md`) driving the stunt marker, while `TargetNextEnemy` steps the targeting HUD's
  enemy/objective cycle on `T` and `D-pad Up` (tap), and the two draw through different code.
  *Fix shape:* fold the stunt marker into the targeting HUD's objective class so one cycle, one
  marker style and one keybinding cover both, and the stunt modes stop needing a private marker at
  all. `BL-400`'s curated non-aircraft list and `BL-351`'s class-cycling generalisation are the
  neighbouring pieces of the same HUD, so whoever takes this should read all three together.
  ⚠ **The one thing that does not unify: stunt markers are per player, never shared.** Each pilot
  has their own set, so in splitscreen each pane shows and cycles its own and a pick made in one
  pane must not move another's. Folding the marker into the targeting HUD's objective class must
  therefore keep the per-pane selection the targeting HUD already has (`TargetSelection.Current` is
  per player), and must not promote the stunt marker to a session-wide objective the way a mission
  objective is shared. A unification that makes every pane agree has broken the feature.
  ⚠ Trap: the original's own `targets.zrd` cycle is decoded in `docs/org/targeting.md` and is the
  reference for what belongs in a cycle; do not invent a class the original has no equivalent of.
  ⚠ Until this lands, `CycleStuntTarget` shares `D-pad Up` with `TargetNextEnemy` as an interim,
  which is a deliberate two-actions-on-one-control case (see `PLAN-input-rebinding`'s note on
  `ActionMap.Assign` stealing from only the first owner it finds).
  *Cross-refs:* `BL-351`, `BL-400`, `BL-397`, `docs/org/targeting.md`, `docs/controls.md`.

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
  itself must stay visible — it is not a needle overlay). The windshield bullet-hole decals
  (`bullet1`-`bullet5`) share the same parked-state mechanism but are driven by the unrelated
  `cockpit_bulletholes` anim-def family (`PLAN-m3-polish-5` line 453), not this item.
  Neither that family nor either other `PLAYER_1ST_PERSON` def (`muzzle_burst`, `player-1`'s
  `pdpanel4`/`pdpanel6`) targets any node inside `gauges`, and no runtime binds a plane's own
  subtree apart from the crash rig's narrow subset — so nothing animates the panel per frame.


- `BL-706` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The PLANE NAME dialog
  sits off-centre because its pane is drawn at a raw 0,0 and its rows are drawn as though their
  authored coordinates were the screen's.** *Evidence:* reported at the controls over E44's pointer
  sweep, "Plane Construction: The PLANE NAME Dialog should be centered", and settled over
  `PLAN-M5-polish-13`'s closing sortie against `OriginalScreenshots/CustomPlane Name Dialog.png`,
  which puts the panel in the middle of the board. The data is right and the fit is wrong:
  `PN_P_BACKGROUND` is authored at 0,0 over `PX_PlaneNameBackground.Png`, whose extracted art is
  264x177 and so centres on the 800x600 board at 268,211, and every other row in the section carries
  a small coordinate that lands on that centred pane and nowhere else (`PN_E_NAME` 23,40,
  `PN_B_DEFAULT` 27,83, `PN_B_OK` 74,130, `PN_B_CANCEL` 161,130, `extracted/rof/menu_layout.json`).
  Ours draws the pane at the raw authored corner (`AddPane`,
  `CSVM/src/UI/Menu/Original/OriginalHangar.cs:443-449`) and each row at its own, so the whole dialog
  sits against the screen's top-left. *Fix shape:* the section's pane is placed centred when its art
  is smaller than the board, and the section's rows are drawn relative to it. *⚠ Traps:* **do not
  centre the rows by hand-tuned constants**, which would hide the same fault on every other
  sub-screen pane. The centring rule is proved on one case here: `PX_P_BACKGROUND` is 800x600 and
  centres to 0,0 either way, so it agrees with the rule without testing it; check the other sections'
  small panes before generalising. *Cross-refs:* `PLAN-menu-presentations.md` E44 row 7, `BL-769`
  (the same screen's refusal), `BL-759` (the name box on the hub behind it).

- `BL-750` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Instant Action's list
  arrows and scrollbar thumb sit on the page background rather than clear of the list.**
  *Evidence:* reported at the controls over `PLAN-M5-polish-12`'s closing sortie, "Instant Action:
  scrollbar and arrows a bit to the right, not on the background". Every one of them is placed
  flush against the list's right edge: the open dropdown's arrows at `box.X + box.Width`
  (`CSVM/src/UI/Menu/Original/OriginalInstantAction.cs:360-364`), the contents window's at
  `x + width` (`:395-399`), and both thumbs at the same edge (`:837-845`, `:865-873`). *Fix
  shape:* one inset constant applied at all four sites, so the chrome clears the field's border
  instead of straddling it. *⚠ Traps:* the list boxes themselves are authored geometry; move the
  remake chrome, never the box. Check the layout's own widget for an authored arrow position
  before choosing a constant. *Cross-refs:* `BL-707`'s landing (`git log --grep=BL-707`), which
  added the thumbs.

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

- `BL-753` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Plane Construction's
  tab labels ride high enough on their strips to touch the element above.** *Evidence:* reported
  at the controls over `PLAN-M5-polish-12`'s closing sortie, "Plane Construction: move the text of
  the tabs a little bit down so they dont overlap with the element above". The tabs are authored
  strips carrying their label on the plaque (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:769-772`,
  drawn through the paper-button arm at `:1676-1679`), so the label's baseline inside the plaque is
  ours, not the layout's. *Fix shape:* drop the tab label's baseline within its strip. *⚠ Traps:*
  the strip geometry is decoded; move the text inside it, never the strip. Every other paper
  button shares the same draw arm, so a change there has to leave OK, Cancel and the export
  variants where they are. *Cross-refs:* `BL-706` (the same screen's PLANE NAME dialog).

- `BL-754` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Plane Construction's
  CURRENT WEIGHT line stays in the ordinary ink when the build is over its airframe's capacity.**
  *Evidence:* reported at the controls over `PLAN-M5-polish-12`'s closing sortie, "colour Current
  Weight: xxx lbs. red if overweight". The hub draws it in `BoardInk.Dialog` unconditionally
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1333-1339`) while the cash figure two blocks above
  already takes the problems ink once the build outruns the wallet (`:1312-1317`), and Built-in
  colours the same totals line through `TotalsOverweight`
  (`CSVM/src/UI/LaunchMenu.cs:2364`, `CSVM/src/UI/HangarFlow.cs:244-246`). *Fix shape:* the weight
  line takes the same problems ink when `bill.Total.Weight` exceeds `bill.Capacity`, the condition
  `HangarEconomy.Price` already reports as `PurchaseVerdict.Overweight`
  (`CSVM/src/Flight/HangarEconomy.cs:181`). *⚠ Traps:* the ink is the board palette's, not a
  literal colour; use the mark the cash figure uses so the two agree. The pending case before an
  airframe is chosen has no weight and must stay plain. *Cross-refs:* `BL-655`'s landing
  (`git log --grep=BL-655`), which added the cash note.

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

- `BL-757` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Switching the menu
  presentation shows Godot's procedural sky between the old menu and the new one.** *Evidence:*
  reported at the controls over `PLAN-M5-polish-13`'s closing sortie, "On switching from built-in to
  Original and vice versa i can see the godot skybox too. Can we replace the skybox with black?".
  `ApplyOptions` deactivates the running presentation, re-selects and shows the new one over three
  statements (`CSVM/src/Session/Launcher.cs:1326-1330`), and the persistent `WorldEnvironment`
  behind the menu is still on `BGMode.Sky` with its `ProceduralSkyMaterial` (`:1093-1098`), so any
  frame with no opaque menu backdrop over it draws that sky. *Fix shape:* the environment is blacked
  while the menu owns the screen, the way `BlankAndQuit` blacks it on the way out
  (`CSVM/src/Session/Launcher.cs:1529-1538`), and the mission sky is put back when a session starts.
  *⚠ Traps:* the environment is process-lifetime and feeds the glossy water's specular
  (`UseMissionSky`, `:1129-1134`), so a blank has to be undone on a launch rather than left standing.
  ⚠ **No headless run may pay for it**, the rule `BlankAndQuit` already carries.
  *Cross-refs:* `BL-710`'s landing (`git log --grep=BL-710`), the same sky at the quit exits.

- `BL-758` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **A name box draws a
  trailing underscore in the text ink where the layout authors a red cursor.** *Evidence:* reported
  at the controls over `PLAN-M5-polish-13`'s closing sortie, "Plane Construction: Text Input cursor
  is a red blinking line in the original". Both edit rows carry `CursorColor=0xFFEF0010` as their own
  field: `PN_E_NAME` on the PLANE NAME screen and `PX_E_NAME` on the construction hub
  (`extracted/rof/menu_layout.json`), and the editbox type's field table names that column, so the
  colour is decoded rather than guessed. Ours draws the label plus `"_"` while focused, in
  `BoardInk.Row` and without a blink (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1663-1667`).
  *Fix shape:* a caret drawn from the row's own `CursorColor` as a filled bar at the text's end, on a
  fixed blink period. *⚠ Traps:* one draw arm serves every `E` row, so read the colour off the widget
  instead of writing a literal; the same arm already takes its border from `FrameColor`. A caret is
  not part of the text, so it must not shift the string or count against `MaxChars`, which is 16 on
  both rows. *Cross-refs:* `BL-711`'s landing (`git log --grep=BL-711`), the same box's refusal.

- `BL-759` `[Feature]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The Plane
  Construction hub draws the plane's name as a static line where the layout authors an edit box.**
  *Evidence:* reported at the controls over `PLAN-M5-polish-13`'s closing sortie, "The PLANE NAME is
  still editable in the original. Textbox where the name is". The hub section authors `PX_E_NAME`, an
  editbox with its own frame and cursor colours and `MaxChars=16`
  (`extracted/rof/menu_layout.json`), and ours writes the scratch plane's name as a plain `BoardLine`
  at constants of ours instead (`HubNameX`/`HubNameY`,
  `CSVM/src/UI/Menu/Original/OriginalHangar.cs:97-99`, `:1301-1302`), so the only place a name can be
  typed is the PLANE NAME screen the build opens with. *Fix shape:* the hub's name becomes a text
  field row over `PX_E_NAME`, writing `HangarFeature`'s scratch name. *⚠ Traps:* the authored row
  carries no width, which is why ours is a constant; take the width from the box art or the name
  title's row rather than inventing one. A rename must not create a second plane or re-raise the
  defaults ask. *Cross-refs:* `BL-758` (the caret the field would draw), `BL-706` (the screen that
  takes the name first).

- `BL-760` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **PLANE COST and CURRENT
  WEIGHT do not follow the row the cursor is on in an open list.** *Evidence:* reported at the
  controls over `PLAN-M5-polish-13`'s closing sortie, "Weight and Plane Cost is previewed on hover of
  combo box rows (updates on hover)". The right page already previews: the description box and the
  name line read the focused list row (`FocusedItem` and `FocusedAirframe`,
  `CSVM/src/UI/Menu/Original/OriginalHangar.cs:1386-1398`, `:1505-1513`), and so does the blueprint.
  The hub's two figures do not, both reading `hangar.Bill`, the committed scratch's price
  (`:1303-1306`, `:1333-1339`). *Fix shape:* the chrome prices the build as it would stand with the
  focused row taken, which every list already has as a `CostWith…` delegate the unaffordable mark
  uses (`:332-346`). *⚠ Traps:* a preview must not commit, and leaving the list without a pick has to
  put both figures back. The cash note takes the problems ink off the same bill, so a previewed
  figure moves that colour too; decide whether the mark previews with it.
  *Cross-refs:* `BL-754` (the same weight line's ink).

- `BL-761` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: footage]` **The tab pages'
  description box carries the figures alone where the original follows them with a DESCRIPTION
  paragraph.** *Evidence:* reported at the controls over `PLAN-M5-polish-13`'s closing sortie,
  "Description text missing", and `OriginalScreenshots/CustomPlane Engine.png` and
  `CustomPlane Guns.png` both show COST, WEIGHT and the rest, then a DESCRIPTION heading over the
  component's own prose. Ours builds the box from the figures and stops
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1431-1488`). The prose is in the shipped table: the
  engines run 3270 to 3305, six ids per family in engine order, with 3307 as No Information
  Available, and the guns run 3330 to 3335 in gun order, 3335 being No Gun
  (`extracted/rof/ui_strings.json`). *Fix shape:* the description list gains the heading and the
  component's own string, wrapped inside the box the way the figures already are. *⚠ Traps:* the id
  blocks are ordered per component and are not one contiguous run across kinds, so index them rather
  than deriving a single base. The box is the authored `S` widget with its own back and border, so a
  longer body scrolls or wraps inside it and never grows it. Armour, hardpoints and paint show no
  such prose in the stills; do not invent it. *Cross-refs:* `BL-653` (the same screen's TOP SPEED and
  OFFENSE ratings).

- `BL-762` `[Bug]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **A decal list opens as
  a one-wide column where the original opens a five-across, two-row grid of tiles.** *Evidence:*
  reported at the controls over `PLAN-M5-polish-13`'s closing sortie, "Decal select has two rows with
  5 decals each", and `OriginalScreenshots/CustomPlane Decal Select.png` shows exactly that under the
  three preview boxes, with a scrollbar down its right edge. `PT_D_DECALS0` to `2` author
  `ItemHeight=73`, `TotalDisplayed=2` and `Width=87` (`extracted/rof/menu_layout.json`, the `Paint`
  section), which is two rows of the closed box's own width, and the fifty tiles come off the
  section's `PT_P_DECALS` strip. Our open list stacks one tile and its name per row inside that width
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1788-1794`, the rows built at `:818-838`).
  *Fix shape:* the decal lists lay their visible window out across five columns and two rows over the
  page, keeping the tile art already drawn. *⚠ Traps:* the grid is wider than the closed box it hangs
  from, so the hit rectangles and the arrows move with it, and `ComposeOpenList` derives its panel
  from the rows' own extents. The other paint lists are colour swatches in a single column, so the
  change belongs to the decal case alone. *Cross-refs:* `BL-659`'s landing
  (`git log --grep=BL-659`), the aid that can open these lists.

- `BL-763` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: footage]` **The wallet-free
  totals page commits with Purchase Now where the original's reads Export.** *Evidence:* reported at
  the controls over `PLAN-M5-polish-13`'s closing sortie, "Ready to Export shows Export instead of
  Buy button in instant action", and `OriginalScreenshots/CustomPlane Export.png` is that page on the
  wallet-free path (its tab bar reads READY TO EXPORT and CANCEL EXPORT) with Export on the commit
  button. Ours draws the button from `PUR_B_PURCHASE`'s own resource, `IDS_PUR_B_PURCHASE`,
  "Purchase Now", on both paths (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:743-748`).
  *Fix shape:* the wallet-free path swaps the commit's label the way it already swaps the two tab-bar
  strips (`ExportVariant`, `:777-795`). *⚠ Traps:* the shipped table has no export label for this
  button, only `IDS_PS_B_EXPORT`, "Export", authored for the inventory's own; reuse that id rather
  than writing a literal. The campaign path keeps Purchase Now. *Cross-refs:* `BL-764` (the same
  path's inventory buttons), `BL-651`'s landing (`git log --grep=BL-651`), which drew the export
  boundary.

- `BL-764` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **The wallet-free
  inventory keeps a dead Export button and calls the removal a sale.** *Evidence:* reported at the
  controls over `PLAN-M5-polish-13`'s closing sortie, "Should not have a export option (its already
  deactivated) coming from instant action and should be delete instead of sell for instant action".
  The inventory builds both buttons on both paths and disables Export
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:813-815`), where Instant Action has nothing to export
  to and a plane that cost nothing is not worth a sale. *Fix shape:* on the wallet-free path the
  Export row is not built at all and Sell is labelled Delete. *⚠ Traps:* the campaign path keeps both
  buttons and both words. There is no shipped delete label for this button (the table has
  `IDS_PS_B_SELL`, "Sell", and `IDS_PS_B_EXPORT`, "Export"), so the word is remake-only, as the tab
  bar's export variants are. Dropping a row moves the focus order the pad and the aid both walk.
  *Cross-refs:* `BL-766` (the confirm the same button raises), `BL-763`.

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
  `BL-764` (the same screen's buttons).

- `BL-766` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **The wallet-free
  inventory asks the campaign's sale question, which prices a plane that cost nothing.** *Evidence:*
  reported at the controls over `PLAN-M5-polish-13`'s closing sortie, "Confirm dialog should ask 'Are
  you sure you want to delete it?'". `AskToSell` formats langui 700, "Your %1!s! is worth
  <B>$%2!d!<b>.  Are you sure you want to sell it?", on both paths
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1058-1071`). *Fix shape:* the wallet-free path asks
  its own question in the same two-button query box. *⚠ Traps:* the shipped table carries no delete
  question for a plane (201 is the player and 210 the saved game), so the wording is remake-only.
  Keep the box's shape: the sell confirm is the `0x4` two-button query with the `?` icon, which
  `BL-744` settled. *Cross-refs:* `BL-764` (the button that raises it), `BL-744`'s landing
  (`git log --grep=BL-744`), the icon rule.

- `BL-767` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **A message box's rollover
  frame is drawn for the pad focus, so a button never returns to its normal frame and the pointer's
  own hover barely reads.** *Evidence:* reported at the controls over `PLAN-M5-polish-13`'s closing
  sortie, "Hover for buttons not really visible (removes the border), does not return to default if
  not hovered". The dialog draws each answer at `PlaqueFrame(4, focused, held)` with `focused` being
  the row index the pad and keyboard carry (`CSVM/src/UI/Menu/Original/OriginalCampaign.cs:396-409`),
  and a dialog always has one focused answer, so frame 2 stands whatever the pointer does. The
  reference the shared drawing cites shows OK on its normal frame with the pointer elsewhere
  (`CSVM/src/UI/CampaignBoards.cs:370-375`). *Fix shape:* the strip frame follows the pointer while
  one is present, and the pad focus is shown some other way, so an unhovered default answer sits on
  frame 1. *⚠ Traps:* a pad-only player still has to see which answer is armed, so the pad focus
  cannot simply stop drawing. Whether frame 2 of `MB_B_Buttons.Png` is the rollover at all is worth
  reading off the strip before its look is called wrong. *Cross-refs:* `BL-744`'s landing
  (`git log --grep=BL-744`), the same box's icon.

- `BL-769` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: footage]` **The PLANE NAME
  screen's empty-name refusal stands as a line under the buttons instead of being raised as a
  message box when OK is pressed.** *Evidence:* reported at the controls over `PLAN-M5-polish-13`'s
  closing sortie, "no name should not be text below but a dialog on ok", with
  `OriginalScreenshots/CustomPlane Name Dialog No Name.png` showing the original's own message box,
  its warning icon and a single OK, raised over the screen. Ours writes langui 203 as a `BoardLine`
  under the panel's buttons whenever the field is empty, before OK is pressed at all
  (`CSVM/src/UI/Menu/Original/OriginalHangar.cs:1276-1283`), and draws OK disabled while it is empty
  (`:734`), so the press the refusal answers cannot happen. *Fix shape:* OK stays live on an empty
  field and raises the one-button box carrying string 203; the standing line goes. *⚠ Traps:* the
  icon is the message box rule's, not a choice made here, so take the frame the way `BL-744`'s
  landing settled it. The screen is itself a panel, so the raised box is a second overlay over the
  first and has to be drawn after it. *Cross-refs:* `BL-706` (the same dialog's placement),
  `BL-744`'s landing (`git log --grep=BL-744`).

- `BL-779` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **Original's
  credits screen draws ABOUT disabled, because the box it raises wants a widget set the shared
  messagebox chrome does not compose.**
  *Evidence:* `MESSAGEBOX.SCRIPT:11-25` picks the box's widget prefix off two globals, `WR` taking
  `mp`, else a set `XR` taking `ma`, else `mb`. `CREDITS.SCRIPT:52-61` sets `XR = 1`, so ABOUT's box
  is the `ma_` set: `MA_P_BACKGROUND` over `CR_AboutMessageBox.png` (505x416), `MA_T_MESSAGE` at
  96,70 by 375x240 and `MA_B_CENTER` at 224,371 (`extracted/rof/menu_layout.json`).
  `CampaignBoards.Dialog` composes the `mb_` set alone (`CSVM/src/UI/CampaignBoards.cs:394-419`) off
  the constants `DialogX = 195` and `DialogY = 150` (`:83-84`), and `DialogSlot` is hard-wired the
  same way (`:424-435`), so the shell draws ABOUT as a disabled row and the manifest records the
  four `MA_` rows as not drawn.
  *Fix shape:* a variant on the dialog composer carrying the widget prefix and the box's origin,
  defaulting to today's `mb_` set so every campaign caller is unchanged, then that variant through
  `OriginalDialog`, `RaiseDialog`, `DialogRows` and `ComposeDialog`, and ABOUT enabled with
  `DialogIcon.Death`. The origin is the script's own centring, `(800 - w) / 2` by `(600 - h) / 2`
  over the chosen background, which is exactly what 195,150 already is for `MB_Background.png`'s
  410x300; for the `ma_` box it is 147,92.
  *⚠ Traps:* **the icon does not take the prefix.** `MESSAGEBOX.SCRIPT:32-34` initialises
  `mb_p_icon` whatever the variant and only the `mp` box moves it (`:69-72`), so the `ma_` box draws
  the same strip at the same authored place, frame 2. There is no `MA_P_ICON` row and adding one
  would be an invention. The words are already decoded, so do not re-run that: `uiData` 2108 at
  `0x0040a2cb` is langui 1301 over the product id. That row carries a `[COUR9]` tag `UiStrings`
  strips and a `<B>`/`<b>` pair around its one placeholder that nothing strips centrally, though
  `OriginalHangar.cs:1077` does it inline for one question string. Without `BL-780` the placeholder
  reads `???`, which is the original's own answer on a machine with no such key.
  *Playtest after fix:* `./RunGame.ps1 --presentation=original --menu=credits`, press ABOUT, and
  check the box is the tall parchment rather than the small one, with the skull icon and one OK.
  *Cross-refs:* `docs/org/menu-inventory.md` (the Credits row, which holds the decode), `BL-780`,
  `BL-744`'s landing (`git log --grep=BL-744`, the icon rule), `BL-775`'s landing
  (`git log --grep=BL-775`).

- `BL-780` `[Feature]` `[Blocked: BL-779]` `[S]` `[Next: decide]` `[Impact: low]`
  `[Evidence: decoded]` **The About box's product identification number would read `???`, because
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
  *Cross-refs:* `BL-779` (the box this text stands in), `docs/org/menu-inventory.md`.

- `BL-781` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The credits
  screen's right-button easter egg cannot be revealed, because the seat seam carries no
  right-button signal.**
  *Evidence:* `CREDITS.SCRIPT:25-39` activates a text widget while the right button is held inside
  the authored rectangle x 287..353 by y 313..333, and deactivates it on release. The widget is
  created at 288,308 in colour `0xffffff00` (`:14-19`) and its line is built once at create, on the
  `mail(20001)` the script sends itself, by shifting each character of the obfuscated literal
  `"xl#ghy#ohdg=#ulfk#hl}hqkrhihu"` down by three (`:63-72`). `MenuPointer` carries `Pressed`,
  `Clicked` and `Wheel`, all of them the left button (`CSVM/src/UI/Menu/MenuCommands.cs:31`), and
  `PointerSeat` reads one injected `_pressed` func (`PointerSeat.cs:44-59`).
  *Fix shape:* a right-button field on `MenuPointer` fed by a second injected read in `PointerSeat`,
  then a held-inside-the-rectangle test on the credits screen that activates the line.
  *⚠ Traps:* decode the string at runtime as the script does; a plain-text copy in our source is the
  same string with the joke removed. It is a HOLD and not a click, so a click edge is the wrong
  signal. The five test helpers that construct a `MenuPointer` move with the record
  (`MenuHangarSuites.cs:889`, `MenuInstantActionSuites.cs:1072`, `MenuLaunchReturnSuites.cs:518`,
  `MenuOriginalCampaignSuites.cs:771`, `MenuOriginalSuites.cs:635`). ⚠ Do not route this as a
  general secondary-click command; `MenuCommands` is device-neutral by contract and no other screen
  reads a right button.
  *Playtest after fix:* `./RunGame.ps1 --presentation=original --menu=credits`, hold the right
  button inside that rectangle, and check a line appears while held and goes on release.
  *Cross-refs:* `docs/org/menu-inventory.md` (the Credits row), `BL-775`'s landing
  (`git log --grep=BL-775`).

- `BL-783` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Built-in's Options screen shows none of the four display
  settings, so the monitor, the resolution, the display mode and V-Sync are settable in the Original
  presentation alone.** *Evidence:* the VIDEO page landed for Original
  (`CSVM/src/UI/Menu/Original/OriginalVideo.cs`, `PF_B_VIDEO` live) and widened the shared store, so
  `OptionsDef`, `OptionsStore`'s validation and `OptionsApplyExit` all carry the four settings beside
  the three vocabulary words. Built-in reads them into `_monitorChoice`, `_resolutionChoice`,
  `_displayModeChoice` and `_vsyncChoice` purely to hand them back untouched, its own comment saying
  the screen shows none of them (`CSVM/src/UI/LaunchMenu.cs:189-191,1557-1559`). A player who never
  leaves Built-in cannot pick the screen the game opens on.
  *Fix shape:* four rows over the same four settings and the same resolvers the VIDEO page reads
  through (`CSVM/src/Utils/MonitorSetting.cs`, `ResolutionSetting.cs`, `DisplayModeSetting.cs`,
  `VSyncSetting.cs`), stepped in Built-in's own convention and applied through the existing exit.
  *⚠ Traps:* the resolution words are enumerated from the chosen screen rather than shipped as a
  list, so a monitor change has to re-enumerate them exactly as the VIDEO page's row does, and a
  fixed list would offer a size the screen cannot hold. The two forgiving reads are the feature and
  not error paths to reinvent: a saved monitor index no screen answers to shows as the screen the
  window already stands on, and a saved size the screen no longer offers shows as the project
  default. Enhanced Graphics is not a fifth row here, Built-in's graphics stepper already being it.
  *Cross-refs:* `git log --grep=BL-768` (what the VIDEO page settled, and why each row sits where it
  does), `BL-782` (the same gap for the audio levels), `docs/org/menu-inventory.md`'s Video row.

- `BL-784` `[Feature]` `[L]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The Game Options page drops the original's own Default View and
  Auto Head Turn rows, and neither presentation offers either setting.** *Evidence:* the section
  authors three option rows, Difficulty (`GO_D_DIFFICULTY`), Default View (`GO_T_VIEWTITLE` and
  `GO_T_VIEWDESC` over the `GO_D_VIEW` dropdown) and Auto Head Turn (`GO_T_HEADTITLE` and
  `GO_T_HEADDESC` over the `GO_B_HEADTURN` checkbox). The port's table carries Difficulty and the
  remake-only Menu row alone, while `ReadGameOptionsPage` already reads both dropped rows' widgets
  for the page's row shape (`CSVM/src/UI/Menu/Original/OriginalGameOptions.cs`), so the geometry is
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
  *Cross-refs:* `BL-436` (the cockpit sitting that judges autohead), `BL-782` and `BL-783` (the same
  both-presentations gap for the audio and display settings), `docs/org/menu-inventory.md`,
  `docs/org/cameraViews.md`.

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
`PLAN-splitscreen-polish` (2026-08-15,
complete — the chrome playtest F52/`BL-126` closed it out). New splitscreen findings mint here as
usual.

- `BL-788` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: trace]` **Seat 0's whole frame drives another pilot's campaign flight check.**
  *Evidence:* `OriginalShell.StepSeat` routes seat 0 through `SeatZeroFrame`, which reduces the frame
  to its pointer only on the per-seat aircraft screen. On `CampaignScreen.FlightCheck` the reduction
  does not apply, so while the check of the seat `_campaign.Field.Current` names is showing, seat 0's
  cursor, Accept and Back reach it as well as that seat's own device: seat 0 can change a guest's
  ammunition, aircraft and readiness. The later seat's own arm is already correct, `own` being true
  exactly when the field stands on its index. *Fix shape:* widen `SeatZeroFrame`'s screen test to the
  flight check, on the same rule the per-seat screen takes, the seat's identity rather than the
  device kind. *⚠ Traps:* keep the pointer, as the per-seat screen does; the mouse rides seat 0's
  source and is the one device a guest with no pad can use. Seat 0 must still drive its OWN check,
  which is what the field's index answers. *Cross-refs:* `BL-747`'s closing commit, which found this
  as the same shape of defect on the neighbouring screen and left it out of scope.
- `BL-380` `[Bug]` `[Blocked: per-instance fog shader uniforms]` `[L]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Fog-zone selection stays
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

- `BL-389` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Splitscreen weapon mix needs a retune: rockets too quiet, guns too loud,
  especially four guns firing at once.** Found at the `BL-126` chrome playtest (F52,
  2026-08-15) — `FlightAudio.MixGain`/`Projectile.cs`'s pool `MixGain` (the `1/sqrt(N)` equal-power
  attenuation D31/D32 landed) reads right in isolation but the per-weapon balance under it does
  not: a 4-player Dogfight with simultaneous gunfire is too loud relative to rocket explosions,
  which read as too quiet against it. *Look for:* rocket vs. gun relative level across 2P/4P,
  specifically four guns firing together. *Fix shape:* a judgement call at the controls on the
  per-def volume terms feeding `Projectile.cs`'s `def.Volume * 0.2f * MixGain * distanceGain`
  (line ~2238) — not the `1/sqrt(N)` splitscreen term itself, which is confirmed correct.

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
  swap against splitscreen's `MixGain` term is unjudged at the controls** — `BL-391`'s "own-ship
  engine loop too loud" finding predates the cockpit swap and never isolated the `_cp` def
  specifically. (c) **Today's hiding mechanism is node visibility on a shared plane node, not a
  per-viewport render flag**: `CockpitVisibility` hides the OWN rig's `healthy` body node, so a
  pilot sitting in the cockpit hides THAT AIRCRAFT'S body in every pane that can see it, not just
  their own — a cross-pane effect unjudged at `N > 1`.
  *Fix shape:* isolate the draw-count growth's split between the cockpit subtree and the rest of a
  4P rig; a splitscreen listen for the cockpit-swap/`MixGain` interaction; confirm or fix the
  cross-pane body-hide visually at the controls with 2+ cockpit-view pilots in the same session.
  *Cross-refs:* `PLAN-cockpit-view` B11 ("Splitscreen posture"), `BL-391` (base engine level,
  the audio half of (b)), `BL-389` (splitscreen weapon mix, same playtest family).

## Missions, modes & campaign

- `BL-501` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: data]` `[CM02]` **Nothing exercises avoid-crash probing between aircraft flying one net in
  formation.** *Evidence:* flagged by G77, which fixed the branch draw that split CM02's three
  bombers and then measured them holding 82 m to 219 m apart on one route. That suite builds its
  world with collision off, so the probing three aircraft at roughly a hundred metres would do to
  each other is never run, and that spacing is exactly the geometry that can arm it. *Fix shape:*
  drive the same formation in a collision world and see whether avoid-crash promotes, and if it
  does, whether it takes an aircraft off its route. *⚠ Traps:* do not widen the formation to quiet
  a probe. The separation is what the net and the cross-track carry produce from authored data, and
  a formation that holds only because its members are far apart is not the one the original flies.
  *Cross-refs:* `PLAN-M5-polish` G77, whose `campaign-bomber-formation` suite is the harness to
  extend.

- `BL-502` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: data]` **`SET_AI_NET` and `SET_AI_TEAM` reach no zeppelin.** *Evidence:* found by
  G80, which wired both clauses for roster-spawned aircraft and could not carry the same lookup to
  airships. Six clauses across four missions name one: `blackswanzep` (C1C/M01), `blackhatzep`
  (C4/M05), `piratezep` (C5/M04) and `cargozep2`/`cargozep3` (C2/M05 and C4/M05). None is in CM02,
  so no mission the player is currently trying to finish depends on this. `ZeppelinMotion` holds its
  net follower and `LiveZeppelin.Team` read-only, so this is a change to `ZeppelinRuntime` rather
  than to the director's lookup. Those names surface through the `Gap` line today, so a mission
  hitting this says so. *Fix shape:* give `ZeppelinRuntime` the same two writes the aircraft arm
  got, re-seating the follower from the airship's current position rather than restarting its route.
  *⚠ Traps:* a zeppelin is not a vehicle in the original and does not run the nose-aligned edge pick
  (`PLAN-M5-polish` G77), so a re-seat here keeps the nearest-node rule and must not inherit the
  aircraft path's heading argument. The record's own team fans across the whole airship including
  its guns, so a script-side team write has to fan the same way or half the hull keeps the old side.
  *Cross-refs:* `PLAN-M5-polish` G80, which landed the aircraft arm.

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

- `BL-314` `[Feature]` `[Blocked: PT-45]` `[L]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **Race countdown — a rolling start on rails before the run clock
  opens.** The abreast starting grid landed 2026-08-08 (`StartGrid`), so every pilot in a splitscreen
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

- `BL-299` `[Research]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` **Decode `net.zrd.json` as the multiplayer spawn table → the retail MP1–MP3 maps for
  Dogfight.** 45 files, one flat group each, node counts quantised by mission type (MP1→80,
  MP2/MP3→48, campaign→8), 23 distinct payloads shared across files — shape and distribution say
  *spawn table*, not patrol route (`PLAN-M4-ai` survey; its "do not build patrol on it"
  warning stands). Now there is a consumer to validate a decode against: Dogfight (`--vs`) plays
  the IA1 `dogfight_ace` list today; a confirmed spawn decode gives it the maps the original
  authored for exactly this mode. MP worlds already load (`--mission=MP1`); only their spawns fall
  back today (`SpawnPicker` warns).

- `BL-301` `[Tuning]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Dogfight (VS mode) tuning** — every deliberate v1 deferral, to be re-judged from
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

- `BL-256` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **Stunt screenshot feature, triggered off `DzRadius` — much later, by user decision
  (2026-08-04).** `DzRadius` (15 m, user-hand-tuned) is settled
  as the **marker-centre radius**: scoring crosses the authored `dzpathN` gate pair
  (the archived development log, 2026-08-01 entry "M3 Wave C C9"), and the constant's remaining roles are the `dzN`
  marker centre and, eventually, the trigger for a stunt screenshot feature. No design beyond
  this sentence exists yet — recorded so the constant's purpose and the feature intent survive.
  The screenshot latch should also play `snd_dangerzone_camera` (`dangerzone_camera.wav`, a
  data-orphan SFX named by no `SOUND_GROUPS` entry and no world data; the user confirms it is
  the automatic-screenshot sting, not a zone-cleared cue — formerly `BL-090` item 5, closed).
  ⚠ Do not retune or delete `DzRadius` as dead code — it is reserved, and the 15 m is the user's.

- `BL-446` `[Feature]` `[L]` `[Next: code]` `[Impact: high]` `[Evidence: data]` **The MPG movie cinemas do not play.** *Evidence:* Decision 2 of
  `PLAN-M5-campaign` put them out of scope for the campaign milestone: plain MPG playback
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
  *Cross-refs:* `PLAN-M5-campaign` Decision 2, which filed it; `docs/formats/cinemas.md`.

- `BL-463` `[Feature]` `[L]` `[Next: decode]` `[Impact: low]` `[Evidence: spec]` **The cabin ships without Change Memento.** *Evidence:* Decision 3 of
  `PLAN-M5-campaign` deferred it: the function is cosmetic and rests on the undecoded
  snapshot flow, so the cabin's other rows shipped without it rather than waiting.
  *Cross-refs:* `BL-256` is the adjacent snapshot work; `PLAN-M5-campaign` Decision 3.

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

- `BL-558` `[Research]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: decoded]` **A damaged AI flies a full evasive maneuver where the original may only set a
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

- `BL-653` `[Research]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: footage]` **The plane selection screen's TOP SPEED and OFFENSE ratings have no decoded
  formula, and ship as a stand-in.** *Evidence:* `PS_T_TOPSPEEDP`/`PS_T_OFFENSEP` and their wingman
  twins are four text widgets fed one of langui 501-505 (`Poor`, `Fair`, `Average`, `Good`,
  `Excellent`). Two of the four are decoded and running: `HangarEconomy.Bill` computes
  `AgilityStars` from `(agility - 1) / 4` and `ArmourStars` from
  `(armour + units*5 - 1) / 0x49` (`CSVM/src/Flight/HangarEconomy.cs:173-176`). No such reading
  exists for speed or offense, so the screen derives them from engine power and from gun calibre
  plus hardpoint count instead, which matches the reference screenshots on the airframes visible
  there and is otherwise unevidenced. *What to settle:* which engine field the original rates speed
  from and what it counts as offense, then whether the ratings are per airframe or per built plane.
  *⚠ Traps:* do not settle it by eye against `OriginalScreenshots/Campaign Flight Check Change
  Plane.png` alone. Two airframes reading `Average` is consistent with many formulas, and a
  stand-in that happens to match the four sampled aircraft is exactly what is already there.
  *Cross-refs:* the plane selection screen that draws them.

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
  (`docs/verification.md` DIAG-25), and a ladder completing with no hatch shot is authored
  behaviour once the hull dies, since `all_gmzep_gasbags` demolishes every section. What is left to
  answer is the run's own shape: the Gemini alive at the end with five engines gone and the ladder
  complete. Do not tighten the objective count; `gemini-gasbag-bays` asserts the authored six.
  *Cross-refs:* `BL-694`'s landing commit, `BL-639`,
  [`docs/formats/mission-entities.md`](docs/formats/mission-entities.md) "A gasbag section owns its
  bays and its engines".

- `BL-699` `[Perf]` `[L]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Every AI wave spawn hitches at the controls, and no test measures the
  launch frame.** *Evidence (at the controls, and measured on one mission):* the author feels a
  hitch on every wave spawn in every mission, not only CM18's generator launches. The measured
  case is `BL-641`'s remainder (`PLAN-M5-polish-8` B14): with the crash rig
  deferred behind the launch, a CM18 generator launch still costs 68 to 141 ms against a 40 ms
  threshold, paired A/B under `--det` at 1P and 4P. Flown since inside the docking film itself
  (`PT-118` (d), closed), the five credited launches read with no stall at all, so five spawns
  4 s apart behind a film camera do not show what ten from load do. What is left on the launch frame is the model
  build and `FlightController.Bind`, 50 to 90 ms together, and neither moves behind the frame that
  puts the aeroplane in the world without the aeroplane arriving late. The deferred frames carry one
  `AnimRuntime.PrewarmEmitters` call over about 194 emitters at 15 to 103 ms, whose
  `material_create` term (194 `ShaderMaterial` and `MultiMesh` builds in `Effects/EmitterRenderer.cs`)
  is the PERF-22-shaped follow-up.
  *Fix shape:* two halves. (1) A test first: a suite that spawns an AI aircraft mid-flight through
  the roster's own wave path and asserts the launch frame against the hitch threshold, so the
  hitch is a red bar and not a feel report; `ai-crash-rig-deferral` spawns through the assembler
  and measures nothing. (2) Then build the assembly AHEAD of the launch, off the generator's and
  the roster wave's own authored cycle, which is a change to `AiGeneratorRuntime`'s launch
  declaration and to spawn-index allocation rather than to the assembler. The crash-rig queue
  (`CrashRigQueue`, `FlightRoster.PumpDeferredCrashRigs`) is the pattern for work that can trail
  the launch; the model and bind cannot trail it.
  *⚠ Traps:* the aeroplanes a session builds BEFORE its first frame keep their rigs built in
  place, and deferring them moved the two `--ai=` goldens; keep that rule. The sim clock lags wall
  time on physics-bound late missions, so compare a hitch in sim frames under `--det` and read the
  hitch lines before blaming a spawn (`docs/verification.md` PERF-12/13/21). `--det`'s numbers are
  with nobody at the controls; the author's feel report is the acceptance.
  *Cross-refs:* `BL-434` (the per-viewport splitscreen cost the same pass profiled), `BL-657`
  (CM18's generator launching at the wrong time, which is where the measured case is flown).

- `BL-728` `[Bug]` `[M]` `[Next: data]` `[Impact: high]` `[Evidence: feel]` **AI aircraft rarely or never evade
  under fire.** *Evidence:* reported at the controls as "recheck evasive maneuvers", clarified as
  "AI never or rarely evades". This is the opposite reading from `BL-558`, which found a Fury
  entering `scissors` on its first 40-calibre hit and asks whether the original evades that early
  at all. Both can be true if the evade fires on the steady-hand roll, which a high-rating pilot
  passes, and so is rare on the pilots the campaign fields. *Fix shape:* fly one Instant Action
  sortie against a low- and a high-rating pilot with the AI trace on, count `NotifyDamage` calls
  against `EvasiveManeuver` entries, and read the steady-hand test's inputs
  (`docs/org/aiControlLaw.md`) for the ratings the missions author. Then settle `BL-558` and this
  together. *⚠ Traps:* the report is a feel over a whole sitting on campaign missions; do not tune
  the roll's threshold before the count says which way it errs. *Cross-refs:* `BL-558`,
  `docs/org/aiControlLaw.md`.

- `BL-731` `[Research]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: data]` `[CM24]` **Whether Miles flies
  stunts through Manhattan or cruises, and which way round his net runs.** *Evidence:* asked at
  the controls on CM24 (C5/M04): "should miles fly stunts or just cruise through manhattan (wrong
  net direction perhaps)". `M4MilesRun` is a generator's net with one tagged danger-zone node,
  `dzpath34` (`docs/formats/ai-nets.md`, `docs/org/aiPilot.md` on danger-zone nets), the mechanism
  CM13's racers and CM21's Cabbie also use; Miles launches from the Dante's bay as the
  `stihellhound_5_eg0` template. *What to settle:* whether the original consumes a tagged danger
  zone on a generator-launched aircraft's net (the racers and the Cabbie are roster aircraft), and
  the direction the net is walked from the launch node. His objective marker is not part of this:
  the bay launch stamps it onto the aircraft like any roster spawn.
  *Cross-refs:* `docs/formats/mission-entities/enemy-generators.md`.

- `BL-739` `[Bug]` `[S]` `[Next: decode]` `[Impact: high]` `[Evidence: feel]` **A mission that ends inside a
  docking film cuts back to the pilot's own view for the leaving hold, and the fade to black runs
  over that instead of over the film.** *Evidence:* reported at the controls on a successful
  docking: the film's last frame is followed by the cockpit or chase view, and only then does the
  fade start. `CutsceneController.Act`'s handoff ends the presentation when the definition ends
  (`CSVM/src/Session/CutsceneController.cs:746-754`), while `CampaignDirector` holds the world for
  `LeavingHoldS` and `MissionEndFade` ramps over that hold (`git log --grep=BL-727`); nothing keeps
  the film's camera when code 13 landed inside the episode. The decode note on
  `CampaignDirector.Leave` says the original fades over the last frame; which camera that frame is
  on after a film-ending 13 is not read. *Fix shape:* read `FUN_00443090`'s caller for the camera
  the fade runs over after a code 13 raised inside a definition; if it is the film's, keep
  `Presenting` and the episode's camera until `Leave` whenever the result landed during the
  episode. *⚠ Traps:* a loss inside a film and a win in free flight are separate paths; the fade
  case in `CampaignSuites` should assert all three. *Playtest after fix:* CM14, dock with the
  objectives complete. *Cross-refs:* `BL-727`'s closing commit.

- `BL-771` `[Bug]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: feel]` `[CM18]` **A generator
  credited long after load fires its first launch on the same step its hangar door starts opening,
  so that aircraft flies out through a door that is still closed.** *Evidence:* reported at the
  controls on CM18's docking film (`PT-118` (b), closed; the row's four other checks passed).
  Five allied Furies drop from `cargozep1` about 4 s apart, but "the first one
  starts a little early and flies through the still closed door". `GeneratorCycle.Step` is where it
  comes from: an uncredited cycle is `Blocked`, and a blocked step advances `_timer` without ever
  opening the door, so by the time cutscene callback 800 grants capacity minutes into the mission
  the timer is far past `_nextEvent`. On that first unblocked step the door-lead branch sets
  `DoorOpen` and the spawn branch fires inside the same call, spending the whole
  `DoorLeadSeconds` at once, and `AiGeneratorRuntime` only starts the open anim on that same step
  (`PlayDoor`, `CSVM/src/Session/AiGeneratorRuntime.cs:263-267`), so the panels have not moved yet.
  Launches two to five are clean because the spawn resets `_timer` to 0 and leaves the door open.
  *Fix shape:* on a step that opens a door from closed, hold the spawn until the lead has actually
  run (clamp `_timer` to `_nextEvent - DoorLeadSeconds` as `DoorOpen` flips) instead of letting one
  step satisfy both thresholds. *⚠ Traps:* whether the original does the same is not decoded, and
  `FUN_00452850`'s loop order is what settles it: the remake mirrors the decoded order, so this may
  be authentic and the fix a deliberate deviation. Read it before touching the cycle. The
  `generator-callback-credit` suite pins five launches one every 4 s but does not look at the door
  at the first spawn; extend that suite rather than adding one. *Playtest after fix:* CM18
  (`./RunGame.ps1 --campaign=<profile>:17`), the docking beauty shot, the first Fury only.
  *Cross-refs:* `BL-657`'s closing commit (the credit rule and the callback host), `BL-699` (the
  per-launch hitch, which is a separate item and was not seen on these five),
  `docs/formats/mission-entities/enemy-generators.md`.

## Tooling, platform & docs

- `BL-675` `[Research]` `[S]` `[Next: data]` `[Impact: none]` `[Evidence: trace]` **A `--campaign=<profile>:<n>` run launched through `RunProbe.ps1` from an
  agent worktree reported no such profile, though the profile exists.** *Evidence:* a probe run
  answered `--campaign=Gab: no such profile, flying without a mission` from
  `CampaignDirector.TryCreate`, and repeated it against a fresh copy of the same profile. The store
  is `CampaignProfileStore.UserProfiles()`, `user://Profiles/` globalized, and `user://` resolves by
  the project name alone (`project.godot` sets `config/name="CSVM"`, and no custom user dir is set),
  so a worktree should reach the same directory as the main checkout. That directory does hold the
  profile, and a sibling agent's worktree run created a profile there in the same period, so the
  store is reachable from a worktree at least for writing. **The cause is therefore not established
  and the symptom is not reliably reproduced.** *Fix shape:* reproduce deliberately from a worktree
  with nothing else running, print the globalized `user://` path at startup, and compare it against
  the main checkout's. If they differ, the launch is picking up a different project name or user
  dir; if they match, the fault is in the load rather than the path. *⚠ Traps:* do not "fix" this by
  pointing the store at an absolute path; `user://` is what makes the release build's profiles land
  in the right place. A `--campaign=` probe writes mission results back into the profile it names,
  so any repro copies a profile under a new name and deletes the copy afterwards. *Impact:* while it
  stands, an agent cannot take a screenshot deep inside a campaign mission from a probe, so items
  whose verification wants one fall back to an engine suite plus an at-the-controls `PT-` row.
- `BL-677` `[Fidelity]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **CSVM starts a `CALL_ANIMATION` callee inside the caller's own tick; the
  original starts it on the next one.** *Evidence:* `AnimRuntime.Start` advances a new instance at
  t=0 inside the `CallAnimation` dispatch (`CSVM/src/Mech3/AnimRuntime.cs:1763-1775`), so a callee's
  first event runs before the caller's later events in the same sequence. The original does not:
  `FUN_004ed8c0` appends the new instance at the tail of the action list its dispatcher walks
  (`FUN_004d04e0`, the append store at `004d050d`), the walk re-reads `next` every iteration
  (`FUN_004cffa0` at `004cffee`), and no start-guard flag exists, so the callee is reached only on a
  later dispatcher invocation, after the caller's tick has returned. *Consequence:* a callee whose
  first event is `INVALIDATE_ANIMATION` latches before the caller reaches the call that would have
  started the invalidated definition, which cannot happen in the original. *Fix shape:* queue a
  started instance and give it its first advance on the next tick, rather than advancing it in the
  dispatch. *⚠ Traps:* the ordering is load-bearing for definitions that rely on a callee posing
  something before the caller reads it, so this moves scripted motion across every mission and wants
  the golden and campaign suites run before and after. The Spruce Goose chain is NOT an instance of
  this: its branch is chosen by `ACTIVATION_PREREQUISITE` node states, not by a call race, and both
  branches run (`PLAN-M5-polish-9` C22). *Impact:* unknown breadth. No shipped symptom is
  attributed to it yet; it is a decoded divergence looking for its missions.
- `BL-033` `[Cleanup]` `[Blocked: SDL >= 3.4.4]` `[S]` `[Next: decide]` `[Impact: none]` `[Evidence: data]` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**: the bundled
  SDL (3.2.28 up to Godot 4.7.1) hard-freezes the engine when a >255-button DirectInput device
  disconnects — the 8BitDo Ultimate 2 dongle's HID interface is one (`Uint8` loop counter vs
  uncapped dinput `nbuttons`; godot#115667, SDL#14961, fixed by SDL#15304). Check the bundled
  `thirdparty/sdl/joystick/SDL_joystick.c` `SDL_PrivateJoystickForceRecentering` for the `int i`
  fix before removing. Side effects while active: DirectInput-only controllers (non-XInput sticks
  without an SDL HIDAPI driver) are invisible in-game, and, since the var is set by the launch
  scripts and never by the export, a shipped build enumerates DirectInput devices that no dev or
  test run sees. An 8BitDo Ultimate 2 arrives as three joypads there, which is how a device that
  holds a roster position without producing input came to take the seat `AssignPads` fills by
  position; `Pads.LogPads` records the roster so the next one reads off the log rather than being
  inferred. Dropping the var also closes that divergence.

- `BL-584` `[Research]` `[S]` `[Next: data]` `[Impact: none]` `[Evidence: trace]` **`PerfSampleTests.AScopeAllocatesNothing` goes red intermittently and neither named
  mechanism reproduces.** *Evidence:* two sightings, both isolated, both green on every run
  either side. The first was a full `RunTests.ps1` unit stage (`Expected: 0, Actual: 3984` bytes)
  on a tree whose only difference from six green runs was PowerShell and documentation edits. The
  second came from a whole-project `dotnet test` in an agent worktree, 3441 of 3442, and passed 15
  of 15 on an immediate re-run of the same test. ⚠ **The second sighting's evidence was lost, and
  the way it was lost is itself the finding:** neither the byte count nor the TRX was copied out
  before the agent's worktree was removed, so this recurrence adds a rate observation and nothing
  else. **A flake reported by an agent needs its `.scratch/testresults/units.trx` copied out of
  that worktree before `git worktree remove`**, which deletes the whole directory. **Both
  mechanisms this entry used to name are ruled out, so do not re-chase them.** Cross-class interference on a shared
  thread-pool thread cannot charge this assertion: `GC.GetAllocatedBytesForCurrentThread` is
  per-thread by construction, confirmed empirically with sixteen background tasks allocating and
  forcing gen-0 collections across the whole measured window (8 of 8 trials read exactly 0 bytes),
  and `PerfSampleTests` is the only class in `CSVM.Tests` touching `PerfSample`'s ambient statics
  at all, since every production call site is reachable only through Godot runtime code the unit
  stage never loads. A tiered-JIT recompile landing mid-scope is ruled out the same way: warm-up
  counts of 0, 1, 5, 50 and 500 against the 10,000-iteration measured loop all read 0 bytes.
  Roughly forty forced-contention trials produced no failure. *Fix shape:* none until the cause is
  known; both readings remain unexplained rather than explained-and-fixed. **On recurrence,
  capture the binary hash and the concurrent-class list from the TRX**, captured on neither
  sighting so far, and reopen from there. *⚠ Traps:* do not widen the assertion
  to a tolerance; zero allocations is the contract `PerfSample` makes, and `BL-562` needs
  this test able to catch a real regression. A handful of green runs is not evidence at the
  observed rate: at a 1-in-10 base rate, 30 consecutive clean unit stages give about 95%
  confidence the rate has moved and 44 give about 99%. *Cross-refs:* `docs/verification.md` PERF
  rules, `PLAN-fast-verification` C23.

- `BL-617` `[Perf]` `[M]` `[Next: code]` `[Impact: none]` `[Evidence: data]` **`--perf`'s `script_ms` is the same once-a-second worst-frame monitor that
  `physics_ms` turned out to be, so PERF-1's "about 2.2x real" is a symptom rather than a
  calibration.** *Evidence (traced):* `physics_ms` is Godot's `TIME_PHYSICS_PROCESS`, which holds
  the worst step of the last wall second and refreshes about 1 Hz, which is why a window can report
  27.52 ms against a worst frame of 8.33 ms in the same window and why a flown log repeats one
  value byte-for-byte across a second of records (`docs/verification.md` PERF-21, and `BL-562`'s
  closing commit). `TIME_PROCESS` is set from the same block in the same engine pass, and a C1
  window showed `script_ms` equal to `frame_ms` to the digit while another read 212 ms against an
  8.33 ms frame cap. *Fix shape:* bracket the `_Process` pass the way `PhysicsTickCost` brackets
  the physics tick, report a measured `proc_ms` beside it, then rewrite PERF-1 onto what the
  monitor actually is rather than onto a ratio fitted to it. *⚠ Traps:* the 2.2x figure is quoted
  in existing analysis, so anything resting on it needs re-reading once this lands rather than
  silent correction. Keep the raw monitor reported alongside the measured value, since it is what
  older records hold. *Cross-refs:* `BL-562`'s closing commit (the same misreading, found there),
  `CSVM/src/Utils/PhysicsTickCost.cs` (the pattern to copy), `docs/verification.md` PERF-1 and
  PERF-21.
- `BL-772` `[Tooling]` `[M]` `[Next: code]` `[Impact: none]` `[Evidence: trace]` **The empty-stage
  rig has no patrol net, and a `--ai=` plane never takes a net's volumes, so patrol and the
  netted-versus-netless fork of `AiPilot` cannot be watched there.** *Evidence:* `AiNet` requires
  only `Id`, `Name`, `Nodes` and `Edges` (`AiNets.cs:250-261`) and leaves `Volumes` a plain `init`
  property, so a synthetic net needs no file, but the `--ai=` net lookup reads the chapter's
  `neindex` (`GameSession.cs:2413`), which an empty stage does not have. Separately,
  `ApplyVolumes` is called only from `CampaignDirector` (`CampaignDirector.cs:380`), so a CLI
  plane runs on `AiModeMachine`'s decoded defaults (2000/1200, `AiModeMachine.cs:141`) whatever
  net it is given. *Fix shape:* a built-in circular net above the grid origin at patrol altitude
  that `--ai=<plane>:<net>` and `--zep=` can name the way a chapter's `neindex` net is named, and
  the net's volumes applied to the CLI spawn through `CampaignRosterPlan.ApplyVolumes`. *⚠ Traps:*
  applying volumes on the `--ai=` path changes behaviour for existing command lines that name a
  chapter net, which today take the machine defaults instead; that is the fix rather than a side
  effect, but it is a behaviour change to announce rather than slip in. *Cross-refs:* the
  empty-stage rig this extends, which shipped deliberately without a net and whose `--ai=` squadron
  tokens and `--zep=` graft are documented in `docs/cli.md` (`git log --grep=BL-742`).

- `BL-773` `[Perf]` `[M]` `[Next: code]` `[Impact: none]` `[Evidence: trace]` **`--perf` has no
  term that attributes frame cost to the AI step, so a plane-count sweep can only be read as a
  whole-frame differential.** *Evidence:* `FlightController` steps in `_Process`
  (`FlightController.cs:1844`), so AI cost lands in script time; `script_ms` is `_perfProcess / n`
  (`Launcher.cs:1721`), one of the two Godot `TIME_*` monitors `BL-617` establishes hold the worst
  step of the last wall second rather than a mean; and `phys_tick_ms` brackets the physics tick
  (`PhysicsTickCost.cs`), which the AI never enters. The honest terms left on the window line are
  `p95_ms` and `max_ms` (`Launcher.cs:1735-1739`), both whole-frame. *Fix shape:* bracket the
  roster's per-frame AI walk the way `PhysicsTickCost` brackets the physics tick, and report an
  `ai_ms` beside `phys_tick_ms`. *Cross-refs:* `BL-617` (the same misreading, on `script_ms`); the
  empty stage's `--ai=` squadron tokens, which make a plane-count sweep repeatable and so make this
  term worth having (`docs/cli.md`, `git log --grep=BL-742`).

- `BL-798` `[Testing]` `[S]` `[Next: code]` `[Impact: none]` `[Evidence: trace]` **No golden shot renders the cockpit pass, so the shipped first-person view has no pixel tripwire.**
  *Evidence:* none of the 18 shots in `analysis/goldens/manifest.json` passes `--view=cockpit` or
  `--view=nose`, and a flight shot defaults to the chase camera, so the `SubViewport` the interior
  draws in (`Flight/CockpitOverlay`) is absent from every pinned image. Cutting the bearing mirror
  in `CockpitOverlay.Sync` moved a `--view=cockpit` C1/IA1 capture's pixel md5 from `50b5fcf1` to
  `274ed29c` while leaving all 18 goldens byte-identical, which is the size of the gap.
  *Fix shape:* one further manifest row, a `--chapter=C1 --plane=player_bhawk --view=cockpit`
  flight beside `c1-flight`. *⚠ Traps:* the pass composites over the main view under the HUD, so
  such a shot is pinned by the world behind the panel as much as by the panel, and a moved hash
  will need reading against `c1-flight` before it is read as a cockpit change. `--no-cockpit-pass`
  draws the same panel through the other path, so one shot pins one path only.
  *Cross-refs:* the `cockpit-overlay-pass` and `cockpit-sun-bearing` suites, which assert the
  pass's transforms and its light without rendering either.

## Misc

- `BL-077` `[Feature]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: data]` **Visual prop spin-up/down** (`startprops`/`stopprops` disc crossfade) — spawning mid-air
  already turning is by design; becomes relevant with a landing/shutdown flow
  (`FlightAudio.OnEngineStop` is already wired for the audio half).

- `BL-284` `[Bug]` `[Blocked: CAP-34]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Wing-light flare: soft round glow vs the original's sharp star burst; view-dependence
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
