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
  be read off the original as `player_fortune`'s were. The same holds for the three base defs a
  campaign mission fields directly (`bloodhawk`, `autogyro`, `balmoral`); each is logged once as
  `[paint] ai def '<def>' authors no paint_pattern` and flies its shipped skins.
  *The campaign half routes now.* A mission's own enemy set and its generator waves both resolve
  their militia def from the `aiv` roster block's NAME (`VehicleDefs.DefForBlock` strips trailing
  `_N` ordinals until a def matches, so `medkestrel_1` is `medkestrel` and `blakepeace_2_1` is
  `blakepeace_2`), and `CampaignRosterPlan.SpawnFor` carries it into `PlaneStats.LoadForAi`. The
  resolution is a function of the mission and the block alone, never of a running spawn count, so an
  aeroplane built before the flight starts already knows its livery. CM01 fields Medusa Kestrels;
  the British defs are CM02's (`britbalmoral`, `britpeace`) and CM04's (`britpeace`).
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
  *Size:* what remains is the cockpit confirmation plus the two loose ends above (`dare_devil`, the
  one-pylon-per-entry reading).
  *Cross-refs:* `BL-386` (the damage half, landed and closed 2026-08-16,
  `git log --grep=BL-386`; this builds on the `PlaneStats.AiDefName` seam it left),
  `docs/formats/vehicle.md` (the def-family census), `docs/formats/instant-action.md` (the militia
  table's provenance).
  *The AI ordnance trigger reads the fit now.* `AiRocketeer` takes each pylon's own engagement band
  and refire interval, and stamps both timers a launch stamps in the original: the vehicle-wide
  lockout that blocks ordnance of any kind and the launching slot's own next-ready. `AiGunner` takes
  a bound group's window the same way. Launch rates are worth tuning from here, and the
  `DAMAGES_ZEPPELIN` rule is exercisable in the cockpit rather than only in `AiRocketeerTests`.

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

- `BL-286` `[Tuning]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Muzzle-flash residues after the `BL-263` pick (triad kept, 2026-08-05)**, one
  small open. (a) closed 2026-08-06: the muzzle-light stand-in magnitudes (was `BL-200`, rode
  `BL-261`/`BL-263`; `MuzzleLightEnergy` 2.5, 2-frame `MuzzleLightLife` 0.03 s, the def carries
  range/colour only) are signed off, judged in `--weapon-lab`; static, so the sign-off covers
  magnitudes only, not motion. (c) closed: the muzzle light now rides the firing muzzle node,
  measured 1.65/1.71 m astern before and 0.00 m after over its two drawn frames. The flash quads
  were already anchored, and the def places them at the node with no displacement: the authored
  forward offset belongs to the effects root (`AT_NODE` 0, −0.2, −1.0), which carries the casing,
  the smoke and the lights, and is now built. (b) The user's engine-semantics
  hypothesis, open: the def's 3-way `RANDOM_WEIGHT` roll (30/80/140°) may be rendered
  concurrently (all branches) by the original engine rather than pick-one, which would make the
  authored form itself a triad at those exact angles. Our triad uses 120° spacing with one
  continuous roll; a 30/80/140° triad is one constant away and could be A/B'd against
  `MuzzleFlash1-3.png` if the flash shape is ever revisited.
  ⚠ Trap: the pick-one single-quad reading (+ `_muzzle1`→`_muzzle2` flip) was implemented and
  rejected at the controls, do not re-land it without new footage evidence.
  *Playtest after fix:* (c)'s anchoring, judged in flight rather than in the lab: the flash sits on
  the muzzle at speed and the light travels with the plane over its two frames. An instrument can
  only say the gap is 0.00 m; whether the burst now reads as coming out of the gun is yours.
  The same sortie carries the effects root's displacement, since the casing, the smoke and both
  first-person lights now spawn a metre ahead of the gun and 0.2 m below it: from the cockpit a
  shot lights the interior from a metre further forward, which only your eyes can judge.
  *Decision:* judged in flight, three things. (1) The flash falls back too fast: it is drawn for
  one frame and sits behind the muzzle on the next, so the burst does not read as coming out of
  the gun. (2) The original shows no muzzle flash at all from the cockpit view, so hide the quads
  there. (3) The first-person shot light is much dimmer than the original's; raise the energy, and
  `CAP-39` is the clip that calibrates how bright. The 30/80/140° triad question stays a note for
  any later shape revisit.

- `BL-289` `[Tuning]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: footage]` **Gun-impact looks (A2/`BL-203`, landed 2026-08-01)**,
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
  *Decision:* judged on C1's movie-studio buildings against `30 Slu building.mp4`: no burst plays
  at all where the original throws debris off the wall, so the five stand-in constants are not
  what is wrong. Decode what the original spawns on a building hit (and why nothing of ours
  reaches those buildings) before any of them moves; `PT-128` stays the flight that judges the
  result.

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

- `BL-399` `[Feature]` `[L]` `[Next: decode]` `[Impact: low]` `[Evidence: decoded]` **Track Target's camera is the decoded padlock mode, the third value of the
  head-look state byte, aimed at the current target.** *Decision:* build it faithfully as
  `HeadLook`'s state `2`, not as a remake follow camera; snap versus smooth, the chase-camera
  blend and the no-target and target-behind cases are read off the original's padlock handler,
  not designed. *Evidence:* the player-targeting plan's out-of-scope call (b), 2026-08-15: the
  original's `Views 1 → Track Target` binds `L` (free in our flight keymap; our `L` is the
  viewer-only livery lab), decoded in `docs/org/targeting.md`, but "keep the target framed" hides a
  pile of camera decisions that plan deliberately deferred: snap vs smooth follow, override vs
  blend with the chase camera, behaviour with no target selected or a target behind the pilot, and
  interaction with the right-stick free look (`BL-372`). `L` is reserved in `docs/controls.md` but
  bound to nothing. `PLAN-cockpit-view`'s head-look decode names the mechanism this camera would
  ride: the look-state byte the controller reads (`DAT_0064ef68`) has a third value, `2`, for
  padlock, sitting beside the `0`/`1` snap/free-look states the `K` and `J` selector keys already
  pick between, so `L`'s camera is this same state machine's third mode, not a bolt-on. Building it needs
  `TargetSelection.Current` plumbed into `HeadLook`'s target so the padlock state aims the head at
  the current target instead of reading player input.
  *Fix shape:* decode the padlock branch of the head-look controller (what state `2` does with
  the head angles each tick, how it leaves the state when the target is lost, and whether the
  chase camera follows the head or the aircraft), then plumb `TargetSelection.Current` into
  `HeadLook` and bind `L`. No change to the targeting module itself, which already exposes
  `TargetSelection.Current` cleanly for a camera to read.
  *Cross-refs:* `BL-372` (right-stick free look), `HeadLook.LookMode` and its `SelectMode` writers,
  which state `2` enters and leaves through, `docs/org/targeting.md` "Track Target", `docs/controls.md`,
  `PLAN-cockpit-view` (`HeadLook`, `src/Flight/HeadLook.cs`).

- `BL-603` `[Bug]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: decoded]` **The human rig sweeps the mesh hull where the original sweeps its def's six
  `collision` probes.** *Evidence:* decoded for `BL-601` (`git log --grep=BL-601`): `FUN_0048d7f0`
  carries the def's `collision` list as rays from the previous pose, six points on the `p*` player
  defs; `FlightController.SweepProbes` does that for an AI rig and keeps the mesh-derived hull sweep
  for the human rig, which is wider than the six points. *Fix shape:* fly a slot the six points
  clear and the hull does not (CM13's dbase arch on dzpath2) in both games; if the original passes,
  sweep the player's probes too. *Cross-refs:* `PlaneStats.CollisionProbes`, `docs/formats/vehicle.md`.

## Flight model & collision physics

- `BL-562` `[Perf]` `[M]` `[Next: data]` `[Impact: low]` `[Evidence: data]` `[CM11]` **CM11 (C2/M02) still spends single physics ticks of 33 to 57 ms in flight and
  about 130 to 138 ms on the first tick after the world build.** *Evidence (traced):* the bracketed
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
  reads above 0.00 ms. (c) One reaches 50 ms inside a single `AnimRuntime.Advance`. A fourth class
  is not a term of the code: 33 to 36 ms steps carrying `gc=1/1/0` and a GC pause equal to the whole
  step, landing in the preamble, the ground-blow probe, the flight model or the tail on different
  runs (PERF-34). Window 240, two seconds in, reads 49 to 57 ms with only about 21 ms of it named by
  any simulation phase, and sits inside the first GC window's 211 ms of pause over 16 gen-0, 15
  gen-1 and 7 gen-2 collections, so it belongs with (a). *Fix shape:* (c) next. (b) is now a
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

- `BL-322` `[Research]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: decoded]` `[C5]` **C5's lit facades render ×0.58–0.66 of the original, and collapsing the
  original's per-vertex sun term to one clamped scalar predicts that band** (split out of `BL-303`
  at its close; measured `CAP-11`: tower faces 10.2 vs 15.5, low-rise 21.7 vs 37.6). Explicitly NOT
  fog, `BL-303`'s own adjunct note, and the Wave B fog work moved none of it.
  *The exemption premise is refuted, the residual is not:* `docs/org/vertexLighting.md` decodes the
  hardware draw's one gate (`FUN_00552020`), and **the measured facades are on the lit side of it**,
  so no exemption can close the ratio; the texture alpha bit is software-only (`FUN_005524d0`) and
  offers no lever on the overlays either. What closes it is the *value*. C5 IA1 authors
  `SUNLIGHT_DIFFUSE` 1.5 and `SUNLIGHT_AMBIENT` 0.5 in both its zones
  (`extracted/C5/IA1/zrdr/weather.zrd.json`, ZONE1 and ZONE3), so `FUN_005688a0`'s per-vertex
  `ambient + diffuse × max(N·L, 0)` runs 0.5 to 2.0, while `Weather.WorldLightFactor` collapses the
  pair to `clamp(0.5 + 1.5 × 0.46, 0.15, 1)` = **1.0**, discarding 0.19 at the clamp. On a wall the
  ratio ours/theirs is `1 / (0.5 + 1.5 N·L)`: 1.0 at `N·L` = 1/3, and 0.545 at the most a vertical
  wall reaches under a sun 25° above the horizon (`SUNLIGHT_ORIENTATION` −25/−135). The measured
  0.66 and 0.58 sit inside that range at `N·L` 0.68 and 0.82, walls within 26° to 42° of the sun's
  azimuth; a roof, whose `N·L` under that sun is 0.42, predicts 0.88.
  *Frame-wide is ruled out, so this is a surface term, not exposure or gamma:* the five HUD gauge
  discs are the same 2D art at the same pixels in both frames and read ours/orig **0.97** (rockets
  0.97, ALT 1.07, damage 0.80, guns 1.03, MPH 1.00; the "2400" readout box 0.96). Nothing ×0.6
  survives into the capture or onto the composite.
  ⚠ **The `CAP-11` C5 pair is not a matched pose.** `t0.5-c5-spawn-night-city.png` looks north over
  midtown from altitude and `csvm-c5-night-city.png` sits over water, so 10.2/15.5 and 21.7/37.6
  compare *different buildings*. The band is the right order and the wrong precision; a re-measure
  wants a pose pinned by `--pos`/`--direction` on both sides. C5's sky dome cannot serve as the
  in-world control either: at a level view its rows read 0.83, 0.69 and 0.43 of the original's from
  zenith to horizon, a gradient mismatch of its own rather than one scalar.
  *The decode still owed, before any code:* (1) `SUNLIGHT_BICOLORED` is 1 in both C5 zones and is
  decoded nowhere, so which of the light class-data colour triples (`+0xa4` diffuse, `+0xb0`
  ambient) it selects between, and whether `FUN_00472ea0` writes a second, decides whether the term
  is the scalar above or a two-colour blend; (2) whether a single sun really leaves exactly
  `ambient + diffuse × max(N·L, 0)` once C5's `LIGHT_STATE` point lights populate `FUN_00568790`'s
  other two lists, given `FUN_00568830` seeds the accumulator to 1.0 and each light contributes
  `× colour − 1`; (3) which regime the measured walls are in, since 65.0 % of C5's 113,926 authored
  polygon vertex colours are 255 and the rest run below it, and where `V × F` passes 255 the
  original saturates and the ratio becomes `V/255` instead of `1/F`.
  ⚠ Reproducing the per-vertex term is a look change on every lit surface of every chapter, so it
  is owed a verdict at the controls and not a luminance distance.
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

- `BL-508` `[Research]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The original never alpha-tests, so every alpha texture we scissor is an
  invention rather than a reproduction.** *Decision:* the user picked blended for all three
  families the montage showed, trees (tree, bush and brush cards, 13 textures), rails (fence,
  railing, ladder, stair and grate, 20) and lattice (tower lattice, girder, support and cable,
  23), each judged shipped-against-blended in one shot (C1 tree line, C4 Jimmy's camp, C2 Eiffel
  replica). Land those three as the family table; a scissored texture the census names outside
  them is not decided and stays scissored until shown. ⚠ The switch must reach `Clutter` as well
  as `TextureArchive`: `Clutter`'s sprite shader hardcodes its own 0.5 scissor and never reads the
  archive's alpha class, and C1's whole tree population is clutter, so a `TextureArchive`-only
  table leaves every scattered tree card scissored. *Evidence:* decoded from `crimson.exe`
  (`analysis/alpha-classification/FINDINGS.md`, "The original has no cutout path"). The renderer is
  `zvid_ddd3d.c` over `IDirect3DDevice3`, and `D3DRENDERSTATE_ALPHATESTENABLE` is set nowhere in
  the whole `0x0059e000–0x005ab000` layer, nor are `ALPHAREF` and `ALPHAFUNC`, so alpha test holds
  its Direct3D default of FALSE for the entire run. Blending is one per-texture mode field
  (`tex+0x10 == 4`, `FUN_005a4210`) against a fixed `SRCALPHA`/`INVSRCALPHA` pair, and the
  archive's `TextureAlpha` class decides pixel PRECISION only (`FUN_005a27e0`: colour key for
  `Simple`, 8888 → 4444 → 1555-at-128 for `Full` depending on the card). Ours scissors 388 of the
  604 alpha textures, tree and fence cards included. *Fix shape:* a per-family switch in
  `TextureArchive` (the `SoftAlphaCoastline` list widened into a family table) so a montage can
  flip one family at a time; after the look, each family the user picks as blended joins the
  table, and if every family goes, `AlphaIsSoft` and its 0.45 threshold become dead code and the
  census script goes with them. *⚠ Traps:* blending moves a surface into the
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

