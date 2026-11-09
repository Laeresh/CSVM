# Volumetric clouds and global illumination

**ACTIVE PLAN** (written 2026-11-09). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

This plan takes two prototypes on branch `ccr-a18ce897-jqi2mk` out of prototype state.
- `--proto-clouds` (`CSVM/src/Effects/ProtoClouds.cs`) is the raymarched slab cloud layer, with its ground shadow, god rays and held zone.
- `--proto-gi` (`CSVM/src/Effects/ProtoGi.cs`) is Godot's SDFGI and SSIL over the Enhanced world.

Both become Enhanced-only video options that switch live. Each gets its own lighting tune layer,
and the labs can turn both on and off so tuning needs no restart. The prototype's lab sliders become
saved tune fields.

Out of scope:
- **New skyboxes** for the rifts and for the sky above the held zone. The option ships with the held zone's dome, and skyboxes are a follow-up.
- **The boxes and shells variants.** They are deleted, not finished.
- **A finer erosion octave** for the cloud surfaces.

No `backlog` issue feeds this plan. Its items come from this session's playtest of the prototypes and
the author's decisions below.

## Milestone goal

- Video options carry "Volumetric Clouds" and "Global Illumination" checkboxes, Enhanced only, saved
  with the other settings. They switch live in flight without a restart.
- The global illumination option is SDFGI plus SSIL, one mode.
- The cloud lab turns the volumetric clouds on and off, and the lighting lab turns GI on and off,
  live, through the same path as the options.
- `lighting.json` carries a `clouds` layer and a `gi` layer per zone. Each applies over the zone's tune
  only while its option is on, and the lighting lab edits and saves the layer that is active.
- The prototype's live sliders save:
  - the head taper, deck sun taken and deck ambient taken in the cloud tune;
  - god-ray intensity, terrain shadow strength, SDFGI energy and the ambient floor in the lighting layers.
- The prototype scaffolding is gone: the variants, Shift+F6, Shift+F7, the `Proto` names and the
  "PROTOTYPE, throwaway" docs.
- With both options off, the faithful path and the Enhanced goldens are unchanged.

**The original zones stay authoritative with the option off.** The volumetric clouds hold the zone and
turn night off, but only while the option is on; nothing about the zone system changes for players
who leave it off.

## Decisions (2026-11-09)

