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

- `BL-060` `[Feature]` `[Blocked: the faithful crash settling]` `[L]` `[Next: code]` `[Impact: low]` `[Evidence: feel]` **Improve on the original crash, the bespoke "breaking apart" (branch `bespoke-crash-animation`).**
  *Decision:* stays parked on its branch; nothing is blended until the faithful recreation is settled.
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

## Weapons & combat

## Flight model & collision physics

## Environment & world

- `BL-272` `[Tuning]` `[M]` `[Next: look]` `[Impact: low]` `[Evidence: footage]` **Precipitation: snow's unit mapping from `weather.json` to a look is invented
  and unjudged; rain reads like the original** (`Precipitation.cs:29-62`, type/tint/rate/density
  are authored; fall speed, box size, particle counts, streak length/width, sway are 16 TUNE
  constants; the sprites themselves are procedural stand-ins for the original's untextured
  line/point primitives, and rain streaking along fall-direction-vs-velocity is a documented
  remake-only rule). Rain reads like the original at the controls under C2B IA1's cloud cover,
  streak width included, so its constants stand. *Owed:* snow, flown against the original or its
  footage in a chapter that authors it; the snow constants are the ones still invented.
  ⚠ `CAP-11`'s C2B take shows the original's rain as one-pixel-wide streaks that the 2560-wide
  Game DVR capture swallows, so a capture of that size is no instrument for streak width.

## Effects & animation runtime