- `BL-683` `[Bug]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **The faithful path's aircraft ambient cannot be driven by the mission, because
  `AmbientLightEnergy` never reaches the shader.** *Decision:* the faithful Environment takes the
  mission's authored `SUNLIGHT_AMBIENT` as a colour-sourced ambient, so a night chapter's
  aircraft get a dark fill and both halves of the authored pair are visible. *Evidence (traced and measured):* the faithful
  arm writes the zone's authored `SUNLIGHT_AMBIENT` onto the Environment, but
  `AmbientLightSource.Sky` at full sky contribution makes Godot take the ambient off the sky
  cubemap scaled by the background energy multiplier, so the value is inert. Taking the launcher's
  0.9 to 0.0 left all 18 goldens byte-identical, while the same experiment on the sun, 1.6 to 0.5,
  moved 7 ([`docs/verification.md`](docs/verification.md) `WORLD-32`). So an aircraft's fill light
  is a daytime procedural sky at night as well as by day, and only the sun half of the authored
  pair is visible. *Fix shape:* on the faithful arm set `AmbientLightSource.Color` with the
  authored `SUNLIGHT_AMBIENT` colour and its energy, leave the sky contribution to the Enhanced arm,
  and re-pin the goldens the aircraft fill moves; the fullbright world is untouched because it
  does not read the Environment's ambient. *⚠ Traps:* this is a rendering-design question and not a bug to patch by turning the
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

- `BL-905` `[Fidelity]` `[M]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **Aircraft read
  too glossy under sunlight beside the original's screenshots.** *Decision:* the user picked
  specular 0.25 at roughness 0.85 off the sweep montage against `OriginalScreenshots/Fury from
  above.png` (0.50, 0.10, 0.00 and roughness 1.0 were the other tiles); land it as the one
  constant in `GetBiasShader`'s shaded branch and re-pin the goldens listed below, the decode
  follows as the record behind the picked value, not as a gate on it. *Evidence:* every aircraft
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