| # | Question | Decision |
|---|---|---|
| 1 | Which cloud variant ships? | **The slab.** Boxes and shells look worse at the controls; both are deleted. |
| 2 | Does the cloud option get its own lighting tune? | **Yes, a `clouds` layer per zone.** It holds the zone and turns night off, so C1B and C5 light as day under it and need their own tune. |
| 3 | What mode does the GI checkbox pick? | **SDFGI plus SSIL** (the prototype's `both`). |
| 4 | Where do the switches live? | **Video options, Enhanced only, one checkbox each.** |
| 5 | Can the labs switch them? | **Yes, live.** The cloud lab toggles the clouds and the lighting lab toggles GI, so tuning needs no restart. |
| 6 | Is a per-player cloud option fair in multiplayer, given it holds the zone (no zone switch, no whiteout)? | **Accepted.** The advantage of not seeing the whiteout is small. |
| 7 | Night or day under the clouds? | **A tuning matter.** `WeatherRig.NightOff` holds while the clouds are on; darkness comes from the `clouds` layer, not the night flag. |
| 8 | What about the prototype's live lab sliders? | **They move into the real labs and save.** |
| 9 | What does a placeholder band (10 km, or C2's 19 km) draw? | **It draws a layer fitted to the placed clouds' heights, or nothing when none are placed (C3, C5).** C5's fvol fog banks are not a layer, and its fog field stays drawn. |
| 10 | Does a lab toggle save the video option? | **No, it switches for that flight only.** |
| 11 | Both layers on: in which order? | **`gi` applies after `clouds`.** |
| 12 | Where does terrain and object shadow strength live? | **The base zone tune**, not `clouds`: C1B's caves need it with the option off too. |
| 13 | Goldens with the options on? | **No.** |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The terrain shadow bake drew the inverted ground shadow (lit under cover, dark in the rifts). | A brute-force ray cast against a `--dump-terrain-shadow` PNG agreed with the bake on 99.7% of texels. The cause was the ground's deck term reading `1 - gaps`, but `csky_pc_deck_gaps` returns the share of deck **kept**. The fix is `1 - y * gaps` in `csky_pc_deck_sun` (`CSVM/shaders/csky_cloud_shadow.gdshaderinc`). |
| 2 | `--terrain-shadows=off` is an A/B for the cloud shadows on the ground. | It also drops every cloud shadow, which the terrain's `light()` path draws. Use the cloud lab's tint toggle (red heads, blue deck) to read the shadows instead. |
| 3 | Aiming the sun at the drawn moon card fixes the shadow bearing. | It gave the same bearing; the change was reverted. |
| 4 | C5's fvol volumes can stand in for its cloud layer. | They are ground fog banks reaching down to -463 m, so a slab fitted to them buried the terrain in cloud. |
| 5 | Shift+F6's freeze is the march cost. | Each material compiled its own shader (1077 compiles). One cached `Shader` per code fixed it (`ProtoClouds._shaders`). |

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the mistake this project makes most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + the module's entry in `docs/architecture/<Namespace>.md` (plus its index
  bullet in `docs/architecture.md`) / `docs/formats/` are updated in the same turn** as each landed
  item; a landed item gets its record in the landing commit's message and its issue is **closed**. New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything**, the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture/<Namespace>.md` (found through the index in
  `docs/architecture.md`) before modifying it,** then the comments on the members you touch; dead
  ends are in the landing commits (`git log --grep=<ID>`), so search those before re-chasing one.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A, baseline

1. ☑ Commit the prototype's playtest fixes as the plan's baseline

### Wave B, options, live switches and tune layers

11. ☐ Two Enhanced-only video options: Volumetric Clouds and Global Illumination
12. ☐ Both options switch live in flight
13. ☐ The cloud lab and the lighting lab toggle the options live
14. ☐ `clouds` and `gi` lighting layers in `lighting.json`, edited by the lighting lab
15. ☐ The prototype's live sliders become saved tune fields

### Wave C, remove the prototype scaffolding

21. ☐ Delete boxes, shells, Shift+F6 and Shift+F7; rename out of `Proto`

### Wave D, defects and cost

31. ☐ The slab draws over the rain
32. ☑ C2's horizon under the clouds (author's check)
33. ☐ `TerrainShadow.SunAt` ignores the shadow strength
34. ☐ Frame cost of the slab and SDFGI, desktop and Steam Deck, and the option defaults

### Wave E, tuning and landing

41. ☐ Per-chapter tune of the `clouds` and `gi` layers (author, at the labs)
42. ☐ Tests, docs and the full battery

## Dependency and parallelism notes

- **A1 comes first.** Every later item builds on the committed prototype.
- **B11 and B12 come before B13.** The labs call the same live path the options use.
- **B14 comes before B15**, because the lighting sliders save into its layers.
- **C21 can run in parallel with Wave B**, but it renames the files Wave B edits (`ProtoClouds.cs`, `ProtoGi.cs`, `GameSession.cs`). Either land C21 first, or run it after Wave B; never in a parallel worktree.
- **D31 to D34 are independent of each other.** D34's numbers decide B11's defaults, so measure before B11 lands, or land B11 defaulting to off.
- **E41 needs B13 to B15** (live toggles and saving layers).
- **E42 closes the plan.**

---

# Wave A, baseline

## A1 ☑ Commit the prototype's playtest fixes as the plan's baseline

**Landed.** The prototype's playtest fixes are committed on `ccr-a18ce897-jqi2mk`; the record is in the commit message.

**Verified.** <pending orchestrator run: the full battery runs on the tree after main is merged in.>

**Original approach (kept for reference).**