- `BL-674` `[Bug]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` `[CM10]` **CM10's
  attack balloons were seen at the controls appearing on the water, jumping into the sky and
  slowly descending; no headless drive reproduces it.** *Verdict at the controls:* in the original
  the wave arrives from above; in CSVM the balloons sometimes appear on the water, jump into the
  sky, and slowly descend, which is wrong. *What is settled:* the dip to the water is the
  original's authored entrance, not a runtime fault. The SI-script handler `FUN_004ea7d0` poses
  absolutely and holds its sequence until the script's last frame, so `rise` starts from the
  script's end at the water; the definition's own splash events at 55 to 57 s and the patrol boat
  that wakes at the hand-off agree (`docs/org/sequences.md`, "An SI script holds its sequence and
  poses absolutely"). The wave opens at 978 m inside the 970 to 1124 m cloud layer, so it may be
  first seen at its touchdown. `campaign-balloon-marker` pins that profile, and removing
  `ScriptPlayback`'s opening seek reproduces the reported jump exactly, so the suite would catch
  that cause. *Owed:* (1) in the original, watch one wave of CM10 from its wake to its touchdown
  at about 57 s: does it come down to the sea and drop a patrol boat? (2) in CSVM, if the wave is
  seen on the water before it has descended, note the mission time and whether the view was the
  spyglass or a pane; the realtime render path (`RenderPoses` booking a node after its first
  motion tick) is reasoned, not measured on screen. *⚠ Traps:* do not add an altitude floor to the
  assembly or offset the marker upward; the decode makes the dive faithful. *Cross-refs:*
  `BL-656`'s closing commit, `docs/org/targeting.md` "Where a mission structure is".

- `BL-537` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Effect pools at four players, judged in play.** The pool sizes in `CSVM/data/effect_pools.json` were re-judged on a build with no first-use construction cost: rockets and the sonic burst never wrap, a four-object simultaneous death wraps `flame_ball_01` at 4 and 6 slots and is quiet at 8 (now shipped), and seven or more identical deaths in one frame wrap at the 16 ceiling and cannot be sized away. At the controls the single-player half reads right: four fireballs burn out in place, and the seven-death wrap is not visible under the debris. Still owed: a 4-player splitscreen session with everyone firing, judged for anything that reads as shared between panes, and the ceiling for many-player builds (at 16 players the default root wants 19 and gets 16). The instrument is `AnimRuntime.PoolRecycles` and the `anim: effect pool for '<name>' recycled slot` DEBUG line in the log file sink; the sizes staged print on the world-effects build line. ⚠ Raise only a root that logs a recycle, never the default; the three gun roots stay at 1; a root sized 0 clamps to 1. Each slot copies the root's subtree (155 templates at 1 player, 263 at 4).
  *Cross-refs:* `PT-129` (the four-player flight that judges it), `BL-296` (the other splitscreen-scoped item).

## Audio

## Cameras & views

- `BL-266` `[Fidelity]` `[M]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` **Plane wobble:
  re-judge the three kick knobs on the ported rendered rotation.** `PlaneShake` now runs the
  original's seven component blocks as decoded ([`docs/org/shakes.md`](docs/org/shakes.md), "The
  rendered rotation"): every source (gun buzz, the three being-hit sources, dive rattle, contact,
  nitro) is a random velocity kick into its block, and the summed position turns `ShakePivot` as a
  rotation vector at twice its length, roll on the `×2.5` component, pitch and yaw on the other two.
  So the dive and the nitro engage roll about four times what was judged before and move the nose,
  while a gun burst rolls less than the former displacement walk (about 2.7e-3 rad RMS for wep40 at
  8/s, against 6.1e-3; `analysis/gun-wobble-shake/FINDINGS.md`, "The port"). `GunBuzzKickScale`,
  `DiveRattleKickScale` and `NitroWobbleKickScale` are all at 1, the original's own kick.
  *Sortie:* fly a gun plane (`--plane=player_bhawk`) in the chase view over C1 and, side by side
  with `Z:\CSVM\OriginalScreenshots\Videos\Dive Wobble.mkv`, `Nitro Wobble.mkv` and
  `Gun Wobble and animation.mp4`: dive until the speed passes the plane's rated maximum and hold it
  there a few seconds; with a nitrous engine fitted, press `N` at a full tank; hold `Space` for a
  3 s burst in level flight; take gun rounds with `--incoming` and an HE rocket with
  `--incoming=120,wep_06`; scrape the ground or a building once. Then the cockpit view for one
  burst, where the interior turns and the view does not. *Question:* does each knob read right at
  1, and if not, which way and by how much? ⚠ Traps: dial only the three knobs, never
  `magnitude_factor`, `magnitude_quotient` or the nitro `magnitude`, which are decode; a port
  that only rescales the roll leaves the nose still, which the original never does; `SHAKES_CAMERA`
  is not the fire-path mechanism (it routes `wep_26`'s hits to the empty explosion source); wire
  nothing on one coincidence of a magnitude candidate with an authored constant.
- `BL-885` `[Fidelity]` `[S]` `[Next: look]` `[Impact: high]` `[Evidence: decoded]` **The chase camera's settled pose is authored
  (`thirdp_height` + `thirdp_pitch`, 7.57° above the tail and aimed along the nose), where CSVM
  rests at a hand-picked 15.7° aimed ahead of the nose.** The decoded rig is built and runs under
  `--chase-rig=authored`; the default stays `picked` and no golden moves until the look below.
  The decode (`docs/org/cameraViews.md`, "The chase rig"): `FUN_0042c7f0` swings the plane-frame
  vector `(0, thirdp_height·w²·1.0145, 1.0145)` by the head's elevation PLUS `thirdp_pitch`
  (`0042c88d`) and its azimuth (`FUN_0053f550`), and aims the camera along the same swing.
  `thirdp_height` 0.138 is the lift (`atan(0.138)` ≈ 7.9°) and the 0.29° pitch only tilts the rig,
  so the settled camera is 7.57° above the tail (Balmoral 11.11°), not "dead astern at 0.29°".
  `w²` fades the lift as the head swings: the level numpad keys sit at about 3.7° on the flanks and
  0.29° below level nose-on, where the picked rig holds all three at 15.7°.
  *Sortie:* `.\RunGame.ps1 --chapter=C1 --plane=player_bhawk --chase-rig=picked`, then the same
  with `--chase-rig=authored`; fly level and through a few turns in each, then hold `Kp4`, `Kp6`
  and `Kp2`. Compare where the aircraft sits under the reticle and how much ground the frame shows
  against the original's `Z:\CSVM\OriginalScreenshots\C1 IA1 Fog river.png` (a level chase still
  over the C1 river) and the numpad keys against `Z:\CSVM\OriginalScreenshots\Videos\CAP-07 Numpad
  1,2,3,6,9,8,7,4.mp4`. The question: which rig reads as the original's chase camera?
  *If authored:* make it the default and re-pin every chase golden in that commit (every flight
  shot moves). *If picked:* close with the reason, the decoded rig staying behind the flag.
  ⚠ **Do not read the 15.7° as wrong-by-construction**: it was picked to look right and has never
  been judged against the authored figure side by side. ⚠ The authored rig ports the direction and
  aim only; the original's `pos_catch_up`/`look_catch_up` easing of the aircraft frame is not
  ported by either rig, so a hard roll still trails on CSVM's own rates.
  *Cross-refs:* the numpad views' three level keys (`git log --grep=BL-150`),
  `docs/formats/camparam.md` (`thirdp_height`, `thirdp_pitch`, Known limits), `docs/cli.md`
  (`--chase-rig`).

