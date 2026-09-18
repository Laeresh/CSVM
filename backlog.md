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

## Weapons & combat

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

- `BL-286` `[Tuning]` `[S]` `[Next: data]` `[Impact: low]` `[Evidence: footage]` **The first-person muzzle-light energy is picked, not
  measured, and reads much dimmer inside the canopy than the original's.**
  *Evidence:* `MuzzleLightEnergy` **2.5** (`CSVM/src/Flight/Projectile.cs`) is the one figure both
  `muzzle_burst` light branches take, because no def carries an energy at all: the two big
  `PLAYER_1ST_PERSON` lights (`bigmuzzle_lt` + `muzzle_lt`, at the authored offsets, ranges and
  colour) start at the third-person stand-in's brightness. The magnitudes were signed off in
  `--weapon-lab`, a static A/B that settles the third-person look only, and at the controls the
  interior lights far more weakly than the original's.
  *Fix shape:* one constant. The calibration clips are in the library:
  `OriginalScreenshots/Videos/CAP-39 1.mkv` (Devastator) and `CAP-39 2.mkv` (Bloodhawk), the
  original's cockpit view with the guns held. Read the lit interior's frame luminance at the
  light's peak against the unlit frame on both sides at a matched pose, and fit the energy to the
  measured ratio; the emphasis to match is the canopy struts above the head, which is where the
  original puts the flash at the controls. Any windshield bullet-hole decals in the same clips ride
  along for `BL-932`'s glass-hole judgement.
  *⚠ Traps:* **do not raise the energy on an estimate**; the two clips are the only cockpit-view
  gunfire in the library, so the fit is theirs. The pick-one single-quad flash reading
  (+ `_muzzle1`→`_muzzle2` flip) was implemented and rejected at the controls; do not re-land it
  without new footage evidence.
  *Note, the flash shape:* the def's 3-way `RANDOM_WEIGHT` roll (30/80/140°) may be rendered
  concurrently (all branches) by the original engine rather than pick-one, which would make the
  authored form itself a triad at those exact angles. Ours uses 120° spacing with one continuous
  roll; a 30/80/140° triad is one constant away and could be A/B'd against `MuzzleFlash1-3.png` if
  the flash shape is ever revisited.
  *Cross-refs:* `BL-932` (the canopy holes, which the same clips may show).

- `BL-289` `[Tuning]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: footage]` **Gun-impact looks (A2/`BL-203`)**,
  ⚠ **The six `DirtDebris*` constants left this entry: the dirt-chip effect they tuned was deleted
  (`BL-313`, closed), so there is nothing to A/B.** Dirt now takes the single spark.
  What remains here is the building ricochet:
  `RicochetSparks` **8**, `RicochetSparkSize` **0.55 m**,
  `RicochetSparkLife` **0.55 s**, `RicochetSparkSpeed` **22 m/s**, `RicochetSpreadDeg` **90°** (a
  stand-in, both authored assets are missing from the install). The water-splash column width
  is settled and out of this entry (`SplashColumnWidthScale` **8×**, `BL-265` closed).
  *Decoded:* the original indexes the `IMPACT` table with the struck material's `soil` byte and
  with nothing else (`FUN_005ac7a0` at `0x005ac7a9`, [docs/org/weaponImpact.md](docs/org/weaponImpact.md)
  carries the three effect slots with their addresses and gates); there is no building table and no
  structure arm. **The Hollywood studio blocks carry `soil` `default`, not `buildings`(11).** That
  chapter has no collider with id 11 at all, so a round on a studio wall reads `default` and plays
  the authored `3040slug_gunhit`, which is the effect the footage's wall debris comes from, and the
  five constants are unreachable there. `buildings`(11) belongs to C1's four `aphagar0N` hangar
  materials, where every gun but `wep_02` binds the install-missing `bld_damage.flt` and the
  ricochet burst stands in for it. The suite `impact-building-surface` holds both halves, and the
  one defect the decode localised is fixed: a row the effects runtime does render (`wep_02`'s
  `large_fireball`) no longer draws the invented burst on top of it.
  *Judged at the controls, and the reading above is contested by it.* The C1 hangar strafe drew
  no sparks in CSVM, and the user's account of the original is "the original in the C1 airport
  doesn't spark either, but has debris", the same wall debris `30 Slu building.mp4` shows on the
  film lot. So the spark burst is not what the hangar wants, and the decode's split (film lot plays
  `3040slug_gunhit`, hangar reaches the install-missing `bld_damage.flt` and so a stand-in) no
  longer explains what the original shows on the hangar. Re-decode it against that clip: what the
  original renders on a `buildings`(11) surface whose row binds a missing effect, whether a
  fallback row or the `default` row's debris is taken, and whether the film lot and the hangar in
  fact play the same thing. If they do, the five ricochet constants go with their burst and the
  hangar takes the authored debris; `PT-128` then judges the debris, not a spark.
  *Cross-refs:* `PT-128` (the flight that judges the result), `docs/org/weaponImpact.md`.

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

