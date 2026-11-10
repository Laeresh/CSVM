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
| 14 | The slab costs 4-9 ms of GPU at 1080p on an RTX 5080 (D34). Ship it as is? | **No: march at reduced resolution first (D35)**, judged by the author against full resolution. |
| 15 | The options' defaults? | **Clouds off; GI on, off through the existing integrated-GPU fallback** (D36), pending the Deck half of D34. |

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

11. ☑ Two Enhanced-only video options: Volumetric Clouds and Global Illumination
12. ☑ Both options switch live in flight
13. ☑ The cloud lab and the lighting lab toggle the options live
14. ☑ `clouds` and `gi` lighting layers in `lighting.json`, edited by the lighting lab
15. ☑ The prototype's live sliders become saved tune fields

### Wave C, remove the prototype scaffolding

21. ☑ Delete boxes, shells, Shift+F6 and Shift+F7; rename out of `Proto`

### Wave D, defects and cost

31. ☑ The slab draws over the rain
32. ☑ C2's horizon under the clouds (author's check)
33. ☑ `TerrainShadow.SunAt` ignores the shadow strength
34. ☑ Frame cost of the slab and SDFGI, desktop and Steam Deck, and the option defaults (Deck half deferred)
35. ☐ March the clouds at reduced resolution
36. ☐ Option defaults: clouds off, GI on with the integrated-GPU fallback

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

**Verified.** The full battery on the plan tree with main merged in, A1, C21 and D33 landed: build, units 8411 passed / 0 failed / 6 skipped, engine 674 passed / 0 failed / 2 skipped with engine errors clean, goldens 41 shots hash-identical.

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

## B11 ☑ Two Enhanced-only video options: Volumetric Clouds and Global Illumination

**Landed.** `Utils/VolumetricCloudsSetting.cs` and `Utils/GlobalIlluminationSetting.cs` are registered word settings, off by default, resolved from `--volumetric-clouds=on|off` or `--global-illumination=on|off`, the saved word, the `graphics.*` key, then `off`, with `--det` keeping only the flag; the `--proto-*` flags are gone. The VIDEO page shows them as Off/On dropdowns under Post-processing, since the page's two authored checkbox lines are taken by Ocean Waves and Enhanced Graphics.

**Verified.** The full battery on the plan tree with B11 to B13: build, units 8418 passed / 0 failed, goldens 41 hash-identical, engine 674 passed with one red, `campaign-coop-hangar-owner` (ObjectDisposedException on a `Node3D`, no stack). It passed alone three times and twice in its shard's exact order, so it is intermittent; it also failed once before B11 existed. It stays on the watch-list for E42's battery. `menu-original-tracer-core` walks the VIDEO page to Top 10.

**Original approach (kept for reference).**

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

## B12 ☑ Both options switch live in flight

**Landed.** Each setting's live-apply hook follows the session (`EnhancedLook.FollowWordSettings`): `GameSession.FollowVolumetricClouds` builds the clouds node or drops it, whose exit restores the zone, night, fog and deck, and `FollowGlobalIllumination` rewrites GI on the world and cockpit Environments, then the zone. The zone is held before the bake only for a build with the option on, the session's exit clears `WeatherRig.NightOff`, and the `graphics-volumetric-clouds-gi` suite toggles both live.