**Goal.** The session's uncommitted prototype work stands as a commit on `ccr-a18ce897-jqi2mk`, with
the format gate and battery green.

**Evidence (confidence: traced).** `git status` in the worktree. The changes span:
- `ProtoClouds.cs`, `ProtoGi.cs` and `TerrainShadow.cs`;
- `CloudShadow.cs`, `WeatherRig.cs`, `GameSession.cs` and `Launcher.cs`;
- `CloudLab.cs` and `LightingLab.cs`;
- the shaders `csky_cloud_shadow`, `csky_proto_clouds`, `csky_atmosphere` and `csky_terrain_shadow` (`.gdshaderinc`);
- `docs/controls.md`.

A full `-t:Rebuild` was StyleCop clean before the last two `FitLayerToSprites` edits.

**Approach.** Run `.\scripts\RunTests.ps1` from the worktree (the format gate runs first), then commit
with a message listing the playtest fixes:
- the shader cache;
- the held zone and night off;
- the deck shadow inversion;
- the on-demand terrain bake;
- the shadow strength and ambient floor;
- the placeholder band fitting;
- the C2 haze.

**Model recommendation.** medium, low effort: mechanical gate and commit.

**Verify.** RunTests green; `git status` clean afterwards.

**⚠ Traps.** The comment-cap hook (25 words per sentence) and StyleCop member order have both bitten
this branch; the format gate reports them before the commit.

# Wave B, options, live switches and tune layers

## B11 ☐ Two Enhanced-only video options: Volumetric Clouds and Global Illumination

**Goal.** The video page shows "Volumetric Clouds" and "Global Illumination" checkboxes under Enhanced
Graphics. They are saved with the other settings and replace `--proto-clouds` and `--proto-gi` as
the way a player turns them on.

**Evidence (confidence: traced).** "Ocean Waves" is an Enhanced-only `Check` row in
`CSVM/src/UI/Menu/Original/OriginalVideoPage.cs:177`. It is backed by `CSVM/src/Utils/WaterQualitySetting.cs`,
registered in `CSVM/src/Utils/WordSettings.cs:22`, and applied live by
`EnhancedLook.ApplyWaterQuality` (`CSVM/src/Launch/EnhancedLook.cs:236`). `ProtoGi.Configure` reads the
flag in `CSVM/src/Launch/Launcher.cs:1490`.

**Approach.** Add two setting classes on the `WaterQualitySetting` pattern and register them in
`WordSettings`. Then add two `Check` rows beside Ocean Waves. Keep the command-line flags as overrides,
the way the other settings resolve a flag over the saved word.
<TODO: the flags' new names (keep `--proto-*`, or `--volumetric-clouds` / `--gi`) and their docs/cli.md bullets.>
<TODO: defaults; off unless D34 says otherwise.>

**Model recommendation.** medium: follows an existing setting end to end.

**Verify.** The settings round-trip test the other word settings have. A launch with each option
saved on logs the cloud and GI lines, and with both off it logs neither.
<TODO: name the existing settings suite to extend.>

**⚠ Traps.** The options must do nothing under faithful graphics. `ProtoGi.Apply` already gates on
`enhanced`; the cloud build gates on `GraphicsMode.Enhanced` through `ProtoClouds.Follow`.

## B12 ☐ Both options switch live in flight

**Goal.** Toggling either option mid-flight builds or removes the effect at once, with the zone,
night, fog and lighting restored exactly when the clouds go off.

**Evidence (confidence: traced).**
- **Clouds:** `ProtoClouds.Follow(bool)` (`CSVM/src/Effects/ProtoClouds.cs:223`) already builds, or frees and restores. `Restore` resets `HoldZone` and `NightOff` and reapplies the zone. But the node is created only at session build (`GameSession.BuildProtoClouds`, `CSVM/src/Launch/GameSession.cs:2847`). The held zone is also set before the terrain bake there, so the bake keys the held sun bearing.
- **GI:** `GameSession.CycleProtoGi` (`GameSession.cs:2831`) re-applies `EnhancedLook` to the world and cockpit environments, then the zone. That is the live path.