- `BL-603` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The human rig sweeps the mesh hull where the original sweeps its def's six
  `collision` probes.** *Evidence:* decoded for `BL-601` (`git log --grep=BL-601`): `FUN_0048d7f0`
  carries the def's `collision` list as rays from the previous pose, six points on the `p*` player
  defs; `FlightController.SweepProbes` does that for an AI rig and keeps the mesh-derived hull sweep
  for the human rig, which is wider than the six points. *Fix shape:* fly a slot the six points
  clear and the hull does not (CM13's dbase arch on dzpath2) in both games; if the original passes,
  sweep the player's probes too. *Cross-refs:* `PlaneStats.CollisionProbes`, `docs/formats/vehicle.md`.

- `BL-930` `[Fidelity]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **A pilot is not flashed, deafened or stunned by their own
  `SONIC`/`FLASH` burst, where the original's own guard exempts the damage pair alone.**
  *Evidence:* the original's self-hit guard is `CMP EBX,ESI` at `0x004b9e3e`, shooter against
  victim, and it stands BELOW the four no-damage arms of `FUN_004b9bc0`, so a `SONIC`, `FLASH`,
  `BEEPER` or `TANGLER` burst reaches its own shooter and only the damage pair is zeroed
  (`docs/org/ordnanceTypes.md`). CSVM instead drops the shooter one level earlier, in the blast
  pass's aircraft gather, and `ProjectilePool.ApplyDisabling` walks that same gather, so a pilot who
  detonates a sonic or flash round beside themselves takes neither the wash nor the stun. The
  choker cloud is already faithful here: `SpawnTanglerCloud` excludes nobody, so flying into your
  own cloud chokes you. *Fix shape:* pass the shooter through `ApplyDisabling` as a candidate while
  the damage gather keeps dropping it, which is one gather flag, not a second walk. *⚠ Traps:* this
  is not the self-blast exemption itself, which is decoded, faithful and pinned by the `air-to-air`
  suite; only the no-damage arms are at issue. The decide is whether a self-flash a player cannot
  see past is worth the fidelity in a two-to-four-player Dogfight. *Cross-refs:*
  `git log --grep=BL-301` (the VS tuning entry the exemption settled),
  `ProjectilePool.GatherAircraftCandidates`.

## Flight model & collision physics

- `BL-562` `[Perf]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: data]` `[CM11]` **CM11 (C2/M02) still spends a single physics tick of about 36 ms on the sortie's
  first part destruction, and about 130 to 138 ms on the first tick after the world build.** *Evidence (traced):* the bracketed
  instrument (`PhysicsTickCost`, `--perf`'s `phys_tick_ms` / `phys_tick_max_ms` / `phys_hz`), with a
  temporary sub-scope Stopwatch splitting one whole `FlightController.SimStep`, over three
  78-sim-second `--no-det` runs of 150 windows each, one aeroplane and nobody at the controls with
  17 AI aircraft alive. Median `phys_hz` is 60.0 in every run and 4 to 10 windows of 150 exceed
  16.7 ms. (a) The FIRST tick after the build costs 130 to 138 ms, 95 ms of it the 20 animation
  runtimes' first `Advance` (46,355 index rows walked over five cold `FindAll` misses, plus a 29 ms
  first sim step); it is the world-build settling regime, not flight. (b) **The collision sweep is
  ruled out.** The 36 to 43 ms tick a player meets in flight is the sortie's FIRST part destruction:
  on the step where the pilot's right wing reaches 0 % against `g1176/col_buildings` the whole step
  costs 36.1 / 36.6 / 37.0 ms across the three runs, of which `AircraftContactResolver.Resolve`
  holds 35.6 / 36.2 / 36.4, `IContactEffects.SpendDamage` 29.1 / 29.5 / 29.7 and `ShatterStruck`
  4.6 / 4.7 / 4.8, while the sweep costs 0.35 to 0.41 ms and `UnEmbed`'s single `Overlaps` 0.04 ms,
  with no collection anywhere across the step. What that `SpendDamage` does is start the damage
  presentation for the first time (`rightwing_damage_effects`, the `pdpanel6` / `pdpanel1` /
  `pdpanel2` swaps, `player_fuelleak`); an earlier graze in the same sortie that destroyed no part
  spends 0.73 ms there. Only one part destruction happens per unattended run, so whether the second
  costs the same or the first is paying a warm-up is unmeasured. The sweep's own worst step over the
  three runs is 2.9 / 5.3 / 2.9 ms: the 2.9 ms is the session's first sweep and is managed
  first-call cost (0.4 ms of it in the physics server), and the 5.3 ms is one `CenterRayContact` ray
  that struck nothing, so no collider is answerable for it and the report the query fills never
  reads above 0.00 ms. (c) **The 33 to 36 ms steps carrying `gc=1/1/0` were the AI target ranking
  reading the world's node names on every tick, and that is fixed.** A per-phase allocation meter
  (`--perf`'s `[perf] alloc` line, `sim_alloc_b=`, now permanent) puts 98 % of a settled tick's
  allocation in `SimPhase.CapturedAiAircraft` at about 309,000 B per tick, and a temporary bisect
  inside it lands on `TargetPool.CollectOwners` (251,904 B per tick over 164 calls, 81 % of the
  tick) and `TargetPool.NameOf` (about 36 KB per tick over 197 calls, 12 %). Both are Godot
  node-name reads: every AI shooter asks every ranked structure candidate for its whole gamez
  ancestor chain on every physics tick, and each ancestor's name comes back as a fresh string, a
  finalizable `StringName` wrapper and a `DisposablesTracker` entry, which is the finalization rate
  the pause follows (PERF-20). Measured and ruled out in the same pass: the AI pilot decision (15 to
  21 B per tick), the collision sweep (308 B), the ray casts (about 2.9 KB) and the whole `_Process`
  pass except `Flight` (about 13.8 KB per frame). The ancestor chain is now cached on the
  destructible instance, keyed by the anchor's parent so an authored re-parent re-walks. Two before
  and three after runs on the same rig (78 sim seconds, `--no-det`, one aeroplane, nobody at the
  controls): `CapturedAiAircraft` 309,000 to 57,100 B per tick, `alloc_mb_s` 19.2 to 19.7 down to
  4.1 to 5.6, `fin_per_s` about 60,500 down to about 9,300, per-10-wall-second collections gc0=4
  gc1=4 down to gc0=1 gc1=1 (gc2=0 in the settled regime both ways), `pause_per_s_ms` 25 to 41 down
  to 3.5 to 8.5. Worst tick per window over the last 60 windows: mean 31.7 and 36.7 ms down to 8.3,
  9.5 and 15.6; windows over 16.7 ms 20 and 21 of 60 down to 3, 3 and 9 of 60; maximum 116 and 123
  ms down to 55, 72 and 91. Median `phys_hz` stays 60.0. The spread across the three after-runs is
  fight variance under `--no-det`, so the GC terms are the stable measure (PERF-20). What is left on
  the tick is `TargetPool.NameOf` at about 42 KB per tick over 220 calls, then
  `Enumerator[AiRatingBias]` boxing in `AiTargetRanking.MatchedBias`'s `foreach` over an
  `IReadOnlyList`. The 50 ms `AnimRuntime.Advance` step is not a fourth class: window 240, two
  seconds in, reads 49 to 57 ms with only about 21 ms of it named by any simulation phase, and sits
  inside the first GC window's 211 ms of pause over 16 gen-0, 15 gen-1 and 7 gen-2 collections, so
  it belongs with (a). *Fix shape:* (b) next. (b) is now a
  question about the damage presentation rather than about collision: decide whether the first
  damage-stage start is spread off the contact tick, which touches `SpendDamage` and the damage
  pools, not the sweep. (a) is worth a separate look only if a cutscene handoff or a mid-mission
  stage build repeats it. *⚠ Traps:* **the sweep premise is dead**: the sweep is under 2 % of the
  step it sits in on the spiking tick, so do not re-derive a sweep cost from a tick maximum or from
  the `HumanAircraft` phase maximum that holds it. A gen-1 GC pause lands inside whichever
  sub-scope of a bracket happens to be open, so read the GC counters across the same span before
  naming the term a bracket reports (`docs/verification.md` PERF-34). **The cap-exhaustion premise
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
  *Cross-refs:* `PLAN-M5-polish-6` C22, `docs/verification.md` PERF-1, PERF-19, PERF-20, PERF-23
  and PERF-34.

## Environment & world

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

- `BL-322` `[Research]` `[Blocked: CAP-58]` `[L]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` `[C5]` **The original shades a lit surface per vertex and clamps the
  product at white, so reproducing its sun term darkens every away-facing surface of every chapter
  and cannot darken a C5 city block at all** (split out of `BL-303` at its close). Explicitly NOT
  fog, `BL-303`'s own adjunct note, and the Wave B fog work moved none of it.
  *Evidence, decoded:* the whole term is in `docs/org/vertexLighting.md`, "The sun's own term, and
  where the product is clamped". `SUNLIGHT_BICOLORED` is light flag `0x400` (`FUN_004dbe80`, tested
  once per light at `0x00568a7f`) and it selects whether the ambient half of the term carries
  `SUNLIGHT_COLOR_AMBIENT` or reuses `SUNLIGHT_COLOR_DIFFUSE`; `FUN_00472ea0` writes both colours on
  every zone apply, so the second one always exists and the bit only decides whether it is read. All
  32 C5 zone blocks author white for both, so **in C5 the bit is a no-op on the value** and the term
  is the single scalar `0.5 + 1.5 × max(N·L, 0)`. It is live elsewhere: 28 of the install's 212 zone
  blocks are bicolored with two different colours, C4's `IA1` pairing a warm diffuse with a cool
  ambient. C5 also places exactly one `Light` node among 11,438 and authors no `LIGHT_STATE` at all,
  and the two point-light lists `FUN_00568790` fills add their term without the directional loop's
  `− 1`, so nothing but the sun ever reaches the accumulator there.
  *The measured band is refuted on the family it was read off:* `FUN_00554550` multiplies the
  per-vertex factor by the **authored vertex colour** and clamps the product at 255 (`0x00555289`
  then `0x005552b2`), where `Weather.WorldLightFactor` clamps its collapsed scalar to 1 *before* the
  multiply. All 8,549 authored vertex colours on C5's `cblock1`-`cblock7` city-block skins are 255,
  so the original's product saturates at white for every `N·L ≥ 1/3` and draws exactly what we draw,
  and is *darker* than us below it. Only the `bldg1`-`bldg4` tower skins (51 % to 71 % at 255, tenth
  percentile 0 to 112) stay unsaturated, and there the ratio ours/theirs is `1 / (0.5 + 1.5 N·L)`,
  which is 0.66 at `N·L` 0.68 and 0.58 at 0.82. So the term can produce the measured band, but not
  on the low-rise blocks a midtown frame is mostly made of.
  *Frame-wide is ruled out, so this is a surface term, not exposure or gamma:* the five HUD gauge
  discs are the same 2D art at the same pixels in both frames and read ours/orig **0.97** (rockets
  0.97, ALT 1.07, damage 0.80, guns 1.03, MPH 1.00; the "2400" readout box 0.96). Nothing ×0.6
  survives into the capture or onto the composite.
  ⚠ **The `CAP-11` C5 pair is not a matched pose.** `t0.5-c5-spawn-night-city.png` looks north over
  midtown from altitude and `csvm-c5-night-city.png` sits over water, so 10.2/15.5 and 21.7/37.6
  compare *different buildings*, and the decode above says the difference between two buildings is
  exactly what decides the ratio. C5's sky dome cannot serve as the in-world control either: at a
  level view its rows read 0.83, 0.69 and 0.43 of the original's from zenith to horizon, a gradient
  mismatch of its own rather than one scalar. `CAP-58` is the matched pose this needs.
  *Fix shape:* per lit vertex, `drawn = clamp(authored_vertex_colour × (SUNLIGHT_AMBIENT +
  SUNLIGHT_DIFFUSE × max(dot(N, L), 0)), 0, 1)` in place of `ALBEDO *= csky_world_light`. Four
  files: `Weather.cs` stops collapsing (the uncollapsed pair is already on `ZoneWeather`),
  `WeatherRig.cs` publishes the pair and the sun direction beside `csky_world_light`,
  `csky_atmosphere.gdshaderinc` declares them, and `SceneBuilder.cs`'s shaded world variant reads a
  normal instead of the flat scalar. It is not a one-uniform change: 66 % of C5's 21,577 lit
  polygons and 57 % of C1's 16,580 carry no normal array, so `SceneBuilder` has to emit a face
  normal per polygon for those rather than the smoothed normal `SurfaceTool` generates.
  ⚠ **Taking it is a look change on every lit surface of every chapter, and its largest effect is a
  darkening nobody has asked for.** A vertex authored at 255 facing away from the sun halves, and
  most of a city at night faces away from a sun bearing −135°. That is owed a verdict at the
  controls and not a luminance distance.
  *Playtest after fix:* the C5 night poses in `playtest/CAP-11/README.md`.

- `BL-325` `[Feature]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` `[C1B]` **Night cloud sprites are directionally moonlit in the original; ours are
  uniformly lit** (split out of `BL-118` at its close, PLAN-overcast-match `C24`, decision 2 of
  that plan kept it out of a two-daytime-stills milestone). The original lights a
  night cloud by which side of it faces the moon; we apply one flat `WorldLight` to the whole
  population, so a moonlit frame is right on the away side and roughly half as bright on the lit
  side.
  *Evidence:* `CAP-11` C1B (`playtest/CAP-11/`): cloud cores near the moon reach
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
  brightness knob; `FogVolumeClutter` carries no such constant, and the only thing that reaches the
  authored 240 is the original's own per-vertex term, on the cards whose chapter authors it.
  ⚠ **The lighting gate cannot supply this item's direction, decoded, and that half is now
  built.** Every placed `cloudparent` card in every deck chapter (C1's 626, C1B's 1,620, C1C's
  1,056, C4's 1,453) is authored `lighting: false` and ships an EMPTY normal array, so neither the
  gate nor the geometry the term runs on is there, and C1B carries no `fvol` volume to hold the
  cards that do. The original's directional light therefore reaches nothing in C1B's footage and
  the item's stated mechanism is not the one in that frame; re-check what the measured p90 above
  is actually reading (fog mix, not the world light) before building on it. What the gate does buy
  is a per-vertex term on the `fvol` cards, and `FogVolumeClutter` now applies it: the card carries
  its three authored normals and a `lighting: true` chapter (C1C, C2B, C5) evaluates
  `AMBIENT + DIFFUSE × max(N·L, 0)` per vertex through the billboard basis off the zone's
  uncollapsed `SUNLIGHT` pair, clamping the product against the authored 240 rather than the
  factor. C1 and C4 author false and are byte-identical. That lifts a C1C card top from the flat
  **163.7** toward the placed facades' **235.25** while its underside drops, which narrows the
  71.6 cloud-population spread at the top of each puff and widens it at the bottom, and C1B moves
  by nothing at all (p90 173.0 whole frame, 165.7 moon side, 175.3 away, before and after).
  *Playtest after fix:* two sorties, the C1B night spawn
  (`--chapter=C1B --pos=-5406,55,-7200 --direction=-0.391,0,-0.921`) against `playtest/CAP-11/`'s
  t5 and t16 frames, saying for every measured puff which side of the moon it faces, and one lit
  chapter (C1C above the band) for a verdict on whether the new per-vertex shading on the `fvol`
  cards reads like the original rather than only measuring closer.
  *Cross-refs:* `docs/formats/effects.md`'s speed-cue section (the third population),
  `docs/org/vertexLighting.md`'s facade section, `CAP-11`.

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

## Effects & animation runtime

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

- `BL-537` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Effect pools at four players, judged in play.** The pool sizes in `CSVM/data/effect_pools.json` were re-judged on a build with no first-use construction cost: rockets and the sonic burst never wrap, a four-object simultaneous death wraps `flame_ball_01` at 4 and 6 slots and is quiet at 8 (now shipped), and seven or more identical deaths in one frame wrap at the 16 ceiling and cannot be sized away. At the controls the single-player half reads right: four fireballs burn out in place, and the seven-death wrap is not visible under the debris. Still owed: a 4-player splitscreen session with everyone firing, judged for anything that reads as shared between panes, and the ceiling for many-player builds (at 16 players the default root wants 19 and gets 16). The instrument is `AnimRuntime.PoolRecycles` and the `anim: effect pool for '<name>' recycled slot` DEBUG line in the log file sink; the sizes staged print on the world-effects build line. ⚠ Raise only a root that logs a recycle, never the default; the three gun roots stay at 1; a root sized 0 clamps to 1. Each slot copies the root's subtree (155 templates at 1 player, 263 at 4).
  *Cross-refs:* `PT-129` (the four-player flight that judges it), `BL-296` (the other splitscreen-scoped item).

- `BL-867` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **The speed cue's wisps read
  more opaque than the original's.** *The sortie:* fly C1 in chase view over water at about 58 m,
  the one altitude the original is filmed at with the cue running, and judge the wisps against
  `CM02.mkv` at that altitude. Report whether they still read too opaque now that the spread is in,
  and if so whether the sprite reads too solid (the size stand-in or the blend) or too bright (the
  authored alpha). *Evidence:* reported at the controls against mission recordings: the pale wisps
  each chapter's `speed_cue.zrd` emits ahead of the player (`Flight/SpeedCue.cs`, three authored
  `cuepufferN` states selected by altitude) are far more prominent than in the footage; size reads
  right, opacity does not. Stills cannot settle it: the only original frame with wisps has a
  different background from any CSVM render, and no recording shows wisps above 500 ft (CAP-11's
  C1B never runs the cue at any altitude). The lateral spread is landed: at 16:9 the cue's emitter
  widens its lateral spawn half-width by the pane's aspect over 4:3, 9.0 m to 12.0 m on
  `cuepuffer1`, leaving the vertical and forward halves and the generic puffer's cube where the
  data puts them. *Fix shape:* the alpha comes from the authored state, so a halved constant would
  be a departure from data; CSVM's own contribution is the puffer's judged sprite-size stand-in
  (`Puffer.SizeScaleDefault`) and the blend the texture header names. Move the constant the sortie
  names: the blend or the size stand-in before the authored alpha, a cue-only alpha factor last.
  *⚠ Traps:* three cloud populations share textures
  (`BL-118`'s note under the cloud items): judge the wisps by altitude and by their position
  ahead of the aircraft, never by texture. *Cross-refs:* [`docs/formats/effects.md`](docs/formats/effects.md)
  (the cue's data), `docs/org/puffer.md`.
- `BL-952` `[Fidelity]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The world
  shader's point-light spill is shaped differently from the original's point-light term, so a
  ground burst lights the walls facing it and barely the ground under it.** *Evidence:* the
  original adds `weight(d) × (ambient + diffuse) × colour` per vertex, `weight` linear from the
  light's near range to its far one, with no `N·L`, only on models whose `lighting` bit is set, and
  clamps the product with the vertex colour (`FUN_005688a0`, `FUN_00566e00`;
  `docs/org/vertexLighting.md`, "Point lights: how a `LIGHT_STATE` range is applied").
  `CSVM/shaders/csky_lights.gdshaderinc` weights by `N·L × (1 − smoothstep(near, far, d))` and
  `SceneBuilder` adds `base_colour × spill` to every fullbright surface, with no `lighting` gate and
  no product clamp. A `he_ground_effect` burst beside C1's airfield (the light sits at ground level)
  lifts the hangar walls facing it by up to 39 of 255 and leaves the flat ground around it nearly
  unchanged, where the original's term, having no `N·L`, lights both. *Fix shape:* the linear
  weight without `N·L`, gated by the model's `lighting` bit, applied as a factor on the vertex
  colour before the clamp. *⚠ Traps:* the change moves every golden that holds a lit
  `LIGHT_STATE`, `c5-city-night` and the C1 beacons first; ambient and diffuse default to 1.0 and 0
  for a `LIGHT_STATE` that authors neither, as `he_light` does. *Cross-refs:*
  `docs/architecture/Mech3.md` (`WorldLights`).