**Verified.** `graphics-volumetric-clouds-gi` passes in the full battery (B11's line): C1C's clouds on, off, on, off with the hold, night-off, deck and ground globals following, GI on and off with SDFGI and SSIL following. C1C probes with both on log both lines with zero shader errors; both off match a no-flag probe's node and mesh counts.

**Original approach (kept for reference).**

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

## B13 ☑ The cloud lab and the lighting lab toggle the options live

**Landed.** The cloud lab has a Volumetric Clouds box and the lighting lab a Global Illumination box, each calling B12's follow after `TakeUnsaved`, which moves the run's word and never the options file. The session's exit puts the resolved word back (`WordSetting.Retake`), so the toggle lasts the flight (Decision 10).

**Verified.** Units cover the unsaved word and its `Retake` (8418 passed in the battery). The boxes' feel at the controls is the author's check in E41.

**Original approach (kept for reference).**

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

## B14 ☑ `clouds` and `gi` lighting layers in `lighting.json`, edited by the lighting lab

**Landed.** Any zone block may carry sparse `clouds` and `gi` layers, resolved chapter then mission over the base tune while each option's word stands on, `gi` after `clouds` (`LightingTable.ActiveLayers`, `For`), and `WeatherRig.ReapplyZone` drops its cached tunes so a live toggle takes the right layers. The lighting lab names the layer it edits under its title, re-reads it when a box flips, the panel opens or a Save starts (dropping unsaved edits made over the other layer), and `WithZone` writes into that layer only what differs from the tune beneath it, keeping the base fields and the other layer in place.

**Verified.** Full battery on the plan tree with B14 and B15: units 8422 passed, engine 675 passed with engine errors clean, goldens 41 hash-identical. `lighting-lab` saves with GI on and finds only `gi: {sdfgi_energy 0.9}` beside an unchanged base block. C1C probes with both options off match a no-flag probe byte for byte.

**Original approach (kept for reference).**

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

## B15 ☑ The prototype's live sliders become saved tune fields

**Landed.** The cloud tune gains `head_taper`, `deck_sun` and `deck_ambient`, saved by the cloud lab and read by the volumetric clouds from `CloudShadow.Live`. `ZoneLighting` gains `shadow_strength` (base), `god_rays`, `ambient_floor` and `sdfgi_energy`, applied on every zone apply in `WeatherRig`, so `VolumetricClouds.LabSlider` and the unsaved statics are gone and the defaults reproduce the prototype's values.

**Verified.** The same battery. A round trip written into the data files (base `shadow_strength` 0.75, `gi` `sdfgi_energy` 0.65, cloud `head_taper` 0.55) read back after a relaunch, `sdfgi_energy` only while GI is on. A lab Save then relaunch is the author's check in E41.

**Original approach (kept for reference).**

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

**⚠ Traps.** Shadow strength is set through `WeatherRig.ShadowStrength`, which writes `TerrainShadow.Strength` (D33). Never write the `csky_terrain_shadow_strength` global directly, or `SunAt` falls out of step with the shader.

# Wave C, remove the prototype scaffolding

## C21 ☑ Delete boxes, shells, Shift+F6 and Shift+F7; rename out of `Proto`

**Landed.** Only the slab remains, as `Session/World/VolumetricClouds.cs` over `shaders/csky_volumetric_clouds.gdshaderinc`, and GI is `Effects/GlobalIllumination.cs`, fixed at SDFGI plus SSIL and switched on or off. The boxes, the shells and both key handlers are gone, the `--proto-*` flags are plain switches until B11, and the clouds moved to `Session.World` because they drive `WeatherRig`, which `FamilyOrderTests` forbids from `Effects`.

**Verified.** 8-chapter `--freecam --graphics=enhanced` sweep with the clouds on: zero shader errors, every chapter's layer as before the rename (C3 and C5 draw none); C1 with the clouds off keeps its node and mesh counts. The full battery is green (A1's line).

**Original approach (kept for reference).**

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

## D31 ☑ The slab draws over the rain