**Approach.**
- **Clouds:**
  - Build the clouds node whenever the world is Enhanced. Let the option drive `Follow`, the way the graphics switch does.
  - Hold the zone through the option rather than at build.
  - The on-demand terrain bake (`TerrainShadow._Process`) covers the bearing change on a live toggle.
- **GI:** turn `CycleProtoGi` into the option's setter, with the mode fixed at SDFGI plus SSIL (Decision 3).

**Model recommendation.** high: the zone, night, fog and bake interplay has broken several times on this branch.

**Verify.** In C1C and C1B, toggle the clouds on, off and on in flight.
- The log shows `proto clouds: zone held, night off` and then the restore.
- The terrain shadow logs `baked on demand` when the bearing changes.
- With the option off, the zone switch and whiteout return; compare against a launch with the option off.
<TODO: a suite that toggles both and checks the globals return to their off values.>

**⚠ Traps.**
- `WeatherRig.NightOff` is a static. It must be reset on every path that tears the clouds down, a session exit included.
- The held zone changes the sun bearing. The terrain bake must have the new bearing's key, or the ground loses its shadows until the on-demand bake settles (0.25 s).

## B13 ☐ The cloud lab and the lighting lab toggle the options live

**Goal.** The cloud lab (Shift+F4) has a Volumetric Clouds checkbox, and the lighting lab (Shift+F5) a
Global Illumination checkbox. Each switches the effect live through B12's path.

**Evidence (confidence: lead-only).** This is the author's request (Decision 5). Both labs already carry live sliders, for example the lighting lab's
SDFGI energy, which sets `env.SdfgiEnergy`.

**Approach.** Each lab gets a checkbox that calls B12's setter. The toggle is live for that flight
only and does not save the video option (Decision 10).

**Model recommendation.** medium.

**Verify.** At the controls in C1C:
- the lab checkbox toggles the effect without a restart;
- the lighting lab's sliders then edit the layer that matches the new state (B14).

**⚠ Traps.** After a toggle, the lighting lab must re-read which layer is active, or a save writes the
wrong layer.

## B14 ☐ `clouds` and `gi` lighting layers in `lighting.json`, edited by the lighting lab

**Goal.**
- **The data:** each chapter, mission and zone block in `lighting.json` may carry a `clouds` block and a `gi` block. Each applies over that zone's tune only while its option is on.
- **The lab:** the lighting lab shows which layer it edits and saves into that layer.

**Evidence (confidence: traced for the existing layering, lead-only for the new one).**
`LightingTable` (`CSVM/src/Session/World/LightingTable.cs:12-20`) already layers a mission block over its
chapter and a chapter block over the defaults. Each block holds only the fields that differ, and the
lighting lab writes through `WithZone`, keeping every other key and its order.

**Approach.** Parse `clouds` and `gi` as sparse `ZoneLighting` overlays inside a zone block, applied
after the zone's own fields. With both on, `gi` applies after `clouds` (Decision 11), so the lab
edits `gi` while GI is on and `clouds` while only the clouds are. Extend `WithZone` to write into
the active overlay.

**Model recommendation.** high: merge semantics on a hand-edited data file that every chapter reads.

**Verify.** `LightingTable` unit tests:
- an overlay parses;
- it applies only when its option is on;
- `WithZone` round-trips without reordering other keys;
- a malformed overlay is a warning, not a fatal error.

A live check that saving in the lab with GI on leaves the base zone block unchanged.

**⚠ Traps.** `WeatherRig.WriteColorAmbient` multiplies the ambient by `ProtoGi.AmbientFloor` while SDFGI
stands (`ProtoGi.SkyLightLive`). The floor becomes a `gi` field, not a second multiplier on top of it.

## B15 ☐ The prototype's live sliders become saved tune fields

**Goal.** The prototype's live-only sliders save, per chapter:
- the cloud lab's head taper, deck sun taken and deck ambient taken;
- the lighting lab's god-ray intensity, terrain and object shadow strength, ambient floor under SDFGI and SDFGI energy.