## HUD & UI

- `BL-181` `[Tuning]` `[Owed-playtest]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **The stunt HUD, target marker and stunt
  scoreboard now read one chrome type scale; the fidelity sign-off against it is owed at the
  controls.** The earlier verdict on the stunt run HUD and scoreboard ("work for now") was
  contingent on a shared chrome that did not exist. It exists now: `UI/Boards/ChromeType.cs` is the
  type scale for chrome the original never painted, with one face (the theme default, varied for
  italic and bold), one size ladder (`ChromeSize`: 26, 22, 19, 17, 15, 13, 11, 8 and 6 frame units,
  a frame unit being the board's authored pixel, 1/600 of the frame height) and whole metres for a
  printed distance. The join board is posed on it (its draft 14 folded into 13: the subtitle and the
  P1 to P4 chip tags are one pixel smaller at 720p). `StuntRunHud`, `TargetHud` and `VersusHud` take
  their face and their three sizes from the bottom rungs, pixel-identical at 720p (every golden is
  hash-identical); at 1080p the marker label grows from 10 to 11 px, and at 4K the marker,
  status and banner lines each grow one pixel. `StuntScoreboard` and its `StuntSplits` section (shared with
  Instant Action's wrap-up board) moved onto the ladder: title and context unchanged at 720p,
  column heads 14 to 13, rows 17 to 18, total 23 to 22, best line 16 to 15. No placement changed:
  the HUD's positions hang off the original's own decoded HUD geometry, and the stunt HUD prints no
  distance (the only printed distance is the `--debug-markers` tag). Before/after crops accompany
  the landing commit (`git log --grep=BL-181`).
  *Playtest:* `./RunGame.ps1 --stunt --chapter=C4 --plane=player_fury`, fly a zone or two and read
  the run clock, the intro banner, the zone-cleared flash and the zone marker's label, then finish
  the run (or add `--debug-scoreboard`) and read the scoreboard; then a target HUD case,
  `./RunGame.ps1 --chapter=C1 --plane=player_bhawk --ai=player_kestrel,player_fury`, and read the
  bracket label and the off-screen edge label. Judge the sizes against the join board
  (`--menu=join-board:2 --presentation=original`); pass closes this, a size complaint names the rung.
  *Left open:* Built-in's Start-to-join strip behind `--force-builtin` (`LaunchMenu.cs`) is the
  second join path `BL-951` said must fold into the board's own gesture; folding it means a join
  screen in Built-in, not a small change, so it stays. The rest of the results-board family
  (`VersusBoard`, `StuntRaceBoard`, `IaWrapupBoard`'s own heading, `PauseBoard`, the board menu's
  rows) and `FlightHud`'s top-left text block are still off the ladder. *⚠ Traps:* the composed
  campaign boards and `BoardPalette` are painted original artwork and carry no type scale; do not
  pose them on this one. Do not give the HUD sizes of its own beside the ladder. The marker label's
  rung is bounded by the decoded 15-pixel line pitch, so a larger one runs its three lines together.
  *Cross-refs:* `git log --grep=BL-951`.

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

- `BL-380` `[Bug]` `[M]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **Fog-zone selection stays
  player-1-only in splitscreen: `csky_fog_color`/`_range`/`_alt`/`csky_world_light` are one GLOBAL
  shader uniform set, written from rig 0's camera weather state alone, so a pane on the other side
  of a fog-zone boundary from P1 renders P1's fog, not its own.** The whiteout overlay and the deck
  regime are already per-rig (the same `WeatherRig.Tick` loop), and so is the state itself:
  `Tick` resolves `CameraWeatherState` for every rig's camera. Only the fog writes lag behind:
  `FogStateTrigger.Next` is fed `rigs[0].CameraWeatherState` and `ApplyZone` writes session-wide
  shader globals.
  *Evidence:* the comment above that call in `WeatherRig.Tick`: "Driven by rig 0 because these are
  global shader uniforms, so splitscreen wears rig 0's zone". Pre-existing (`SetupWeather` always
  wrote one global set before splitscreen existed), not introduced by it.
  *Fix shape:* let the shader pick the zone per view. Every view already carries its own
  `CAMERA_POSITION_WORLD`, which `csky_atmosphere.gdshaderinc` receives. Publish as globals a small
  table of each live camera's position (the pane cameras and the spyglass disc cameras) with that
  camera's zone index, and the fog parameters of every zone the mission authors; the fragment
  matches its `CAMERA_POSITION_WORLD` to a table entry and reads that zone's colour, range and
  altitude. The zone choice stays where it is, per rig and edge-triggered on the CPU, so the deck
  rim keeps its no-shimmer rule. The sun and ambient energy `ApplyZone` writes into the shared
  `Environment` are a second half: `Camera3D.Environment` gives each pane its own, unexamined.
  *⚠ Traps:* neither a second `RenderingServer.GlobalShaderParameterSet` call nor an
  `instance uniform` fixes this. A global is one value for the process, and an instance uniform
  is one value per mesh instance, read alike by every pane that draws it. A spyglass disc camera
  sits away from its pane's camera, so the table needs its own entry or the disc reads the wrong
  zone. Any new `instance uniform` in `shaders/csky_instance_uniforms.gdshaderinc` is APPENDED,
  never inserted: Godot assigns instance-uniform slots by declaration order per shader (the file's
  header names the `csky_fog_on` collision this prevents).