**Landed.** The claim held. The rain (`Effects/Precipitation.cs`) blends with no depth write at priority 0, and the slab drew at 120 with no depth test, reading opaque depth alone, so it painted the cloud over every streak in front of it. `Precipitation.DrawPriority` (105, the world flare's 100 plus 5) now orders the rain, and the slab draws one before it at 104. The slab stays after the world's transparents (all at 30 or below) and the flare's four quads (100 to 103), so the cloud still covers the sun. In flat mode nothing else sits between 104 and 120. In stereo the slab and the rain now draw under the head-locked tiers (whiteout 109, interior glass 110, flash 111, markers 112, HUD panel 115 and up); at 120 the slab painted cloud over the HUD panel. That stereo order is not checked in a headset. Wind streaks (`Effects/WindStreaks.cs`, priority 0, off by default) are still covered by the slab.

**Verified.** The orchestrator viewed the before and after shots and confirms them; the full battery runs in E42. Probe in C1C/M01 (layer 1055-1955 m, rain under 1055 m), `--freecam --graphics=enhanced --volumetric-clouds=on '--pos=-3694,800,-12482' '--direction=0,0.6,-1'`: with the old order the deck's underside shows no streaks, with the fix the streaks stand over it, and with the clouds off the same streaks fall in front of a flat grey sky. The pose looks up from under the base, so it does not show rain against a distant cloud at the horizon or the fog fill below it.

**Original approach (kept for reference).**

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

## D33 ☑ `TerrainShadow.SunAt` ignores the shadow strength

**Landed.** `TerrainShadow.Strength` is the one owner of `csky_terrain_shadow_strength`: `WeatherRig.ShadowStrength` sets it, it writes the global and keeps the value, since the renderer reads no global back. `SunAt` passes it to `TerrainShadowRaster.Lit`, which now computes `1 - strength * clamp(...)` as the shader does, and `TerrainShadowRasterTests` checks that at strengths 0, 0.5 and 1.

**Verified.** The new theory passes, and the full battery is green with the `graphics-terrain-shadow` suite (`SunAt`'s only caller) passing (A1's line).

**Original approach (kept for reference).**

**Goal.** The CPU twin of the baked terrain shadow agrees with the shader at every strength.

**Evidence (confidence: traced).** `csky_terrain_shadow.gdshaderinc` computes the baked sun as
`1.0 - csky_terrain_shadow_strength * clamp(...)`; `TerrainShadow.SunAt` was not changed with it.

**Approach.** Apply the same strength in `SunAt`.
`SunAt` has one caller, the `graphics-terrain-shadow` suite (`GraphicsSwitchSuites.TerrainShadowBakes`), which reads a cliff's waterline as shaded and the open sea as lit. No gameplay or rendering code samples it, so the disagreement reached only that suite, and only with the lighting lab's slider moved below 1.

**Model recommendation.** medium, low effort.

**Verify.** A unit check that `SunAt` matches the shader formula at strengths 0, 0.5 and 1.

## D34 ☑ Frame cost of the slab and SDFGI, desktop and Steam Deck, and the option defaults

**Landed.** The desktop half is measured (**Measured (desktop).** below) and the defaults follow from it (Decisions 14 and 15, D35, D36). The Steam Deck half is deferred at the author's call, because the Deck does not start; it is owed before the options ship to Deck players.

**Original approach (kept for reference).**

**Goal.** Measured frame times for each option on and off on the author's desktop and on the Steam
Deck, and from them the defaults: on, off, or tied to a quality setting.

**Evidence (confidence: lead-only).**
- SDFGI runs 8 cascades at a 1.5 m finest cell (`ProtoGi.cs:18`).
- The slab is one full-screen march.
- Neither has been measured.

**Approach.** The instrument is a `--perf` probe through `RunProbe.ps1`, the same launch the
`-Perf` stage makes (`--det --perf --no-vsync --mute --frames=600 --screenshot=`), with
`--graphics=enhanced` and the two flags set per state. `-Perf` itself cannot take the flags, since
its scenarios are fixed in `analysis/perf/scenarios.json`. The verdict term is `gpu_ms`, the root
viewport's GPU time per frame (PERF-1, PERF-2, PERF-11), read beside `frame_ms` and the window's
`p95_ms`. Each launch gives ten 60-frame windows; the first two are dropped (shader compilation and
SDFGI's cascade fill) and the run's figure is the median of the other eight. Each pose runs one
discarded warm-up round and then three measured rounds, the four states interleaved in a rotated
order within each round (PERF-9, PERF-48); a cell is the median of the three runs. The Deck half
uses the same probe over ssh under `--det` with a held session, and judges on the Deck (PERF-42).

**Model recommendation.** medium.

**Verify.** The table under **Measured (desktop)**, at 1920x1080 (`RunProbe.ps1 -Resolution`), with
one pose repeated at 2560x1440. The poses, all `--freecam` but the flown one:
- **C1C under:** C1C/M01, `--pos=-3694,800,-12482 --direction=0,0.6,-1`, under the base (layer
  1055 to 1955 m) in the rain, looking up into the deck (D31's pose).
- **C1C graze:** the same point, `--direction=0,0.15,-1`, so the rays cross the layer at a shallow
  angle: the pose that looks through the layer.
- **C1C above:** `--pos=-3694,2300,-12482 --direction=0,-0.25,-1`, over the tops, looking down
  through the layer.
- **C1C flown:** `--chapter=C1C --plane=player_bhawk --hold=0.2,0.1,0,1` (no mission, so no intro
  film; IA1's spawn at 1230 m), a chase camera climbing inside the layer, the moving camera that
  makes SDFGI scroll its cascades.
- **C1B:** `--pos=-6748,300,-8885 --direction=0.242,0.1,0.970`, the golden's point looking up at
  the fitted layer (986 to 3404 m) and its 1620 hidden placed clouds.
- **C2:** `--pos=-5722,186,-3457 --direction=-0.438,0.1,-0.899`, the golden's point over the city,
  the fitted layer (986 to 3456 m) over the upper half of the frame.
- **C3:** `--pos=-4518,400,-2015 --direction=-0.35,-0.35,-1`, the golden's canopy pose. C3 has no
  layer, so its clouds-only state must equal both off; it does to 0.01 ms, the check that the
  instrument reads the option and nothing else (METHOD-12).

**Measured (desktop).** RTX 5080, Enhanced at its `--det` defaults (shadow quality ultra, TAA,
render scale 100). Each cell: median `gpu_ms` (its change against both off), median `frame_ms`,
median window `p95_ms`, all in ms. Run-to-run spread of `gpu_ms` across the three runs is 0.06 ms
or less in every cell but two (C1C graze both on, 9.03 to 9.46; C1C above both on, 7.40 to 7.70).
Every state is GPU-bound: `frame_ms` sits 0.3 to 0.8 ms over `gpu_ms` throughout.

| Pose | Both off | Clouds only | GI only | Both on |
|---|---|---|---|---|
| C1C under | 0.60, 0.94, 1.39 | 9.53 (+8.93), 9.82, 10.00 | 1.17 (+0.57), 1.52, 1.52 | 10.13 (+9.54), 10.42, 10.42 |
| C1C graze | 0.83, 1.18, 1.39 | 8.42 (+7.59), 8.70, 9.09 | 1.42 (+0.59), 1.75, 1.85 | 9.41 (+8.58), 9.70, 9.72 |
| C1C above | 0.64, 0.99, 1.39 | 6.97 (+6.33), 7.26, 7.41 | 1.25 (+0.61), 1.56, 1.64 | 7.54 (+6.90), 7.81, 8.07 |
| C1C flown | 0.91, 1.38, 1.39 | 5.17 (+4.26), 5.45, 5.79 | 1.90 (+0.99), 2.25, 2.38 | 6.13 (+5.23), 6.41, 6.83 |
| C1B | 1.26, 1.59, 1.67 | 6.92 (+5.66), 7.20, 7.41 | 1.86 (+0.60), 2.18, 2.38 | 7.51 (+6.26), 7.81, 7.95 |
| C2 | 1.39, 2.23, 2.38 | 7.17 (+5.78), 7.47, 7.58 | 2.13 (+0.74), 2.71, 2.78 | 7.90 (+6.51), 8.21, 8.33 |
| C3 | 1.75, 2.27, 2.38 | 1.75 (+0.00), 2.26, 2.38 | 2.54 (+0.80), 2.94, 3.03 | 2.54 (+0.80), 2.93, 3.03 |
| C1C under, 2560x1440 (one run) | 0.91, 1.27, 1.39 | 16.54 (+15.63), 16.85, 17.54 | 1.63 (+0.72), 1.98, 2.08 | 17.19 (+16.28), 17.53, 18.06 |

- **The slab costs 4.3 to 8.9 ms of GPU at 1080p and 15.6 ms at 1440p**, the deck, field and
  sprites it hides already netted out. It grows 1.75 times for 1.78 times the pixels, so the cost
  is the per-pixel march (`steps = 40`, up to 60 on a long span, each sample with five sun taps,
  plus 16 shaft samples under the base) at full resolution. It is costliest under the deck looking
  up, where the shafts run as well, and through the layer at a shallow angle.
- **GI costs 0.57 to 0.99 ms of GPU at 1080p and 0.72 ms at 1440p**, the most on the moving camera
  (SDFGI scrolling its cascades) and in C3's canopy (0.80). It adds to the slab roughly linearly:
  both on is the two deltas summed to within 0.4 ms.
- **Not shown:** CPU load from AI, combat and effects, since the poses are empty and GPU-bound;
  splitscreen panes and the spyglass disc, which `gpu_ms` leaves out (PERF-39, PERF-47); 4K and
  the stereo eye sizes; fast flight's cascade scroll beyond the one climbing pose; and the Deck,
  where engine-native costs grow far more than here (PERF-42).

**Desktop recommendation (the author's call; no default changed).**
- `--volumetric-clouds`: **off by default.** On the fastest current desktop GPU the march alone
  takes 77 to 91 % of a 1080p frame at the clouded poses and passes the 120 Hz refresh period by itself
  at 1440p, before any CPU or combat load; 4K and the VR eye sizes multiply it by pixels. Shadow
  Quality does not govern its cost, so tying it there would mislabel the setting. A lower-resolution
  march with the TAA resolve, or fewer steps, would bring it back for review.
- `--global-illumination`: **on for a discrete GPU, off by the integrated-GPU fallback** the shadow,
  water and ground-cover settings already use (`ShadowQualitySetting.FallbackFor`), if the Deck half
  confirms that SDFGI does not fit there, and off in a headset's VR quality profile until it is
  measured in stereo. It costs at most about 1 ms here at 1080p and 1440p alike.

**⚠ Traps.** The sim clock lags wall time on the author's rig in late C2. Read the sim-clock cadence, not `physics_ms`.

## D35 ☐ March the clouds at reduced resolution

**Goal.** The slab costs about a quarter of its full-resolution GPU time at the D34 poses, with no visible loss the author objects to against full resolution.

**Evidence (confidence: direction-sound).** D34: the slab adds 4.3 to 8.9 ms at 1080p and 15.6 ms at 1440p on an RTX 5080, scaling with pixel count (1.78x the pixels cost 1.75x). The cost is the per-pixel march (40 to 60 steps, 5 sun taps each, 16 shaft samples under the base). The magnitude a lower resolution saves is TUNE until measured.

**Approach.** <TODO: the agent reads how the slab draws (a full-screen quad at priority 104 reading `depth_texture`) and picks the smallest workable route in Godot 4.7. Candidates: march into a half- or quarter-resolution target (a SubViewport or a compositor effect) and upsample against full-resolution depth so edges against terrain stay sharp; or march a checkerboard and reproject. Keep D31's draw order and the clouds-over-flare rule.> A lab or flag switch between full and reduced resolution lets the author compare.

**Model recommendation.** high: renderer plumbing in Godot 4.7 with depth-aware upsampling.

**Verify.** D34's instrument and poses, before and after; screenshots of full against reduced resolution at the C1C under, graze and above poses for the author; the 8-chapter sweep for shader errors; the full battery.

**⚠ Traps.** Upsampling without depth bleeds cloud over near terrain edges and aircraft. A half-resolution target with TAA on top can shimmer at layer edges; compare with TAA as the player runs it.

## D36 ☐ Option defaults: clouds off, GI on with the integrated-GPU fallback

**Goal.** A fresh install runs with Volumetric Clouds off and Global Illumination on, except where the integrated-GPU fallback the shadow, water and ground-cover settings already use turns GI off (Decision 15).

**Evidence (confidence: traced for the fallback, lead-only for the Deck).** `ShadowQualitySetting.FallbackFor` is the existing integrated-GPU fallback (named by the D34 agent). GI costs 0.6 to 1.0 ms on the desktop; the Deck half of D34 decides whether the fallback must cover it.

**Approach.** Default `GlobalIlluminationSetting` to on, with the fallback; keep `VolumetricCloudsSetting` off. Update the settings' unit tests and `docs/cli.md`. <TODO: check the VR quality profile; the D34 agent advises GI off there until measured in stereo.>

**Model recommendation.** medium.

**Verify.** Unit tests for the default and the fallback; the full battery (goldens run under `--det`, which keeps only the flag, so they must not move).

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