**Evidence (confidence: traced).**
- **Cloud lab:** it saves through `CloudShadowChapters.Save` (`CSVM/src/UI/Labs/CloudLab.cs:120`). The three cloud sliders use `ProtoClouds.LabSlider`, which is live and unsaved, over the statics `Taper`, `DeckSunTaken` and `DeckAmbientTaken`.
- **Lighting lab:** its four sliders write `ProtoClouds.ShaftScale`, `WeatherRig.ShadowStrength`, `ProtoGi.AmbientFloor` and `ProtoGi.Energy`.

**Approach.**
- **The cloud fields** join the chapter's cloud tune beside coverage.
- **The lighting fields:**
  - god rays go in `clouds`;
  - the ambient floor and SDFGI energy go in `gi`;
  - shadow strength goes in the base zone tune (Decision 12).
- Delete `LabSlider` once nothing uses it.

**Model recommendation.** medium.

**Verify.** Move each slider, save, relaunch and see the value return. The log's tune line names the field.

**⚠ Traps.** `TerrainShadow.SunAt` (the CPU twin) does not read the strength yet (D33). Anything that samples it will disagree with the shader until D33 lands.

# Wave C, remove the prototype scaffolding

## C21 ☐ Delete boxes, shells, Shift+F6 and Shift+F7; rename out of `Proto`

**Goal.** Only the slab remains, and the files no longer use the prototype names: `ProtoClouds` becomes, for example, `VolumetricClouds`, and `ProtoGi` becomes `GlobalIllumination`. No Shift+F6 or Shift+F7 handler remains, and the "PROTOTYPE, throwaway" summaries are gone.

**Evidence (confidence: traced).**
- **The variant cycle:** `ProtoClouds._UnhandledInput` cycles variants on Shift+F6 (`ProtoClouds.cs:201`).
- **The GI cycle:** `GameSession` handles Shift+F7 at `GameSession.cs:1042`.
- **The variant code:** boxes and shells live in `ProtoClouds.cs` (the `BoxesShader`, `AddBox`, `PuffMap` and shells builders).
- **The shader include:** `CSVM/shaders/csky_proto_clouds.gdshaderinc` holds the slab's march helpers.
- **The docs:** `docs/controls.md` has the Shift+F7 row.

**Approach.**
- Delete the variant enum and the boxes and shells code.
- Delete both key handlers and the `Cycle` method.
- Rename the classes, the files with their `.uid` sidecars, and the shader include.
- Rename the `csky_pc_*` globals only if the rename is cheap.
- Update `docs/controls.md`, `docs/cli.md` and the architecture entries.

<TODO: whether `csky_pc_*` global names are renamed (touches every shader include and RegisterGlobals).>

**Model recommendation.** medium: wide but mechanical, with the `.uid` sidecar and the include-order hazards.

**Verify.**
- Full 8-chapter `--freecam` regression with the clouds option on and off: zero shader errors, and the same mesh/node counts off.
- `CheckUidSidecars.ps1` passes.

**⚠ Traps.**
- **Registration order:** every `global uniform` must be registered before any shader that reads it compiles (`CloudShadow.RegisterGlobals`, `Launcher`).
- **Include guards:** a renamed include keeps its guard.
- **Headless shader errors:** a headless shader error shows its lines in the shard's `.log.out` (`E  39->`).

# Wave D, defects and cost

## D31 ☐ The slab draws over the rain

**Goal.** Rain in front of the cloud layer draws over it.

**Evidence (confidence: lead-only).** The slab's full-screen quad draws at render priority 120. The
rain was not checked against it in a live run.

**Approach.** <TODO: the rain's draw order and depth; read the rain module's architecture entry first.>

**Model recommendation.** medium.

**Verify.** <TODO: a chapter with rain under a cloud layer, captured with the option on.>

## D32 ☑ C2's horizon under the clouds (author's check)