## Audio

- `BL-269` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The 3D sound falloff between the authored `RANGE`
  radii is the original's own curve now, and wants one pass by ear.** The approximation is gone:
  `SoundFalloff.cs` computes the decoded law and `WorldSounds` drives every emitter's level from it,
  so the players carry no engine attenuation model. The curve holds full volume one eighth of the
  way into the band, then loses 10 dB per doubling of the reach past that shelf, reaching 30 dB down
  at the audible radius and running straight to silence over the next tenth. For the police siren
  (`RANGE [200, 1200]`) that is 0 dB to 325 m, -10 at 450, -20 at 700, -25.8 at 950, -30 at 1200 and
  silent at 1320; for the train (`RANGE [600, 1200]`) 0 dB to 675 m, -10 at 750, -20 at 900, -25.8
  at 1050, -30 at 1200. The old curve was 6 dB per doubling measured from the emitter, multiplied by
  Godot's linear fade to nothing at `MaxDistance`, which is what dropped it early: the train was
  already 6 dB down at its own full-volume radius and silent at 1200 where the original is 30 down
  and still audible. *Playtest:* `PT-150`, the same two fly-pasts that produced the complaint.
  ⚠ The C1 refinery flare is not a candidate emitter: neither `refinery_fire_always` nor
  `refinery_fire.zrd` authors a sound. ⚠ Do not tune this by taste without re-reading
  `docs/formats/sounds.md`: every number above is decoded, so a change is a change away from the
  original. If the fly-past says it is still wrong, the suspect is the world scale or the other
  positional sites (`AiWeaponAudio`, `WeaponAudioCues`, the turret voices) which still use Godot's
  own inverse-distance model with a 1.1x cull, not this curve.