- `BL-389` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: feel]` **Splitscreen weapon mix needs a retune: rockets too quiet, guns too loud,
  especially four guns firing at once.** Found at the `BL-126` chrome playtest (F52,
  2026-08-15), `FlightAudio.MixGain`/`Projectile.cs`'s pool `MixGain` (the `1/sqrt(N)` equal-power
  attenuation D31/D32 landed) reads right in isolation but the per-weapon balance under it does
  not: a 4-player Dogfight with simultaneous gunfire is too loud relative to rocket explosions,
  which read as too quiet against it. *Look for:* rocket vs. gun relative level across 2P/4P,
  specifically four guns firing together. *Fix shape:* a judgement call at the controls on the
  per-def volume terms feeding `Projectile.cs`'s `def.Volume * 0.2f * MixGain * distanceGain`
  (line ~2238), not the `1/sqrt(N)` splitscreen term itself, which is confirmed correct.

## Missions, modes & campaign

- `BL-314` `[Feature]` `[L]` `[Next: code]` `[Impact: high]` `[Evidence: feel]` **A network stunt race: a timed, Trackmania-style run over the network,
  started together by a countdown.** Splitscreen needs no countdown, since every pane shares one
  load and nobody gets a head start; the abreast grid (`StartGrid`) reads right at the controls
  there. The case left is a network race, where machines finish loading at different times. Stunt
  mode does not exist over the network yet (the original's stunt mode is single-player Instant
  Action only), so that comes first.

  **The race, as the user designed it.** Everyone starts together at the same point, with no
  collision between the aeroplanes; anyone may respawn as often as they like; when the timer runs
  out, the fastest completion time wins. With no collision a shared start point needs no grid,
  so the network race need not use `StartGrid` at all.

  **The countdown's shape, as decided.** (a) A **rolling start**, not a full freeze: the aircraft stay
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
  4. **The instrument is a hand-flown sitting, not a screenshot.** `--det` cannot reach the race
     path at all, so an automated check can prove only that scripted runs are unchanged. Whether the
     count *feels* like a race start comes from a two-machine sitting.

  *Unlocked by the grid, noted here rather than promised:* race best-times become feasible once a
  race has a defined start (`StuntRace.cs`, `ScoreStore.GetBest`/`RecordIfBest`), and would want
  their own key namespace, since a countdown makes race and solo totals diverge again.

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

- `BL-1018` `[Tuning]` `[S]` `[Next: code]` `[Impact: low]` `[Evidence: trace]` **The guest clock
  slew's window, rate bound and snap threshold are all invented.** *Evidence:* `Net/NetClockSlew.cs`
  walks a guest's offset onto host time over `ConvergeSeconds = 2.0` at no more than
  `MaxRateOffset = 0.10` of real time, and applies a reading more than `SnapSeconds = 5.0` out at
  once. Nothing in the original's networking was decoded for any of the three; they are chosen so a
  correction is invisible over a couple of seconds and a lost link does not leave the guest walking
  for a minute. *Fix shape:* judge them against a real link once a match runs: the window and the
  bound against how a corrected timestamp reads at the controls (an aeroplane's interpolation is
  what shows a clock walking), the threshold against the observed `Snaps` count, which is exposed
  for that reason. A rising `Snaps` says the window or the threshold is wrong, not that the link
  is. *⚠ Traps:* do not raise the rate bound to make convergence quicker; host time running well
  off real time is the thing the walk exists to avoid. *Cross-refs:* `Net/AircraftStateCadence.cs` and
  `Net/RemotePoseBuffer.cs` (send rate and interpolation buffer, judged in the same sitting). The feed now has a live
  reading: `net-match-state` measures a target of 6.000 s and one snap on both guests off the
  ordinary match-state tick, so the threshold can be judged against a real link rather than
  against nothing. `Net/NetClockPing.cs`'s `RetrySteps = 60`, how long an unanswered clock
  question waits before it is asked again, is invented the same way and is judged in the same
  sitting against how often a lossy link leaves the round trip unmeasured.

- `BL-1025` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: trace]` **The host's
  match-state tick rate is a guess at what the clock readout needs.** *Evidence:*
  `Net/MatchStateCadence.cs` repeats the match state every `TickStepInterval = 60` simulation
  steps, one second at the fixed step, chosen because the versus HUD prints whole seconds and a
  faster tick spends the wire on digits nobody sees. Nothing in the original was decoded for it:
  the original's client runs its own countdown and is told only the end. *Fix shape:* judge it on
  a real link with the HUD clock in view. A guest's clock lags the host by up to one tick, so the
  reading is whether the count-down ever visibly jumps or stalls; the same tick is what feeds
  `NetClockSlew`, so `BL-1018`'s window and this rate are judged in one sitting. *⚠ Traps:* the
  ending never waits for this tick (it is sent where it happens), so slowing the rate delays only
  the clock, and the reading must not be taken from a match that ended.