**Verified.** The author flew C2 with the clouds on: the horizon reads right, with no grey-blue band. The fix landed in A1.

**Original approach (kept for reference).**

**Goal.** C2's horizon shows no grey-blue band, and the far clouds fade into the fog.

**Evidence (confidence: lead-only).**
- The cause was the fog split applying ZONE2's 9 km in-cloud fog above a layer fitted to C2's placed clouds at 986-3456 m, plus a hard fill at the 24 km march cap.
- The fix:
  - fitted layers skip the split;
  - the slab adds an 8-24 km distance haze;
  - the fill is weighted by the layer length past the cap.
- The probe compiled cleanly, but its free camera faced the ground, so no frame shows the horizon.

**Approach.** The author flies `.\RunDev.ps1 --freecam --chapter=C2 --graphics=enhanced --proto-clouds`
and judges the horizon. If far clouds read wrong, `haze_near_m` (8 km) becomes a cloud lab slider.

**Model recommendation.** none: the author's eyes.

**Verify.** The author's screenshot at the earlier report's spot.

## D33 ☐ `TerrainShadow.SunAt` ignores the shadow strength

**Goal.** The CPU twin of the baked terrain shadow agrees with the shader at every strength.

**Evidence (confidence: traced).** `csky_terrain_shadow.gdshaderinc` computes the baked sun as
`1.0 - csky_terrain_shadow_strength * clamp(...)`; `TerrainShadow.SunAt` was not changed with it.

**Approach.** Apply the same strength in `SunAt`.
<TODO: list `SunAt`'s callers, to know what the disagreement reaches.>

**Model recommendation.** medium, low effort.

**Verify.** A unit check that `SunAt` matches the shader formula at strengths 0, 0.5 and 1.

## D34 ☐ Frame cost of the slab and SDFGI, desktop and Steam Deck, and the option defaults

**Goal.** Measured frame times for each option on and off on the author's desktop and on the Steam
Deck, and from them the defaults: on, off, or tied to a quality setting.

**Evidence (confidence: lead-only).**
- SDFGI runs 8 cascades at a 1.5 m finest cell (`ProtoGi.cs:18`).
- The slab is one full-screen march.
- Neither has been measured.

**Approach.** <TODO: the measuring instrument; read `docs/verification.md` for the rule on frame
timing, and the Deck notes (`--det`, the held ssh session).>

**Model recommendation.** medium.

**Verify.** <TODO: the numbers and the chapters and poses they were taken at.>

**⚠ Traps.** The sim clock lags wall time on the author's rig in late C2. Read the sim-clock cadence, not `physics_ms`.

# Wave E, tuning and landing

## E41 ☐ Per-chapter tune of the `clouds` and `gi` layers (author, at the labs)

**Goal.** Every chapter reads right with the clouds on, with GI on, and with both on. C1B and C5,
dark in the original, are tuned dark through the `clouds` layer (Decision 7).

**Evidence (confidence: lead-only).**
- Night zones under the original flag: C1B (both zones), C5 ZONE1 and ZONE3, and C3 ZONE2 (in-cloud, unflown).
- Under the clouds, `NightOff` lights them as day until tuned.

**Approach.** The author flies each chapter with the labs, toggling the options live (B13), and saves.

**Model recommendation.** none: the author's judgement.

**Verify.** The author's sign-off per chapter. No goldens with the options on (Decision 13).

## E42 ☐ Tests, docs and the full battery

**Goal.** The plan lands with its tests, architecture entries and docs, and the full battery is green.

**Approach.**
- **Architecture:** entries for the renamed modules in `docs/architecture/Effects.md` (check the file name), with their index bullets.
- **Docs:** `docs/cli.md` for the flags and `docs/controls.md` without Shift+F6 or Shift+F7.
- **The battery:** run `.\scripts\RunTests.ps1` complete.
- **Goldens:** the Enhanced goldens must not move with both options off.

**Model recommendation.** medium.

**Verify.** RunTests green, and `CheckDocEntries.ps1` passes.