- `BL-281` `[Tuning]` `[Blocked: CAP-27]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The ricochet sounds are audible but very faint.** `PT-25` (c), 2026-08-05:
  `snd_ricochet1–4` play under the per-impact spark burst but sit too low to read. A mix-gain
  question with no reference recording behind it, to be judged at the controls rather than derived. ⚠ Judge only after `CAP-27` decides
  whether the original has this effect at all: `BL-090` already calls the 0.99 `injure_anims`
  entry that drives it "plausibly an authoring leftover", present on 1 of 11 aircraft, so the
  capture may delete the feature rather than tune it.

- `BL-285` `[Tuning]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: footage]` **The throttle-slam plume streams the
  wrong smoke, and its threshold cannot be judged until it streams the right one.** *Evidence:* the
  plume now fires at the controls, and what it draws is the `nitro_boost` def's own `nitropuffN`
  puffers, which `ThrottleSlamSmoke.Build` borrows from the boost by construction, where the
  CAP-21 footage shows black exhaust smoke ("the nitro smoke, not the black smoke from the
  original"). The nitro puffers were a stand-in: nothing in the tree decodes which emitter the
  original starts on a slam. *Fix shape:* decode first. Find the original's throttle-rise arm (the
  same throttle read the slam gate ports) and which puffer or effect it starts, port that emitter
  from the exhaust markers, then fly `PT-127` for the threshold. The gate stays: the CAP-21 footage
  bounds it at its two ends only, a single 1/8 step (0.125) never fires and idle to 5/8 (0.625)
  does, leaving the 2/8 to 4/8 band unobserved; 0.25, a two-notch jump, is the smallest round
  number consistent with both, and `PT-127` moves it or leaves it once the smoke is the decoded one.
  ⚠ Traps: the plume was unreachable at the controls until the gate moved onto the sim step. It was
  driven from the rendered frame while the lever slews only inside the flight step, so at the 120
  rendered frames a second a realtime session holds over the fixed 60 Hz step it read the throttle
  flat on every other frame and no slam of any size could cross any threshold. Judge nothing off a
  build that predates that fix. The gate itself is covered by the `throttle-slam-smoke` suite (an
  idle-to-full slam fires once, a single 1/8 step fires nothing), so what is left is the number
  alone. *Cross-refs:* `PT-127` (the sortie that judges it).

- `BL-782` `[Feature]` `[Blocked: BL-455]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Built-in's Options screen carries no audio
  levels, so the mix is settable in the Original presentation alone.** *Evidence:*
  `PLAN-audio-preferences` builds the AUDIO page for Original and puts four levels (Master, Music,
  Effects, Voice) in the shared options store, which is where every setting both presentations show
  already lives. Built-in's Options screen holds ten steppers (difficulty, default view, automatic
  head turn, targeting, rumble, graphics mode and the four display settings) and the Controls door, and its apply hands back the
  four levels untouched, the only settings it does not show, so once the page lands a player on
  Built-in can hear the mix and not reach it. Blocked until the four levels exist in the
  store, which is the whole of the dependency: nothing else about this item waits on that plan.
  *Fix shape:* four rows on Built-in's Options screen reading and writing the same store fields the
  AUDIO page does, in the screen's own stepper convention, applied through the same
  `OptionsApplyExit` the ten current rows leave by. The four display rows landed on that screen
  are the worked model: read the store word, step over the row's own values, write the word back.
  *⚠ Traps:* Built-in has no continuous control of any kind, so a 0 to 100 level is a stepper with a
  chosen step rather than a slider, and the step size is a judgement the row has to make rather than
  inherit. Do not add a second writer: `Launcher.ApplyOptions` is the options file's one writer and
  both presentations reach it through the apply exit. Do not re-tune `MusicPlayer.ChannelLevel` on
  the way past; `BL-455` deletes it.
  *Cross-refs:* `BL-455` (the page this mirrors), `PLAN-audio-preferences`,
  `git log --grep=BL-783` (the same gap for the display settings, closed by four rows on this
  screen), `docs/menu-presentations.md`.