- `BL-925` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: data]` `[C5]` **A clutter sprite is
  planted at its decoration node's own origin, so C5's lamp glow sits on the pavement instead of on
  the lamp head.** *Evidence:* a template decoration is a two-node chain, the `.flt` node the
  template ground parents and the mesh node under it. `ClutterBuilder.ParseTemplate` reads
  `deco.Local` from the `.flt` node and reaches the mesh through `FirstWithMesh`, which returns the
  node and never its transform, so the mesh node's own local translation is dropped. In C5's
  `cblock*` templates `w_lightglow.flt` translates by 0 and its `w_lightglow` mesh child by
  y = 4.75 (extracted `C5/gamez/nodes.json`), the height of the lamp head on the 5 m `lightpole`
  card beside it. The glow quad is centred on its own origin (y in [-0.684, 0.684]), so it lands
  half-buried in the road. Every other clutter decoration in the install has an identity mesh-node
  transform (`lightpole` reads `Initial`), so C5's lamp glow is the only placement this moves.
  *Fix shape:* carry the mesh node's local translation into the `Kind`'s cell placement and add it
  to the sprite's planted point, the way the solid path already adds `cell.Origin.Y`.
  *⚠ Traps:* the sprite branch of `PlaceOnTriangle` drops `cell.Origin.Y` deliberately, because a
  tree card's mesh spans y from 0 up while its `.flt` node's Y is the ground; that drop is right
  and is not this defect. Do not raise the card by editing its mesh either, since the same mesh is
  what the `--viewer` path draws through `SceneBuilder`, where the chain's transform is already
  honoured. *Cross-refs:* `BL-508` owns the `Clutter` sprite shader's hardcoded 0.5 alpha scissor,
  which currently eats all but the core of the glow, so the two land their look together.

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
  *Cross-refs:* `BL-231` (the pool's tuning entry), the `trail-world-anchor` suite (rendering at
  real spawns), `BL-700` (the same zeppelin family's wreck rest).

- `BL-867` `[Tuning]` `[S]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **The speed cue's wisps read more opaque than the
  original's, and crowd the centre of a 16:9 view.** *Decision:* stills cannot judge the opacity,
  the only original frame with wisps (CM02.mkv at about 58 m, chase over water) has a different
  background from any CSVM render, and no recording shows wisps above 500 ft (CAP-11 C1B never
  runs the cue at any altitude). What the user asks for first is to spread the wisps out
  sideways: the authored deviation is a cube (`Puffer.SpawnSustained`, ±0.5·d on every axis)
  sized for the original's 4:3 view, so at 16:9 the puffs sit in the middle of the frame. Land
  a cue-only remake rule that widens the emitter-frame lateral half-width by the viewport's
  aspect over 4:3 (so 1.33 at 16:9), leaving the generic puffer's cube alone since that
  re-scatters `c1-waterfall`; then re-judge the opacity at the controls with the spread in.
  *Evidence:* reported at the controls against mission recordings: the pale wisps
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