- `BL-1041` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: trace]` **The soak's
  position-error bars are regression tripwires, not what a player accepts.** *Evidence:*
  `Testing/NetSoakSuites.cs` flies a scripted Dogfight through four loopback cells and fails a
  cell whose worse direction exceeds its bar (mean/worst metres): clean 0.25/0.5, 50 ms and 5 per
  cent loss 1.5/3, 100 ms and 10 per cent 2/6, 200 ms and 20 per cent 3.5/10. The seeded run
  measures 0.01/0.01, 0.57/0.89, 0.81/3.03 and 1.43/4.67, so each bar is the measurement with two
  to three times headroom. Nothing says a player notices 3 m of worst error at 200 ms, or that
  1.5 m at 50 ms is fine. *Fix shape:* fly a two-machine match over a shaped link with
  `--debug-net` up, note at which cell a remote aeroplane first reads as wrong (a jump, a lag
  behind its own tracers), and set the bars from that instead. *⚠ Traps:* the error is read after
  the fitted lag is removed, so a large render delay does not show here at all; judge the delay
  (`RemotePoseBuffer.BufferDelaySeconds`) separately. *Cross-refs:* `BL-1018` (the same sitting).

- `BL-1043` `[Tuning]` `[S]` `[Next: look]` `[Impact: low]` `[Evidence: trace]` **The router
  mapping's lease length and renewal fractions are chosen, and its permanent-lease fallback has
  met no real router.** *Evidence:* `Net/UpnpLease.cs` asks `LeaseSeconds = 3600`, renews at
  `RenewAtFraction = 0.5` of the grant and retries a failed renewal after `RetryFraction = 0.125`.
  The hour bounds what a crashed host leaves open; the fractions leave room for three retries,
  each paying a whole gateway search, before the lease runs out, which `UpnpLeaseTests` asserts.
  Every test runs over a fake gateway, so no router has yet answered a finite lease, error 725
  (permanent leases only) or a delete of a stale mapping. *Fix shape:* host through a home router
  with UPnP on, read the `upnp mapping` and `upnp renewal` log lines and the router's own mapping
  table across more than one renewal, then kill the process and confirm the next host's stale
  clear removes the entry. Shorten the lease if routers keep stale entries visibly long. *⚠ Traps:*
  never widen the stale clear past the exact remembered port; a range delete would take another
  program's mapping on the same router.