- `BL-933` `[Bug]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]`
  **The gun voices now run the decoded level law, and whether that is enough to hear a turret, a
  patrol boat and another aeroplane's guns is the listen `PT-151` owes.** *Evidence:* reported at
  the controls three times, after `BL-793` landed a positional loop per mount ("cant hear guns
  of turrets. And planes only when very near. Too much falloff perhaps?"), after `BL-820` gave the
  hulls a voice ("Cant hear them. They fire but there is no sound") and against `BL-846` ("i can
  only hear them if they are shooting right beside me"). The cause was the mapping `BL-269` named:
  every gun voice built its player with `UnitSize = RANGE`'s full-volume distance and
  `MaxDistance` = its audible one on Godot's inverse-distance model, which multiplies a second
  linear fade onto the level, reaches silence exactly at the audible radius where the decoded law
  still plays to 1.1x, and brings a low-pass of up to 24 dB above 5 kHz that guts a gun's crack.
  Against `SoundFalloff.cs` at `VOLUME` 1, `snd_turretgun` `RANGE [30, 400]` read -13.0 / -22.5 /
  -32.0 / silent dB at 100 / 200 / 300 / 400 m where the law gives -6.0 / -18.8 / -25.5 / -30.0;
  `snd_60cal` `RANGE [35, 450]` read -11.3 / -20.2 / -28.2 / -40.2 against -3.3 / -16.7 / -23.5 /
  -28.1. Both sites now carry a player with no attenuation model and no `MaxDistance`, levelled
  from the law per frame, the way `WorldSounds` already was.
  *What is left:* the audible radii are the data's and did not move, so a turret's `snd_chaingun`
  is still silent past 220 m and a 30-cal past 165 m, and whether the recovered level is enough at
  the controls is a judgement no instrument makes. The aeroplane loops author `RANGE [20, 150]` to
  `[40, 550]` by caliber, not the `[80, 800]` of the dry cue `snd_emptyclip`.
  *⚠ Traps:* `WeaponSoundCue.CullMargin` (1.1) is `SoundFalloff.CullFactor`, decoded from the
  compare, and is not the knob; `BL-846`'s closing note says the same. Do not retune a level by
  ear: every constant here is the decode's or the authored `RANGE`.
  *Cross-refs:* `PT-151` (the sortie), `BL-269` and `PT-150` (the same law on the world emitters,
  owed its own listen), `docs/formats/sounds.md` ("The gain between the two radii"), `INSTR-87`,
  `git log --grep=BL-793`, `git log --grep=BL-820`, `git log --grep=BL-846`.

- `BL-934` `[Bug]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: feel]` **The dynamic enemy and
  ally voice lines dispatch and play again; whether they are audible at the controls is unheard.**
  *Evidence:* the silence was the mode-machine subscription sitting inside `RegisterAi`'s voiced
  branch, so an aircraft carrying no accent was never watched, and the shipped rosters leave nearly
  every enemy on `accentID` -1 while voicing the player's own flight. A flown C1 session now logs
  `ai voice: ai2_player_pfighter: trigger #8 -> snd_id7_WA-Enemy-6 (Talker test passed. Play AI
  sound #8.)`, an accentless hostile committing to the human and a voiced ally speaking the bearing
  call-out, and the `ai-voice-mission` suite holds the same chain over C1/M02's shipped roster
  through to a playing stream on the Voice bus. *What to settle first:* whether the line is audible
  from the cockpit, which no instrument here answers (`docs/verification.md`). *⚠ Traps:* the
  scripted lines ride `Mech3/MissionRadio.cs`, a different channel, so their working proves nothing
  about this one; the aircraft that speaks is never the enemy that was spotted, so a silent enemy
  is not the symptom. *Playtest after fix:* fly C1/M02 from the campaign, stay in the fight beside
  the enemy flight for a minute or two, and listen for a wingman calling an enemy's clock bearing
  and for the tiered damage calls while you take and deal hits. *Cross-refs:*
  `docs/formats/combat-voice.md`, `git log --grep=BL-934`, the saved Voice level (`Utils/AudioMix.cs`, the `audio-buses`
  suite) if the lines dispatch and stay inaudible.

## Cameras & views

- `BL-266` `[Research]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **Plane wobble: residual decode questions after the
  wiring landed.** *Decision:* the dive rattle and the nitro wobble both read too small and janky
  next to the original's, and the mechanism was the reason rather than any magnitude. Both now run
  the original's own component block. **Owed sortie:** fly the merged build against
  `OriginalScreenshots/Videos/Dive Wobble.mkv` and `OriginalScreenshots/Videos/Nitro Wobble.mkv`,
  a dive past rated max for the first and a nitro engage for the second, and judge the ported
  amplitude and rate before touching `DiveRattleKickScale` or `NitroWobbleKickScale` (both default
  to the faithful step). The oscillators are wired (`ShakeDefs`/`PlaneShake`, visual-only roll on
  the plane node; law and measurement in [`docs/formats/shakes.md`](docs/formats/shakes.md) and
  `analysis/gun-wobble-shake/`). The dressing behind the shake is a decoded camera
  random-walk (`crimson.exe`), not the remake's dated sawtooth, details below.
  **Resolved:**
  - **(a) made faithful:** the fire source IS a random-walk accumulator
    (`PlaneShake.FireBullet` steps `Walk += (rand−0.5)×2·(factor×caliber)·7.54`
    = uniform ±7.54·(factor×caliber)/shot, wep40 ±2.11e-2 rad; decayed
    by the authored `damp` τ≈80 ms). Merged to `main` (`eba69782`, branch experiment `bl266-random-walk`).
    **`GunBuzzKickScale` (default 1.0 = faithful) is the one tune knob, dial it, never
    `magnitude_factor`.** So the fire gap was a **mechanism/law mismatch, not a render-pipeline
    loss**, and this port is the first real feel of the kick law.
    (The old approach-(B) suspects are also settled: fire-rate is one round per tick at authored
    `FIRE_RATE` (8.0 for wep_40), the "12–13/s" was a redraw-window artifact, and 60 fps
    pose-interpolated render loss tested NEGATIVE.)
    ⚠ **The (d)/(e) decode says that step is the wrong size and the wrong kind, so (a) is not
    closed for fidelity.** The `7.54` came from the camera constructor's *defaults*, a `2.0` gain
    and the `6.2832` waveform branch, which `FUN_0042bc10` overwrites for every authored source.
    `fire_bullet` authors `sawtooth 1` and `frequency 15.0`, so the original's step is
    `mag × 15 × 4 × 1.2` and the kick is a **velocity** of ±36·mag rad/s into block 0's `[3]`, not
    a displacement. A displacement walk at ±36 would render roughly forty times the original's
    angle, so the number cannot simply be swapped; `_fire` keeps its landed step until (a)'s own
    look decides, and the choice is between the whole component block (as (d)/(e) took) and the
    envelope it has. `docs/org/shakes.md`, "`fire_bullet`, per-shot roll".
  - **(d) the dive rattle now runs the original's component block, ported.** The per-frame player
    updater `FUN_0048c470` reads block 4's `min_speed`/`magnitude_quotient` and calls the same
    `FUN_0042c070`/`FUN_0042be10` dispatcher the `fire_bullet` path uses on component index 4
    (`camera+0xd4/+0xd8/+0xdc`), every frame the excess-over-gate law
    (`(speedRatio − min_speed)/magnitude_quotient`, `PlaneShake.SetSpeedRatio`) is positive. What
    the kick adds to is a **velocity**, and the rendered angle is the position `FUN_0042bec0`
    integrates out of it on the block's `sawtooth 1` branch, so `PlaneShake` now carries the pair
    rather than an envelope: a per-tick kick of ±36·magnitude rad/s and the two-branch integrator
    at the original's `1/150` substep. `DiveRattleKickScale` (default 1 = the faithful step) is the
    one knob; `magnitude_quotient` is decode. Trace:
    `analysis/gun-wobble-shake/FINDINGS.md` (high_speed section) and
    [`docs/org/shakes.md`](docs/org/shakes.md).
  - **(e) the nitro wobble runs the same block, ported.** `FUN_004b2131` kicks block 6 with the raw
    authored `magnitude` 0.05 once per engage, player only, and the block's authored
    `frequency 4.0, damp 3.0, sawtooth 1` turns that into a velocity kick of ±0.48 rad/s that
    renders as a decaying triangle over about a second and a half. `NitroWobbleKickScale`
    (default 1) is the knob. The judgement that drove this ("janky at the beginning, larger but
    very fast, then too small but still very fast") was three complaints the mechanism explains at
    once, and none of them was a magnitude. ⚠ **The plane wobbling is the decode, not the defect**
    ([`docs/org/shakes.md`](docs/org/shakes.md), "Two oscillators, one mechanism"): a plane-mounted
    camera inherits the roll, and `PT-86` asked for a *camera* shake it should not have. The
    contradiction this clause flagged is settled from the binary: `[3]/[4]/[5]` are the velocities
    and `[6]/[7]/[8]` the positions the consumer sums, `docs/org/shakes.md` had it right and
    `analysis/gun-wobble-shake/FINDINGS.md` had it reversed twice; the findings page is corrected.
    The `sawtooth` branch constant `4.0` is a literal at `0x00603514` and genuinely separate from
    the authored `frequency 4.0`, so that coincidence is not one.
    *Still unanswered:* whether the original's engage moves the nose or only the roll.
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
  - **(fidelity) judge the port, then dial.** Playtest owed on all three knobs, each defaulting to
    its faithful step: `GunBuzzKickScale` against the firing clip, and `DiveRattleKickScale` and
    `NitroWobbleKickScale` on the sortie named at the top of this entry. Two honest caveats: the
    fire source's **decay model (τ≈80 ms) is an engineering guess, not a decode**, and its step is
    the one (a) now flags as wrong-sized; and the ported dive rattle is about 1.1× the replaced
    sawtooth's RMS but 2.2× its peak, so the mechanism alone only partly answers "too small" and
    the look is what decides whether a knob moves.
  ⚠ Traps: `SHAKES_CAMERA` is NOT the fire-path shake mechanism, its sole carrier among all
  48 weapons is `wep_26` "FW", a zero-damage scripted fake weapon (a scripted detonation-shake
  marker); the fire path is the unflagged `fire_bullet` source. And the near-match trap: several
  magnitude candidates coincide with authored constants, wire nothing on one coincidence (the
  caliber law stood because the candidates separated by an order of magnitude each way).
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
  *Cross-refs:* the numpad views' three level keys (`Kp2`/`Kp4`/`Kp6`), which sit at this same
  base elevation (`git log --grep=BL-150`), `docs/formats/camparam.md` (`thirdp_pitch`, and the Known limits paragraph this corrects),
  `docs/org/cameraViews.md` (head-look controller, the chase placement's own elevation).

- `BL-924` `[Fidelity]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: feel]` **The cockpit view does not turn the head with the
  aeroplane's motion, and the original's does.** *Evidence:* judged at the controls on the cockpit
  sitting that closed the view's other seven decisions (`git log --grep=BL-436`): panel scale,
  head-look feel and sign, the lean's sub-cap, engine-sound precedence, wobble and free-look in
  Nose all read right, and the automatic head turn is what is missing. CSVM has C22's autohead
  built (`HeadLook.AutoheadTarget`, fed by `FlightController.AutoheadTarget` with the def's
  `autohead_turn_*` values) and gated behind `headLook.autohead`, default off because the
  original's cockpit footage read as a pixel-frozen sight through manoeuvres (`Config.cs`,
  `docs/formats/vehicle/player-globals.md`'s autohead row). *Decide:* whether the default is
  simply wrong, or whether what the original does at the controls is a different turn from the
  velocity lean the port implements. Fly with `headLook.autohead` true first; if that is the
  original's turn, the fix is the default and the Game Options row (`BL-784` (c)). If it is not,
  the turn wants a decode of what drives the head in mode 6 beyond the lean. *⚠ Traps:* the
  forward-velocity drop in the lean is C22's port decision and reads right; do not widen the lean
  to fake a turn. *Cross-refs:* `BL-784` (c), `PLAN-cockpit-view` C22, `docs/org/cameraViews.md`.

- `BL-966` `[Research]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: feel]` **Which camera the original's Danger
  Zone photograph is taken from.** *Evidence:* both remake cameras (`StuntCapture` for a stunt run,
  `CampaignSnapshot` for a campaign mission) photograph the pilot's own pane as it is drawn on the
  latch frame. The user's recall of the original's scrapbook photographs is that they "look like
  taken from an outside position, not the chase view". The scrapbook format decode names the file
  and the forced 164x123 region but not the eye. *Deliverable:* read the original's capture call
  (the writer of the `.PN_` file the commit sweep `FUN_004072a0` renames) and say whether it grabs
  the back buffer as drawn or poses a camera of its own first, and if the latter, from where. If a
  camera is posed, mint the port with the pose. *Cross-refs:* `Utils/PaneReadback.cs` (the
  latch's asynchronous readback), `docs/formats/campaign-screens.md` ("The danger-zone slot"), `docs/org/debrief.md`.

- `BL-968` `[Bug]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: data]` **An F12 screenshot stalls the frame
  it is taken on.** *Evidence:* F12 (`Launcher`, `LaunchMenu`) calls
  `CaptureDirector.SaveScreenshot`, which reads the viewport back with a synchronous
  `GetTexture().GetImage()` and writes the PNG on the main thread. That is the path that cost a
  Danger Zone photograph 1.3 to 1.7 s at 5120x1440, and a stall of that length crashed the game.
  `BL-964` moved only the Danger Zone cameras off it. *Fix shape:* take the frame through
  `PaneReadback.Request` and write the PNG on its worker, logging "screenshot saved" when the file
  lands; keep the synchronous path for `--screenshot`/`--shots` captures, which exit on the file.
  *⚠ Traps:* `MenuCaptureSuites` presses F12 and must wait for the file rather than read it on
  the same frame. *Cross-refs:* `git log --grep=BL-964`, `Utils/PaneReadback.cs`.

## HUD & UI

- `BL-113` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Compass tape**: two
  readings the reference stills cannot settle on their own. The bar-end caps are gone, the tile
  draws at bar height, and the comb stops on a three-row black hem, all measured off `HUD.png`,
  `Targeting HUD Kestrel.png` and `C1 IA1 whiteout at height.png`. What is left is a look call on
  both remaining picks, on the montage at `.scratch/orch-7/BL-113/bl113-compass-montage.png`
  (the three originals' tape crops over the port's own tape, both fade laws crossed with upright
  and squeezed letters). (3) The rim falloff ships as `min(1, 1.35·cos(Δ)^2.1)`, fitted to the
  stills' own luminance profile across the bar (rms 0.026 against the plain `cos(Δ)`'s 0.102),
  which holds the inner half flat and takes the outer quarter near black; the montage carries the
  old cosine beside it. (5) Whether the octant labels foreshorten with the drum is unsettled by
  the stills themselves (on the Kestrel shot the edge `E` at Δ about 44° is about 0.8 of the
  centre `E` while the edge `S` is full width), so they ship upright and `--compass-squeeze`
  draws them with the ticks' own horizontal squeeze for the A/B. North = −Z is confirmed against
  the original, do not reopen. The nearest-tick look stays as shipped.
  *Cross-refs:* `PT-121` (the flight that judges the result).

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

- `BL-784` `[Feature]` `[Blocked: a user decision on the page's row budget]` `[S]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The Game Options
  page carries the original's Default View and Auto Head Turn rows, and holds six rows where the
  plate holds four at the authored pitch.** *Decision:* the taller plate is built, tiled from the
  one plate image and recorded in `docs/org/menu-inventory.md`; what is open is which rows the
  page keeps. *Evidence:* the settings side stands. Both rows are entries in the `GameOptions`
  table with `OptionsDef.DefaultView` and `OptionsDef.AutoHeadTurn` behind them, both rows stand
  on Built-in's Options screen in its stepper convention, `SessionSpec.WithSavedDefaultView` and
  `WithSavedAutoHeadTurn` fold them into a launch (a `--view=` outranking the saved word through
  `ViewModeExplicit`, a `--det` run reading neither), and `FlightController.AutoHeadTurn` gates the
  automatic turn with null leaving the `headLook.autohead` config key deciding, so no default
  moves. The dropdown's words are the original's own: `uiData` 2127 at `0x0040d124` builds exactly
  three items (`0x0040d12d`), langui 112 "Cockpit", 136 "First Person" and 113 "Exterior"
  (`0x0040d181`, `0x0040d168`, `0x0040d14d`), and `FUN_00419160` maps those indices to camera
  modes 6, 7 and 0 at `0x004191a4` while turning the autohead flag `DAT_0071dacc` on at
  `0x00419180`. The plate grows one whole 62-pixel band, tiled from the band between its own
  seams rather than stretched, and the plaques move down with it. **What is open is the row
  budget.** `GO_BACKGROUND` stands at Y 215 and is 566x289, so its bottom sits at 504 and only one
  62-pixel band fits inside the authored 600; the plate cannot move up either, `PF_LOGO` already
  ending at 253 over its top. Four rows then fit at the authored 62 and the page carries six
  (Difficulty, Default View, Auto Head Turn, Menu, Next Target, Rumble), so the pitch tightens to
  40 and the rows no longer sit on the painted panels, which the renders beside
  `git log --grep=BL-784` show. *Fix shape:* the user picks one of three: keep the squeeze, move
  the remake-only Next Target and Rumble rows off this page (a second page, or the pause sheet),
  or page the rows four at a time. *⚠ Traps:* (a) do not stretch the plate to buy room, the
  stretch pulls the rivet holes into ovals and thins the panel edges. (b) Do not move autohead's
  default: the row exposes the existing key, and `BL-924` is where the default itself is argued.
  *Cross-refs:* `BL-924` (the missing automatic head turn), `BL-782` (the same
  both-presentations gap for the audio levels), `git log --grep=BL-783` (the display settings' half,
  closed with four rows on Built-in's screen), `docs/org/menu-inventory.md`,
  `docs/org/cameraViews.md`.

- `BL-809` `[Bug]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` **An opened scrap
  reads differently from the original's: the typeface differs, and where ours shows the scrap's
  picture the original shows a newspaper-header-like image and then the text.** *Evidence:* reported
  at the controls as "Scrapbook Texts look different in the original", detailed as "Typeface, the
  image not showing but another one like the header of a newspaper and then the text". `CAP-52.mkv`
  t=46 to 88.8 opens the scraps full page. The composition is now the decode's
  (`docs/formats/campaign-screens.md`, "Resolving a row to a file"): a `P0` scrap draws the family
  background alone (the "newspaper header" is `SB_BG_B.jpg`'s masthead) with no inset and no EXPORT,
  and each text box takes its langui row's `[FONTID]` face at points times 4/3 in pixels, pitched at
  that size, in the box row's colour, a centred block justified as a whole
  (`docs/formats/strings.md`, "Font prefix"). *Open question for the user:* the montages
  (film | ours before | ours after) are `montage_{wanted,diary,letter,medal,hawaii,aloha,bristol}.png`
  beside the item's commit message under `.scratch/orch-8/BL-809/`. Faces, sizes, positions and
  pitch match the film by my reading; the one visible difference is weight, the original's text
  reading near-black and heavy while ours draws the authored `0xFF444040`-class grey anti-aliased
  and reads lighter. Does ours read right, or should the zoom's words draw heavier (unhinted-style
  darker text, or the authored colour pushed toward black)? Also judge `AB` as Book Antiqua, read
  from its letters alone. *⚠ Traps:* the look is the user's call; never close on a distance.
  *Cross-refs:* `CAP-52`, `docs/org/menu-inventory.md`.


- `BL-946` `[Fidelity]` `[M]` `[Next: decide]` `[Impact: low]` `[Evidence: decoded]` **The Weapon
  Loadout screen maps rocket widget N to physical pylon N+1 where the original maps it to the saved
  record's ordnance cell N, so a starboard pylon can draw in a port box.** *Evidence:* left open
  deliberately when `BL-919` closed, which is where the mapping is written out: CSVM numbers the
  boxes by physical pylon, the original numbers them by the record's cell and resolves the pylon
  from the fit (`Loadout.PylonForCell`, resolve against the fit and never against `LeftWingPylons`
  alone). *Fix shape:* the decision first: take the original's cell order, or keep the physical
  order. *⚠ Traps:* the physical order is not an accident, it is what makes the rows agree with the
  weapon gauge's belt lights, so taking the original's order contradicts that choice and the gauge
  has to be answered in the same breath. *Cross-refs:* `git log --grep=BL-919`,
  `docs/formats/campaign-screens.md` (the Ammo Selection screen).

- `BL-951` `[Feature]` `[L]` `[Next: decide]` `[Impact: high]` `[Evidence: trace]` **A local
  multiplayer door on the main menu, opening a join board where every controller claims its seat
  once and holds it for the whole session, instead of seats being decided implicitly on whichever
  flight screen happens to open joining.** *Evidence:* joining today is scattered across screens
  and has no board of its own: `OriginalSeats.JoiningOpen` opens it on Free Flight, Dogfight,
  Instant Action, the campaign flight check and the controls page, nowhere else, so a player who
  presses Start anywhere but those five joins nobody and gets no word back. Seat 0 meanwhile
  borrows every unclaimed pad until it claims one by steering a screen with it
  (`MenuSeatDevices.ClaimP1Pad`), which means who ends up on which plane is settled by who moved a
  stick first rather than by anyone saying so, and the binding is only read out at launch
  (`Launcher.BindMenuPads`). Two bugs came straight out of that implicitness: the claim taking a
  pad another player had joined on (`git log --grep=BL-949`) and a device blip reshuffling the
  seats (`git log --grep=BL-950`). *Fix shape:* a door on the top level opening a board of four
  slots, each showing its device name and its player tag; Start on an unclaimed pad takes the next
  free slot, Back leaves it; the resulting roster is what every later screen and the session itself
  read, so the flight screens stop being where seats are decided. *⚠ Traps:* the original has no
  such screen, so this is remake-only chrome and needs its own art direction decided before any of
  it is drawn; the wrap-up page's post-its and tick box (`git log --grep=BL-945`) are the nearest
  precedent, procedural furniture in the page's own inks since no shipped art fits. The existing
  per-screen join must become the board's own gesture rather than a second path, or the two disagree about who is
  seated. Seat 0 keeps the keyboard whatever the board says, so a slot is a pad's claim, not a
  player's existence. Device loss and reassignment is the part that bites (the linked thread lands
  on it too, over batteries and devices claimed twice); `MenuSeatDevices`'s guid reconciliation and
  its grace already carry it for the current flow and should not be reimplemented on the board.
  *Cross-refs:* `SeatStrip.cs` (the chip row that already names seated players, the board's
  nearest existing art), `MenuSeatDevices.cs`, `PlayerSetupFeature.cs`,
  https://discussions.unity.com/t/local-multiplayer-player-join-config-screen-using-ui-toolkit/1701038
  (the pattern as other local co-op games ship it, asked for by name).


- `BL-958` `[Bug]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: feel]` **The spyglass disc's picture still steps in a
  hard turn.** *Evidence:* judged at the controls after the eye moved onto the interpolated draw
  pose and the disc's camera dropped the own aeroplane's layer (`git log --grep=BL-906`): the
  picture inside the disc moves in steps with the target rather than smoothly, while the disc
  itself holds and the own aeroplane no longer crosses it. So the remaining source is the disc
  camera's aim, not its eye: the target it looks at is read from a pose that is a physics step
  behind, or the aim is quantised by the bearing it is composed from. *Fix shape:* log the disc
  camera's aim and the target's drawn pose per rendered frame in a hard turn and find which one
  steps; then feed the aim the same interpolated pose the eye takes. *⚠ Traps:* the
  `spyglass-marker-hud` suite runs in one frame and cannot see a per-frame step; a live `--fly`
  probe with a log line is the instrument. *Cross-refs:* `git log --grep=BL-906`.

- `BL-967` `[Feature]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: spec]` **Built-in's Instant Action
  wrap-up board shows the stunt run's Danger Zone photographs.** *Evidence:* an Instant Action stunt
  run under Built-in ends on `IaWrapupBoard`, which draws the four counters and the splits but no
  thumbnail strip; `StuntScoreboard`'s strip is the solo run's alone (`HumanFlightAdapter` builds no
  scoreboard under Instant Action). The Original page draws the run's photographs as prints beside
  its post-its. *Fix shape:* hand player 1's `StuntCapture.InMarkerOrder()` to
  `IaWrapupBoard.Present` at the wrap-up (read then rather than at the ending, as the Original
  handover in `InstantActionDirector` does) and draw the strip under the splits the way
  `StuntScoreboard` does, a pending cell empty and filled on `StuntCapture.ShotLanded`.
  *⚠ Traps:* the run completes on its last zone's gate pair while the camera latches on the marker's
  sphere, tested after the run on a shared frame, so a list read at `MissionEnded` can miss the last
  shot. *Cross-refs:*
  `Flight/StuntScoreboard.cs`, `UI/InstantActionWrapupPage.Prints`.

## Splitscreen

Our splitscreen mode (2–4 players) has no counterpart in the original, so every rule it authored
against "the player" or "the camera" needs an explicit splitscreen verdict: generalise it, take a
nearest/union rule, or record it as deliberately single/global. This theme collects that work
(sweep of 2026-08-15). **Route fixes through the two existing seams instead of minting new ones:**
`GameSession.PlayerPositions` (nearest human) for gameplay rules that say "the player", and the
viewer set behind `ProjectilePool.Viewers` / `ScreenSize.NearestFloor` for draw rules that say
"the camera". Sim state stays global, the mission wind is the worked example
(`Session/WeatherRig.Tick`, stepped once per frame outside the per-rig loop on purpose). Splitscreen-scoped items that live with
their own system: `BL-537` (the 4-player pool judgement), `BL-296` (per-player ActionMap),
`BL-314` (race countdown).

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
  swap against splitscreen's `MixGain` term is unjudged at the controls**; the single-player
  engine level is judged matching (`git log --grep=BL-391`), a listen that never isolated the
  `_cp` def specifically. (c) **Today's hiding mechanism is node visibility on a shared plane node, not a
  per-viewport render flag**: `CockpitVisibility` hides the OWN rig's `healthy` body node, so a
  pilot sitting in the cockpit hides THAT AIRCRAFT'S body in every pane that can see it, not just
  their own, a cross-pane effect unjudged at `N > 1`.
  *Fix shape:* isolate the draw-count growth's split between the cockpit subtree and the rest of a
  4P rig; a splitscreen listen for the cockpit-swap/`MixGain` interaction; confirm or fix the
  cross-pane body-hide visually at the controls with 2+ cockpit-view pilots in the same session.
  *Cross-refs:* `PLAN-cockpit-view` B11 ("Splitscreen posture"), `BL-389` (splitscreen weapon
  mix, same playtest family).

## Missions, modes & campaign

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
  The return rule is settled and ported: the leash is the return cylinder measured from the pursuit
  anchor, the pursuer's own pose where the promotion began (`git log --grep=BL-927`). What remains
  of the cycle is the promotion side and the dwell pair, which is decoded and unported.
  `basic_airplane` authors `attack_dwell 60` and `not_pursuit_dwell 5` and every aeroplane def
  inherits them, and `+0x300` is both the promotion's refusal (`FUN_0041f040`) and the first
  disjunct of the pursue tail's revert (`0x0041e674`): in the original an unassigned chase runs a
  minute, then the aeroplane waits five seconds before promoting again, while a chase on an assigned
  `primary_target` never reverts at all (the guard at `0x0041e5fd`). ⚠ Port both ends or neither;
  the cap alone, without the promotion's own refusal beside it, only churns the mode once a minute.
  *Cross-refs:* `BL-524`, `docs/org/aiPilot.md` ("`attack_dwell` and `not_pursuit_dwell` are pursuit
  timers", "The third volume, and where the leash is read"), `BL-522`'s closing commit
  (`git log --grep=BL-522`).

- `BL-956` `[Bug]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: feel]` `[CM01]` **CM01's cargo zeppelin keeps
  flying after its propane tanks are destroyed, wandering under power instead of crashing.**
  *Evidence:* at the controls the zeppelin is "not really crashing but flying random after
  destroying the propane tanks": it keeps steering, off its authored path, so the crash never takes
  over. The mission authors `cargozep1_crash` beside its gasbag and engine sequences
  (`extracted/C3/M01/mis_anim/cargozep1-*.json`), and the objective script retargets from
  `propane` (`docs/formats/objectives.md`). *Fix shape:* decode the chain from the propane
  objective's completion to the `cargozep1_crash` call and find which link CSVM drops: the
  callback the objective posts, the prerequisite the crash sequence gates on, or the AI path that
  is never stopped when the crash starts. A headless `--campaign` run with the tanks destroyed by
  probe, logging the sequence starts, is the instrument. *⚠ Traps:* zeppelins have no crash
  avoidance and an AI path keeps driving one until something stops it (`docs/org/aiPilot.md`), so a
  crash that plays without stopping the path looks like this too. *Cross-refs:*
  `docs/formats/anim-definitions.md`, `docs/org/sequences.md`.

## Tooling, platform & docs

- `BL-033` `[Cleanup]` `[Blocked: SDL >= 3.4.4]` `[S]` `[Next: code]` `[Impact: none]` `[Evidence: data]` **Drop the `SDL_JOYSTICK_DIRECTINPUT=0` launch-script workaround** (set 2026-07-19 in
  RunGame.ps1/RunDev.ps1) once tools/godot ships a Godot bundling **SDL ≥ 3.4.4**. *Decision:*
  wait for that SDL; the variable stays in the launch scripts and out of the export until then,
  and the item lands with the Godot bump that carries the fix. The bundled
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

## Misc

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