- `BL-252` `[Tuning]` `[S]` `[Next: decode]` `[Impact: low]` `[Evidence: footage]` **Overspeed-whine volume** (`prop_sound`). `CAP-10` plus a
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
  *Decision:* judged at the controls, whatever our build plays past top speed is almost
  inaudible, and this entry has tuned the wrong slot once already. Decode what the original plays
  at the `1.0× fd_speed` foot and at what level (the rattle's `snd_planeshake` curve is the
  candidate to confirm or refute in `crimson.exe`) before any mix constant moves; `PT-126` then
  confirms the port by ear.
  *Cross-refs:* `PT-126` (the flight that names the sound and matches its level).

- `BL-269` `[Tuning]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **The 3D sound falloff curve between the authored `RANGE` radii is an admitted
  approximation** (`WorldSounds.cs:159-161`, endpoints authored, curve "an approximation of
  the original's, hence TUNE"). Low stakes per sound but global: every positional sound's
  audible footprint. A calibrated fly-past recording of one loud fixed emitter would trace the
  real curve. *Decision:* judged on fly-pasts of the police car and the train, the falloff drops
  too fast: a sound goes quiet closer to its emitter than the original's does. Flatten the curve
  between the authored radii so the level holds longer before it dies, then re-judge on the same
  two emitters. ⚠ The C1 refinery flare is not a candidate emitter: neither
  `refinery_fire_always` nor `refinery_fire.zrd` authors a sound.

- `BL-281` `[Tuning]` `[Blocked: CAP-27]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The ricochet sounds are audible but very faint.** `PT-25` (c), 2026-08-05:
  `snd_ricochet1–4` play under the per-impact spark burst but sit too low to read. A mix-gain
  question with no reference recording behind it, to be judged at the controls rather than derived. ⚠ Judge only after `CAP-27` decides
  whether the original has this effect at all: `BL-090` already calls the 0.99 `injure_anims`
  entry that drives it "plausibly an authoring leftover", present on 1 of 11 aircraft, so the
  capture may delete the feature rather than tune it.

- `BL-285` `[Bug]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: footage]` **Engine start/stop residues from `BL-267` (landed 2026-08-05)**, two constants
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
  *Decision:* judged in the cockpit sitting: (b) passes, the start ramp and the wind-down read
  right against the two clips. (a) is a bug, not a threshold judgement: the slam smoke never
  fires, not even on an idle-to-full slam. Find why `ThrottleSlamSmoke` does not trigger from the
  human throttle before `SlamThreshold` moves at all; `PT-127` (b) then judges the gate.
  *Cross-refs:* `PT-127` (the cockpit sitting that judges both).

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

## Cameras & views

- `BL-266` `[Research]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: decoded]` **Plane wobble: residual decode questions after the
  wiring landed.** *Decision:* judged at the controls against two new clips,
  `OriginalScreenshots/Videos/Dive Wobble.mkv` and `Nitro Wobble.mkv`: the dive rattle and the
  nitro wobble are both too small and janky next to the original's. Decode `high_speed` and
  `nitro`'s drive (the (d) accumulator and the block that carries nitro) and port the mechanism
  before wiring any magnitude; the sawtooth is not the mechanism, as (d) and (e) below already
  say. The oscillators are wired (`ShakeDefs`/`PlaneShake`, visual-only roll on the
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

## HUD & UI

- `BL-113` `[Tuning]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: footage]` **Compass tape**: the bar-end
  caps, the tick height and the tape's edges do not read like the original's. *Decision:* judged
  on a sweep montage against `HUD.png`, `Targeting HUD Kestrel.png` (the renders' own 5120x1440
  viewport) and `C1 IA1 whiteout at height.png`; five things land together, then the goldens
  carrying the tape (`c1-flight-kill`, `c1-cockpit`) re-pin once. (1) No rim cap: all three
  originals have dark bar ends, the "~192 at the very edge" the hud doc reads as a cap is
  `HUD.png`'s sky value, so the two rim draws in `CompassTape._Draw`, `RimGain`, and the hud
  doc's cap sentence go. (2) `TileOverscan` 1.0. (3) The sides darker: the original's outer
  quarter of the bar is close to black, so the plain `cos(Δ)` fade is too gentle at the edges,
  a steeper falloff, judged on a follow-up montage. (4) A black hem under the ticks: the
  original's comb stops about three rows above the bar's bottom edge, ours runs lit to the last
  row. (5) Whether the octant labels foreshorten with the drum: ours are drawn at full width on
  `LabelLayer` ("billboarded upright"), which the hud doc records as verified on one edge `W`,
  and the user reads the originals' edge letters as turning with the card. Measured at 3x on the
  Kestrel shot the edge `E` (Δ about 44°) is about 0.8 of the centre `E` and the edge `S` is
  full width, so the stills do not settle it; render the labels with the ticks' horizontal
  `cos(Δ)` squeeze as one more tile on the follow-up montage and let the user pick. North = −Z
  is confirmed against the original, do not reopen. The nearest-tick look stays as shipped.
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

- `BL-784` `[Feature]` `[L]` `[Next: code]` `[Impact: low]` `[Evidence: decoded]` **The Game Options page drops the original's own Default View and
  Auto Head Turn rows, and neither presentation offers either setting.** *Decision:* a taller
  plate holding four rows, the original's three plus the remake-only Menu row, drawn from the one
  plate image by stretching or tiling and recorded as a remake reading in
  `docs/org/menu-inventory.md`. *Evidence:* the section
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
  *⚠ Traps:* (a) **The plate holds three rows and the port already spends two.** A fourth row at
  the authored 62-pixel pitch from the first row's Y 283 reaches the plaque row at Y 457, so the
  plaque row and everything under it move down by one pitch with the plate; keep the 62-pixel
  pitch rather than squeezing four rows into three rows' height, and pick stretch or tile by which
  survives the plate's border art (`docs/org/menu-inventory.md`'s GameOptions row). (b) **The Default View dropdown's words are not
  decoded.** `docs/org/cameraViews.md` has the options menu labelling camera positions "external"
  (`MSG_OPT_3RD_PERSON`), "cockpit" (`MSG_OPT_COCKPIT`) and "default view" (`MSG_OPT_DEF_VIEW`),
  which is a lead and not `GO_D_VIEW`'s item list, and this port's own views are Chase, Cockpit and
  Nose. (c) A row does not settle autohead's port decision: its default is off because the original's
  cockpit footage reads that way, while `BL-924` records that at the controls the original's cockpit
  view does turn the head and ours does not.
  *Cross-refs:* `BL-924` (the missing automatic head turn), `BL-782` (the same
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
`BL-301` (Dogfight tuning), `BL-314` (race countdown).

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
  `FlightController.RespawnPlacement`). **The match rules are on the menu**: the built-in
  launchscreen's Dogfight map screen carries the kill target and the time limit as two rows under
  the maps, defaulting to the command line's own 5 kills and 5 minutes, with an explicit
  `--vs-kills=`/`--vs-time=` still beating the row it names (`PlayerSetupFeature`,
  `LaunchExit.Match`, `SessionSpec.FromMenu`); Original's Dogfight screen is remake-designed with
  no authored slot for a widget, so it offers neither and launches at the defaults.
  Still open: suicide penalty and last-damager credit (0 /
  none in v1), sudden-death overtime on a drawn time-out (draw declared in v1; `PT-43`(f) found
  draw frequency fine at the 5-kills/5-min defaults, so this stays low priority),
  `dogfight_ace` vs `zeppelin_run` spawn
  spacing, the self-blast exemption (own rockets can't hurt you, the guns invariant applied
  consistently, not a balance call), VS HUD line/arrow sizing at 4-player panes (`PT-43`(d):
  confirmed readable and correctly edge-flipping at both 2 and 4 players, `BL-126` chrome playtest
  2026-08-15, no retune owed; the general HUD text-scale config covers the separate font-size preference). Related,
  not absorbed: `BL-126` (splitscreen chrome, closed 2026-08-15). ⚠ The stunt race's
  abreast starting grid deliberately does **not** touch `--vs`, it is selected only when a race
  exists, so Dogfight walks the scattered `dogfight_ace` list and the rotation keeps it that way.
  Copying the grid over is the wrong reflex: four dogfighters 60 m apart on one heading is an
  instant head-on merge every round.

- `BL-256` `[Feature]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Stunt screenshot feature, triggered off `DzRadius`.**
  *Decision:* designed now, no longer parked. *Fix shape:* when the player's aircraft first
  crosses within `DzRadius` of a `dzN` marker in a stunt run, latch one screenshot of the current
  pane on that frame, play `snd_dangerzone_camera` once per marker, write the image to a
  `screenshots/stunts/` folder beside the saves named by chapter, marker and time, and show a
  thumbnail strip of the run's shots on the stunt scoreboard; one latch per marker per run, no
  re-trigger while the aircraft stays inside the radius. *Playtest after fix:* a C4 stunt run
  through every marker, the sting once each, the strip on the scoreboard, and the files on disk.
  `DzRadius` (15 m, user-hand-tuned) is settled
  as the **marker-centre radius**: scoring crosses the authored `dzpathN` gate pair
  (the archived development log, 2026-08-01 entry "M3 Wave C C9"), and the constant's remaining roles are the `dzN`
  marker centre and the trigger for this screenshot latch.
  The latch plays `snd_dangerzone_camera` (`dangerzone_camera.wav`, a
  data-orphan SFX named by no `SOUND_GROUPS` entry and no world data; the user confirms it is
  the automatic-screenshot sting, not a zone-cleared cue, formerly `BL-090` item 5, closed).
  ⚠ Do not retune or delete `DzRadius` as dead code, it is reserved, and the 15 m is the user's.

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

- `BL-699` `[Perf]` `[Owed-playtest]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: trace]` **The wave's aeroplanes are built in the
  loading screen and the launch frame binds one; whether the hitch a player feels is gone is the
  author's to say.** *Evidence:* the author feels a hitch on every wave spawn in every mission, not
  only CM18's generator launches. The launch frame is measured by `ai-wave-launch-hitch`
  (`CSVM/src/Testing/AiWaveLaunchHitchSuites.cs`), which orders the pool exactly as a launch does,
  holds the load screen's build in a window of its own, then drives C4/M03's `cargozep1` through
  `FlightRoster.SpawnAi` over the mission's own built world and times each sim step on the wall
  clock `HitchMonitor` reads. The median launch frame went from 62.6 ms to 17.0 to 17.8 ms at 1P
  and from 65.6 ms to 17.2 to 18.4 ms at 4P, the cold first launch from 197 ms to 39.6 to 42.1 ms
  at 1P and from 87.4 ms to 19.2 to 19.6 ms at 4P, and the worst frame carrying no launch from
  73.5 ms to 24.5 ms at 1P and from 48.3 ms to 13.0 ms at 4P; five launches take five prepared
  aeroplanes and miss none. The median now sits under `HitchMonitor`'s own 40 ms floor, so the
  suite's bars are regression bars at the new level (median 50 ms, worst warm launch 110 ms, worst
  idle frame 80 ms, each about 1.4x the worst reading over repeated runs, alone and inside the
  sharded engine stage) and the floor is reported beside them rather than asserted: the readings
  that still cross 40 ms are the frames a gen-0 collection lands on, which a bar at the floor would
  fail on (`docs/verification.md` PERF-33).
  *What is in place:* `Session/AiAirframePool.cs` holds one slot per airframe and livery, ordered
  off the generator's own roster block at build time and filled by `GameSession.StepOwedLoad`, one
  aeroplane a frame behind the load screen, so that screen is a real yield of several frames. The
  launch frame keeps only the bind, the loadout and the tree insert. A quiet frame in play refills
  one, never a frame a crash rig or a launch already builds on (PERF-32), which is what covers
  Instant Action's waves and a campaign burst deeper than its block. The crash rig's
  `PrewarmEmitters` is a resumable slice (`AnimRuntime.PrewarmSlice`), so a rig's 194 emitters
  arrive about twelve a frame instead of all on one, and `PlaneCollider` hulls are shared per
  airframe with `PlanePainter` shared per airframe and livery (PERF-22).
  *What still costs:* the refill build is 19 to 28 ms, nearly all of it `PlaneBuilder.Build`'s mesh
  instancing, so one refill still overruns a 60 Hz frame even though it now lands on a frame
  carrying nothing else. Sharing the airframe's immutable mesh parts, or splitting that build along
  its own mesh grain (PERF-25), is what would make a refill fit; until then a wave deeper than its
  ordered block, or two waves inside a second, is where a stutter could still be felt.
  *⚠ Traps:* the pool's claim path produces the same tree an in-place build does, asserted node by
  node by `ai-airframe-pool-claim`; the goldens that fly AI aircraft did not move and must not. The
  spawn index still feeds the spawn jitter (`Rng.NewSystemRandom(Rng.Spawn, index, 0)`) at launch
  and is never allocated ahead, since that moves `c1-flight-kill`. C4/M03 cannot be made to launch
  a wave headlessly: `cargozep1` takes its credit from the mission script's callback 800, which
  `--wake-generators` does not grant, so the suite and not a `--det` probe is the launch-frame
  instrument. The sim clock lags wall time on physics-bound late missions, so read hitch lines in
  sim frames under `--det` (`docs/verification.md` PERF-12/13). The instrument numbers are with
  nobody at the controls; the author's feel report is the acceptance.
  *Playtest after fix:* fly a mission whose waves launch from a generator (C4/M03's `cargozep1`
  after the cargo objective, or CM18) and watch a wave arrive: the arrival frame should pass
  without a stutter, and the seconds between waves too.
  *Cross-refs:* `BL-434` (the per-viewport splitscreen cost the same pass profiled), `BL-657`
  (CM18's generator launching at the wrong time, which is where the measured case is flown).
- `BL-926` `[Fidelity]` `[M]` `[Next: decode]` `[Impact: low]` `[Evidence: decoded]` **A surface
  gunner admits targets on its def's `activation` radius where the original's scorer admits on the
  hull's attack cylinder.** *Evidence:* both decoded scorers refuse a candidate outside the SCORING
  vehicle's attack triple `+0x328`/`+0x32c`/`+0x330` (`docs/org/aiPilot.md` "Every reader of the
  attack and activation triples"), the non-jet scorer `FUN_00421950` included, and that is the one a
  boat, truck or tank hull runs. `SurfaceGunner` hands the ranking the def's `activation` value
  instead, 2,500 m for `patrolboat`, so a hull picks up targets on the wrong volume. The aircraft
  pickers were moved onto the attack radius and this one was left, because CSVM has no attack volume
  to move it to: `vehicle.zrd` gives `patrolboat` and `t_truck` an `activation` and no `attack` and
  no `kind_of`, so the number arrives in the original from the mission roster block or the net, not
  from the def. *Fix shape:* decode where a hull's attack triple is written at spawn
  (`SET_AI_ATTACK_RADIUS` is `FUN_00469f70`, and the roster/net writers are the other candidates),
  then resolve the same value in CSVM and hand it to the ranking. *⚠ Traps:* do not simply swap the
  key. A hull with no resolved attack radius falls to a 0 m reach and every boat and truck in the
  game goes silent, which no suite would catch as a failure since a gunner that never fires still
  passes a scan-shaped check. Add the case that a hull refuses a target past its attack radius and
  still takes one inside it. *Cross-refs:* `BL-789`'s closing commit, which moved the aircraft
  pickers and left this one.
- `BL-927` `[Fidelity]` `[M]` `[Next: decode]` `[Impact: high]` `[Evidence: decoded]` **The AI
  disengage gate ANDs the activation radius with the return range where the original reverts on the
  return cylinder alone.** *Evidence:* the engine's task revert (`FUN_0041d9f0` at `0x0041e6aa`)
  tests the return triple `+0x334`/`+0x338`/`+0x33c` measured from the anchor `+0x348` that
  `FUN_0041f040` writes when the pursuit begins, and it ORs that with the dwell and validity tests;
  it never reads the activation triple, which is the simulation gate measured to the player.
  `AiModeMachine` requires a pursuer to be outside BOTH `ActivationRange` and `ReturnRange` before
  it lays off, and measures from the spawn pose rather than from where the chase started, so a
  `DEDG`-widened member holds a pursuit far past the point the original would recall it.
  *Fix shape:* record the pursuit anchor when pursue is entered, test the return cylinder from it,
  and drop the activation term from the disengage condition. *⚠ Traps:* `BL-565`'s surviving goal
  ("a watched group comes to the player from anywhere on the map") currently rests on the widened
  activation radius keeping an engaged member engaged, so removing that term without the anchor in
  place re-breaks it. The entry gate `min(activation, attack)` is a separate question and is
  deliberately left alone. *Cross-refs:* `BL-565`, `BL-523` (the mode cycle this gate feeds),
  `BL-789`'s closing commit.

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

- `BL-844` `[Cleanup]` `[S]` `[Next: code]` `[Impact: none]` `[Evidence: trace]` **654 em dashes remain inside string literals under `CSVM/src` and `CSVM.Tests`: log messages, CLI notes and HUD text.** *Decision:* swept, a string literal is prose under the writing rule the same as a comment. *Evidence:* the repo-wide sweep rewrote comments and docs and skipped literals, since suites match log lines and a HUD string is a display choice (`VersusHud` draws the glyph for a tie). *Fix shape:* a second pass over literals only, with every suite that matches a rewritten line moved in the same change and the goldens re-pinned where a HUD string changes; `VersusHud`'s tie glyph is a display choice and is rewritten to a word or a different glyph, not left as the one exemption. *Cross-refs:* PLAN-code-review-orch A7.

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
