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
| 16 | How does the player choose the march resolution? | **The Volumetric Clouds option: a combo box of off, quarter, half, full; the march resolution is the player's choice and saved** (D37). |
| 17 | Decision 4 says one checkbox each. What do the options look like? | **Two VIDEO dropdowns: Clouds Off/Quarter/Half/Full and GI Off/On**, because the page's two checkbox lines are taken (Ocean Waves and Enhanced Graphics). |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The terrain shadow bake drew the inverted ground shadow (lit under cover, dark in the rifts). | A brute-force ray cast against a `--dump-terrain-shadow` PNG agreed with the bake on 99.7% of texels. The cause was the ground's deck term reading `1 - gaps`, but `csky_pc_deck_gaps` returns the share of deck **kept**. The fix is `1 - y * gaps` in `csky_pc_deck_sun` (`CSVM/shaders/csky_cloud_shadow.gdshaderinc`). |
| 2 | `--terrain-shadows=off` is an A/B for the cloud shadows on the ground. | It also drops every cloud shadow, which the terrain's `light()` path draws. Use the cloud lab's tint toggle (red heads, blue deck) to read the shadows instead. |
| 3 | Aiming the sun at the drawn moon card fixes the shadow bearing. | It gave the same bearing. The drawn-moon `HeldSkyOrientation` was reverted; the held zone still aims the light at the dome's sun or moon card (`WeatherRig.LightOrientation`, from A1). |
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
35. ☑ March the clouds at reduced resolution
36. ☑ Option defaults: clouds off, GI on with the integrated-GPU fallback
37. ☑ The Volumetric Clouds option chooses the march resolution: off, quarter, half, full
38. ☑ The clouds' self-shadow flicker and the horizon band (author's report)
39. ☑ Zone-gated meshes draw while the clouds hold the zone (author's report: C4's mountain tops)
40. ☑ Puffers and other transparent effects are occluded correctly by the clouds (author's report)
43. ☑ Enclosed spaces read black under GI, since SDFGI replaces the flat ambient and `ambient_floor` did nothing (author's reports: C1/IA1 zeppelin hangar, C1 train tunnels)
44. ☑ Weapon sprites (tracers, flashes, gun smoke) are veiled by the clouds like the puffers (author's request, D40's follow-up)
45. ☑ Pre-close review fixes and a clouds layer for the cloud lab

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

**Landed.** The desktop half is measured (**Measured (desktop).** below) and the defaults follow from it (Decisions 14 and 15, D35, D36). The Steam Deck half is measured for GI and for the full-resolution clouds (**Measured (Steam Deck, full-resolution clouds).** below); the clouds are measured there again after D35.

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

**Measured (Steam Deck, full-resolution clouds).** The LCD Deck (`RADV VANGOGH`) in Game Mode,
build `c2a6bb159` as a Linux export in the scratch install `~/CSVM-tmp/d34`, launched over a held
ssh session with `DISPLAY=:0` and the engine's `--fullscreen` (every shot is 1280x800). The
desktop's instrument: `--det --perf --no-vsync --mute --frames=600 --screenshot=` with
`--graphics=enhanced` and the two flags per state, the median of each launch's windows after the
first two, one discarded warm-up round and three measured rounds with the four states rotated per
round, a cell the median of the three. `--det` drops the machine fallbacks, so the Deck's own
words are passed: `--shadow-quality=high --water-quality=flat --ground-cover=off`. A launch without
`--det` logs those three as `default_integrated_gpu`, and render scale 100 and TAA as the defaults,
so the runs match a Deck player's settings but for cloud shadows and post-processing, which `--det`
turns off and a player runs on. Cells as in the desktop table. Run-to-run spread of `gpu_ms` is
0.17 ms or less in every cell.

| Pose | Both off | Clouds only | GI only | Both on |
|---|---|---|---|---|
| C1C under | 4.95, 5.01, 5.06 | 172.93 (+167.98), 133.37, 140.00 | 11.32 (+6.38), 11.39, 11.67 | 184.58 (+179.64), 133.36, 135.09 |
| C1C graze | 5.59, 5.67, 5.89 | 146.59 (+141.00), 133.37, 146.67 | 11.84 (+6.25), 11.92, 12.00 | 156.53 (+150.94), 133.32, 140.00 |
| C1C flown | 8.48, 8.62, 8.79 | 82.00 (+73.52), 82.31, 83.33 | 20.89 (+12.42), 21.05, 24.96 | 93.47 (+85.00), 93.59, 99.09 |
| C3 | 11.78, 11.95, 12.12 | 11.77 (-0.01), 11.96, 12.12 | 19.89 (+8.11), 20.00, 20.00 | 19.91 (+8.13), 20.00, 20.00 |

- **GI costs 6.3 to 12.4 ms of GPU on the Deck**, 10 to 13 times its desktop cost at fewer than
  half the pixels (PERF-42). It takes the climbing chase camera from 8.6 to 21.1 ms a frame (p95
  25 ms) and C3's canopy from 12.0 to 20.0 ms, both past the 16.7 ms of 60 Hz at empty poses with
  no AI, combat or effects. The still C1C poses stay under it (11.4 to 11.9 ms), but those draw
  little besides the sky.
- **The full-resolution slab costs 74 to 168 ms of GPU**, 7.5 to 12 frames a second. At the two
  clouded C1C poses `frame_ms` sits at 133.3 ms (eight 16.67 ms ticks) in every run while `gpu_ms`
  reads 147 to 185 ms, above the frame interval, so `gpu_ms` there is not a clean cost; `frame_ms`
  is the player's figure. A quarter of this cost, D35's goal, would still leave 18 to 42 ms, so
  the clouds stay off on the Deck whatever D35 lands.
- C3's clouds-only state equals both off to 0.01 ms, the instrument check of the desktop table.
- **Not shown:** CPU load from AI, combat and effects; cloud shadows and post-processing, on for a
  Deck player and off here; splitscreen panes (PERF-39); the docked display at 1920x1080, which
  multiplies both costs by pixels; the C1C above, C1B and C2 poses; a long session's thermal and
  power state, since each launch runs one or two minutes; and why the clouded frames hold 133.3 ms.

**Deck recommendation.** GI must be off through the integrated-GPU fallback, as D36 plans. The
Deck counts as integrated: `ShadowQualitySetting.IsIntegratedGpu` reads
`RenderingServer.GetVideoAdapterType() == IntegratedGpu`, and a launch without `--det` on the Deck
logs `default_integrated_gpu` as the source of its shadow, water and ground-cover words. The
fallback must also hold for one pane, since GI alone breaks 60 Hz in a single-pane flight; the
pilot's Options toggle can still arm it.

**⚠ Traps.** The sim clock lags wall time on the author's rig in late C2. Read the sim-clock cadence, not `physics_ms`.

## D35 ☑ March the clouds at reduced resolution

**Landed.** `VolumetricClouds.MarchDivisor` marches the slab into a smaller target. D37 makes it the Volumetric Clouds option's word (`--volumetric-clouds=quarter|half|full`, the VIDEO row, the cloud lab's button); this item landed it as `--cloud-march=`, default `half`, which D37 removed.
- **Route.** A canvas `SubViewport` (2D only, HDR, transparent) runs the same march code as the quad, from the view's camera, which `FollowCamera` writes at `RenderingServer.FramePreDraw`. Every SubViewport renders before the view that samples it, so the quad at priority 104 reads it in the same frame and D31's order stands. A `CompositorEffect` would have meant porting the march, the per-view fog table and the terrain bake's global sampler to RD GLSL, which global shader uniforms do not reach. A checkerboard needs a history buffer a spatial shader cannot keep.
- **Depth.** The reduced march ignores the scene. Its target holds the layer's light weights and transmittance in its left half and the shafts' light at each quarter of their ray in its right half. The quad decides per full-resolution pixel from its own depth: a scene in front of the layer hides the cloud, a scene behind it takes the bilinear sample, and a scene inside the marched span (terrain or an aircraft in the layer) marches at full resolution. The shafts are read at the pixel's own depth between the stored quarters.
- **Full resolution stays** for a divisor of 1, a headset (`UseXR`), and any view whose camera is not the one traced (a splitscreen pane, the spyglass disc), which the quad tests per fragment.
- **Instrument.** The target is a viewport of its own, so `gpu_ms` leaves it out; `--perf` prints it as `[perf] cloud_march march_gpu_ms=`, and the table sums the two.

**Verified.** The orchestrator viewed the 2x crops: half reads as full, quarter saw-tooths the rift edges near the horizon. The full battery runs in E42. The agent's measurements, D34's instrument and poses at 1920x1080, clouds on and GI off, one warm-up round and three measured rounds with the four states rotated. Each cell is the median of `gpu_ms + march_gpu_ms`, then its delta over clouds off and that delta's share of full resolution's. Spread across runs is 0.03 ms or less except C1C above full (6.73 to 7.01).

| Pose | Clouds off | Full | Half | Quarter |
|---|---|---|---|---|
| C1C under | 0.60 | 9.55 (+8.95) | 3.10 (+2.50, 28 %) | 1.39 (+0.79, 9 %) |
| C1C graze | 0.83 | 8.41 (+7.58) | 3.03 (+2.20, 29 %) | 1.61 (+0.78, 10 %) |
| C1C above | 0.65 | 6.99 (+6.35) | 3.11 (+2.47, 39 %) | 1.88 (+1.23, 19 %) |
| C1B | 1.26 | 6.87 (+5.61) | 2.85 (+1.59, 28 %) | 1.75 (+0.49, 9 %) |

- The quad's own pass costs 0.1 to 0.15 ms over clouds off at under and graze, and 0.55 ms above, where no pixel falls back to the full march (checked with a debug colour).
- One flown C1C run each (`--plane=player_bhawk --hold=0.2,0.1,0,1`, chase camera inside the layer, the aircraft marching at full): 5.08 ms full, 2.56 half, 1.76 quarter.
- Screenshots of full against half and quarter at under, graze, above and C1B differ by a mean of 0.13 to 0.38 levels in 255, against 0.08 to 0.17 between two full-resolution runs. Above, quarter draws saw-tooth edges along the rifts near the horizon, where the coarse per-texel dither is too blocky for TAA to average. Half keeps the edges, a little softer.
- `--xr-sim` falls back to full and matches the unchanged build to a mean of 0.07 levels.

**Original approach (kept for reference).**

**Goal.** The slab costs about a quarter of its full-resolution GPU time at the D34 poses, with no visible loss the author objects to against full resolution.

**Evidence (confidence: direction-sound).** D34: the slab adds 4.3 to 8.9 ms at 1080p and 15.6 ms at 1440p on an RTX 5080, scaling with pixel count (1.78x the pixels cost 1.75x). The cost is the per-pixel march (40 to 60 steps, 5 sun taps each, 16 shaft samples under the base). The magnitude a lower resolution saves is TUNE until measured.

**Approach.** March the layer into a half- or quarter-resolution canvas `SubViewport` with the scene ignored, and let the existing quad pick per pixel from its own depth: no cloud in front of the layer, the upsampled trace behind it, a full-resolution march inside it. Keep D31's draw order and the clouds-over-flare rule. A lab or flag switch between full and reduced resolution lets the author compare.

**Model recommendation.** high: renderer plumbing in Godot 4.7 with depth-aware upsampling.

**Verify.** D34's instrument and poses, before and after; screenshots of full against reduced resolution at the C1C under, graze and above poses for the author; the 8-chapter sweep for shader errors; the full battery.

**⚠ Traps.** Upsampling without depth bleeds cloud over near terrain edges and aircraft. A half-resolution target with TAA on top can shimmer at layer edges; compare with TAA as the player runs it.

## D36 ☑ Option defaults: clouds off, GI on with the integrated-GPU fallback

**Landed.** `GlobalIlluminationSetting.Default` is `on`, and its fallback is `FallbackFor(det, integrated)`: `off` under `--det` (`default_det`, as `CloudShadowSetting` and `PostProcessingSetting` do), `off` on an integrated GPU (`default_integrated_gpu`, read through `ShadowQualitySetting.IntegratedGpu` as ground cover does), otherwise `on`. The renderer is asked only off `--det`, so the goldens and suites keep GI off unless their command line asks. The VR quality profile carries a GI word, `off`, beside its shadow word: a headset session stands GI off in place of the saved word, and `--global-illumination=` beats the profile, as `--shadow-quality=` does. `VolumetricCloudsSetting` stays `off` everywhere, unchanged. The `graphics-volumetric-clouds-gi` suite's restore falls back to `off`, the `--det` word, in place of the shared default.

**Verified.** Units 8424 passed in the agent's tree; the full battery runs in E42 with D35 and D36 merged.

**Original approach (kept for reference).**

**Goal.** A fresh install runs with Volumetric Clouds off and Global Illumination on, except where the integrated-GPU fallback the shadow, water and ground-cover settings already use turns GI off (Decision 15).

**Evidence (confidence: traced for the fallback, lead-only for the Deck).** `ShadowQualitySetting.FallbackFor` is the existing integrated-GPU fallback (named by the D34 agent). GI costs 0.6 to 1.0 ms on the desktop; the Deck half of D34 decides whether the fallback must cover it.

**Approach.** Default `GlobalIlluminationSetting` to on, with the fallback; keep `VolumetricCloudsSetting` off. Update the settings' unit tests and `docs/cli.md`. The VR quality profile (`Utils/VrQuality.cs`) is the mechanism that stands a headset session's anti-aliasing and shadow words in for the flat ones, through `LiveSettings.Flags`; it takes a GI word, `off`, until SDFGI is measured in stereo.

**Model recommendation.** medium.

**Verify.** Unit tests for the default and the fallback; the full battery (goldens run under `--det`, which keeps only the flag, so they must not move).

## D37 ☑ The Volumetric Clouds option chooses the march resolution: off, quarter, half, full

**Landed.** `VolumetricCloudsSetting` takes four words, `off`, `quarter`, `half` and `full`, in that order on the VIDEO page's dropdown, still at its place under Post-processing, with `off` the default everywhere (Decision 16).
- **One word.** `Enabled` is any word but `off`; `MarchDivisor` is 4 at quarter, 2 at half, 1 at full, and `VolumetricClouds.MarchDivisor` reads it each frame. A move between resolutions leaves `FollowVolumetricClouds` with nothing to build, so the same node stands, the zone hold untouched, and `FollowCamera` re-marches at the new size. A headset, a splitscreen pane and the spyglass still march at full.
- **The old word.** `on`, from an options file, the config key or the flag, reads as `half`, the march `on` ran at. `WordSetting.Legacy` maps a retired word, and `WordSetting.Known` applies it in `Resolve`, the flag parser and the options file's read, so a saved `on` is written back as `half`.
- **The flag.** `--cloud-march=` is gone; `--volumetric-clouds=` takes the four words.
- **The cloud lab.** One button steps off, quarter, half and full in place of B13's box and D35's march button. It calls `TakeUnsaved(word)`, and the session's exit `Retake` puts the saved word back.
- **The layers.** `LightingTable.ActiveLayers` and `WeatherRig` read `Enabled`, so the `clouds` layer applies at every word but `off`.

**Verified.** Units 8427 passed; `graphics-volumetric-clouds-gi` (able to fail: a planted wrong divisor turned it red), `menu-original-tracer-core` and `lighting-lab` pass; C1C probes per word and a saved `on` loading as half, in the commit message. The full battery runs in E42.

**Original approach (kept for reference).**

**Goal.** The author's request: "Make volumetric clouds option a combo box with off, quarter, half, full." The march resolution D35 put behind a flag becomes the player's saved choice.

**Evidence (confidence: traced).** D35's divisor was a static that `FollowCamera` reads each frame, so writing it live already re-marched without rebuilding the node (the D35 lab button). The VIDEO page draws every word setting as a dropdown over its `Words` and `Labels`, so the row needs no code of its own.

**Approach.** Widen the setting's words, derive the divisor from the word, drop `--cloud-march=` and its spec field, and map the retired `on` to `half` through the setting descriptor so the flag, the options file and the config key agree. Replace the lab's box and march button with one stepping button.

**Model recommendation.** medium.

**Verify.** Units: the four words, `on` as `half` from each source, an unknown word, precedence, `--det`, the options-file round trip, and the flag. `graphics-volumetric-clouds-gi` steps off, quarter, full, half and off live: the node stands exactly when not off, the divisor follows the word, and the same node stands between resolutions. `menu-original-tracer-core` walks the VIDEO page. A C1C probe per word logs the march line at its size.

## D39 ☑ Zone-gated meshes draw while the clouds hold the zone (author's report: C4's mountain tops)

**Goal.** The author's report: "The mountain tops from C4 zone2 are cut off /disabled because of our zone work." Under the clouds there is no whiteout to hide the band's switch, and the author wants to see the sky from below and the ground from over the zone switch, so the content the original gates by band zone draws while the zone is held.

**Evidence (confidence: traced).** `WeatherRig.Tick` stops updating a rig's camera weather state while `HoldZone` stands, and the cull mask kept `ZoneGate.CullMask(mask, gate)` on that held state. A camera held in state 1 culled every `zone_id 2` mesh. The census reads `extracted/<CH>/gamez/nodes.json`, world AABBs composed down each node's parent chain as `GameZ.ParseTransform` does. The `zone_id` decode is `docs/formats/gamez.md` ("`zone_id`'s runtime meaning, decompiled") and `docs/org/weather.md` ("The `zone_id` visibility gate").

| Chapter | Built zone 1 / 2 / 3 (log) | What zone 2 or 3 is | Overlap with zone 1 |
|---|---|---|---|
| C1 | 1394 / 626 / 0 | the deck (144 tiles at 960 m), 9 `fvol`, the `cloudparent` cards (`g0/l2586`, `g27816/l2586`), the zone-2 dome | none; zone 1 is the whole ground world |
| C1B, C2, C3 | 1831, 519, 1186 / 0 / 0 | nothing | not applicable |
| C1C | 144 / 1056 / 0 | the deck, 21 `fvol`, the cloud cards; zone 1 is 144 flat tiles at sea level | none |
| C2B | 145 / 1198 / 0 | the deck and the cloud cards (its `fvol` are `zone_id -1`); zone 1 is 145 flat tiles at sea level | none |
| C4 | 456 / 1721 / 0 | 14 mountain-top terrain pieces cut at the 1,050 m deck plane (`g2052`-`g2057`, `g2061`-`g2065`, `g2607`-`g2609`, tops to 1,490 m at `g2064`), the `bhf` fortress (247 meshes, 1,125 to 1,365 m) and its plugs, the `rabaset1` base (`racmplx`, `rabdr`), the deck, 9 `fvol` and the cloud cards | none; 3,057 zone-2/zone-1 pairs share XZ but meet only along a plane within 1 m |
| C5 | 958 / 0 / 134 | 120 street pieces and 14 lamp posts inside the fog strips (0 to 44 m), the `zone3` dome | **yes**: 35 pairs past half the smaller box, e.g. `g4551` on `g4699` and `g4417` on `g4621`, and 14 share a `model_index` with a zone-1 mesh |

No deck chapter tags an alternate of the same object per band zone, so opening zones 1 and 2 draws nothing twice. C5's zone 3 is the inside look of its fog strips against the zone-1 outside look, so it stays gated by the held state.

**Landed.** `ZoneGate.HeldCullMask(mask, state)` draws zones 1 and 2 both and zone 3 only in state 3; `WeatherRig.Tick` uses it while `HoldZone` stands. The deck stays hidden (`HideDeck`) and the dome gate still shows the upper dome. The fog volumes' body is on the zone-1 layer, which the held mask keeps. Releasing the hold needs no new code: the next `Tick` re-reads the state and the plain `CullMask` narrows the band to it.

**Verified.** The orchestrator viewed the C4 in-layer pair: before, the ground ends in a flat line at the deck plane; after, the massif rises through the cloud. Units, the suite and the sweep are in the commit message; the full battery runs in E42.

**Approach.** Census the zone-gated meshes per chapter by world AABB and name, open the band's two zones under the hold, keep zone 3 on the state, extend the live-toggle suite.

**Verify.** Unit `TheHeldMaskDrawsBothBandZonesAndKeepsTheVolumeZoneGated`. `graphics-volumetric-clouds-gi` ticks the weather with the camera 500 m under and over C1C's band at each word: on, the band is zones 1 and 2 and the state stays held; off, the first tick reads state 1, the second state 2, and the band is zone 2 alone. C4 probes at `--volumetric-clouds=half`, before and after. Under the band (`--pos=-4600,900,-1700 --direction=0,0.18,-0.98`) the peaks sit inside the cloud base, so only the draw count shows them. Inside the layer (`--pos=-4600,1150,-5300 --direction=0,0.12,1`) the before shot ends at a flat line on the deck plane, and the after shot shows the silhouettes. The under-band pose with the clouds off matches a no-flag probe. The 8-chapter `--freecam --graphics=enhanced --volumetric-clouds=half` sweep logs zero shader errors.
## D40 ☑ Puffers and other transparent effects are occluded correctly by the clouds (author's report)

**Landed.** While a slab stands, every puffer draws after it and takes the slab's transmittance from the eye to the particle on its alpha.
- **Order.** `MultiMeshEmitterRenderer.DrawPriority` is 0, or 105 while `VolumetricClouds` has a slab built (`BuildSlab` sets it, `FreeBuilt` puts 0 back). Each layer's material takes a change on its next `Publish`, so a puffer built before the clouds follows them. The slab moves to 104 (`SlabPriority`, the rain's less 2) and the rain to 106 (`Precipitation.DrawPriority`, the flare's 100 plus 6). With the clouds off nothing draws at 104 or 105, so the rain's order against everything else is unchanged.
- **Transmittance.** `csky_pc_veil_at(eye, p)` in `csky_volumetric_clouds.gdshaderinc` marches the slab's own density from the eye to the particle: the heads in twelve steps over the ray's span through the layer, and the deck in three over its own span, which steps sized for the whole layer would miss. It reads the ground shadow's head globals and a new `csky_pc_veil` (floor, top, deck cover, deck depth) that `PublishGround` writes and zeroes with no slab. With no layer between eye and particle (both under the base, the usual case) it returns 1 before any noise.
- **Where.** The Enhanced puffer text computes it once per particle in `vertex()` at the instance origin (`v_veil`) and multiplies `ALPHA` by it before the composite, so an additive and a mixed sprite both dim. A particle is tens of metres across against the clouds' 180 m erosion cell, so one value per particle holds. Being per vertex, it runs the same at every march resolution, in a headset, a splitscreen pane and the spyglass. The faithful text is unchanged.

**Verified.** The orchestrator viewed the `rklow` montage: before, the rocket bursts in the sky are erased at half and full; after, they stand in front of the deck as with the clouds off. Sweep, suites and cost are in the commit message; the full battery runs in E42.

**Goal.** The author: "Puffers are rendered behind the volumetric clouds." A puffer in front of a cloud draws over it, one inside it fades with the cloud between it and the eye, and one behind a thick cloud stays hidden, at every march resolution and with the clouds off unchanged.

**Evidence (confidence: traced-to-code).** The slab is a full-screen quad with no depth test that reads `depth_texture`, opaque depth alone, and composites `cloud + T * dst`. Every transparent drawn before it is under the whole cloud along its pixel's ray, wherever it stands. Census of the transparent world draws:

| Draw | Module | Priority | Blend | Depth write | D40 |
|---|---|---|---|---|---|
| Puffers: smoke, fire columns, explosion and impact sprites, rocket and exhaust trails, splashes, cliff spray, speed cue | `Effects/EmitterRenderer.cs` | 0 | mix (gamma composite) or add | never | after the slab while it stands, dimmed by `csky_pc_veil_at` |
| Tracer streaks, tips, muzzle flashes, impact flashes, gun smoke | `Flight/Weapons/Projectile.cs` pools, `StandardMaterial3D` | 0 | add (gun smoke mix) | never | unchanged by D40; with the puffers since D44 |
| Heat shimmer | `Effects/HeatShimmer.cs` | -1 | mix of the screen copy | never | unchanged: its copy is taken before the transparent pass, so after the slab it would write the cloudless scene over the cloud |
| Wind streaks | `Effects/WindStreaks.cs` | 0 | mix | never | unchanged: off by default, within 30 m like the rain |
| Rain and snow | `Effects/Precipitation.cs` | 105, now 106 | mix | never | after the slab since D31 |
| World flare | `Session/World/WorldFlareRig.cs` | 100 to 103 | add | never | under the slab on purpose (D31) |
| Lamp sprites | `Mech3/SceneBuilder.cs` light shader | 0 | add | never | unchanged |
| Aircraft flares | `Mech3/PlaneBuilder.cs` flare shader | 0 | add | never | unchanged |
| Blended world surfaces (glass, flame facades, ground layers, coast ramps) | `Mech3/SceneBuilder.cs` | 0, -2, -3 | mix | never | unchanged: mostly with opaque geometry right behind, which hides the cloud |
| Cloud field cards | `Effects/FogVolumeClutter.cs` | 0 | mix | never | hidden while a slab stands |
| Moon | `Mech3/WorldBuilder.cs` | 0 | mix | never | correctly under the cloud |
| Aircraft ground shadow | `Flight/Airframe/GroundShadowPass.cs` | 0 | multiply | never | on the ground |
| Scorch marks | `Effects/ScorchField.cs` (`Decal`) | opaque pass | | | not a transparent |

Open after D40:
- **The weapon sprites.** The `Projectile` pools are `StandardMaterial3D`s, which cannot read the veil. D44 covers them.
- **The new order.** While a slab stands, the puffers draw after every other world transparent, the lamps and the aircraft flares included, where they were depth-sorted with them before. A lamp in front of smoke draws under it. The weapon sprites sort with the puffers again since D44.
- **The mix composite.** A mixed puffer's gamma alpha (`GammaMixComposite`) reads the screen copy taken before the transparents. Over a cloud it is therefore worked out against the scene behind the cloud, as it already was over any other transparent.

**Approach.** Order plus transmittance, the rain's fix with the cloud between taken into account. Each alternative was weighed and dropped:
- **The slab before the transparents.** Every blended world surface would then need the transmittance, since seen from above the layer a ground layer would draw over the cloud.
- **The reduced target's transmittance per segment.** It changes the march D38 is reworking. There is no target at full resolution, in a headset, a pane or the spyglass, which would need the per-fragment march anyway.
- **A per-fragment march.** It costs the march per pixel of smoke overdraw where a per-vertex one costs it four times per particle.

**Verify.** `graphics-volumetric-clouds-gi`: a puffer built with the clouds off draws at 0, at 105 at quarter, full and half, and at 0 again off; its Enhanced text carries `csky_pc_veil_at(` and the faithful text no veil. Flown C1 probes firing rockets (`--fly --plane=player_bhawk --hold=0,0,0,1 --fire-rockets`), before and after, clouds off, half and full: from under the layer (`--pos=-6041,600,-4313`) the bursts and trails stand in front of the deck's underside; inside the deck, under the band's midpoint (`--pos=-6041,1050,-4313`), the near puffs stand inside cloud and the far bursts behind it. GPU cost before and after at both, clouds off, half and full (D34's runner, the march's own time added). The 8-chapter `--freecam --graphics=enhanced --volumetric-clouds=half` sweep for shader errors; `xr-sprite-facing`, the puffer suites; units.
## D38 ☑ The clouds' self-shadow flicker and the horizon band (author's report)

**Landed.** `pc_march` (`VolumetricClouds.MarchCode`, shared by the quad and the reduced march) changes in two ways.
- **The step.** It is the span's share as before, capped by `pc_step`: `step_near_m` (30 m) plus `step_growth` (0.05) of the distance from the eye. A ray takes at most `step_budget` (128) steps; a level ray inside the layer needs about 93 to reach the 24 km cap.
- **The first cloud.** A density-only scan finds the first cloud. Its surface is bisected three times between the last clear sample and the first dense one, and the lit march starts half a step past it. The bisection sits after the scan loop, so a GPU wave runs it once; inside the loop it cost 0.45 to 0.66 ms more at half resolution.
- **Unchanged.** The density, the sun taps, the rest fill past the cap, the haze and the shafts. D40's `csky_pc_veil_at` marches from the eye to each particle over that distance alone, never to `far_m`, so the level-ray fault does not reach it.

**Verified.** The orchestrator viewed the montages: the moving hatch on the shadowed undersides is gone at Quarter, and the near heads at eye level are solid with their shaded faces. Metrics and cost are in the commit message; the full battery runs in E42.

**Original approach (kept for reference).**

**Goal.** The author: "the clouds self shadow is flickering very strong", then "I had quarter clouds active, it's a lot better with half or full. But still there. And the transparent band on the horizon level is still there too", and "this is not seen from the cloud but when the camera is on the same height as the clouds. there is always a transparent band in the clouds".

**Evidence (confidence: measured).**
- **The band's cause: the step over a level ray.** The march split the span into at most 60 steps. A ray within about 3 degrees of level inside the layer has a span capped at `far_m` (24 km), so it stepped about 400 m and stepped over near heads. Rays tilted more cross the layer in a short span with short steps, which is why the stripe sat at eye level and cut through near clouds. The rest fill past the cap, and a step budget running out, were ruled out: the old count always reached the cap, and the fill is opaque on a level ray.
- **The flicker's cause: the dithered depth of the first sample.** One step into a dense cloud carries an optical depth of 1.4 at 55 m and 10 at 400 m. So a pixel's colour is the light at one sample, anywhere within a step of the surface, and the dither moves it every frame. The self-shadow there changes steeply with depth. The sun taps (`pc_sun`, three at a fixed 270 m spacing) are not dithered and add no frame-to-frame change.
- **The metric.** Eight consecutive `--det` frames (`--shots=8`, the shader clock steps 1/60 s a frame, so the dither moves as in play), 1920x1080, C1. The share of pixels whose temporal standard deviation exceeds 2 levels, with the 99th percentile in levels. A clouds-off control reads 0.00 % (p99 0.48).

| Pose, rows | Quarter | Half | Full |
|---|---|---|---|
| Author's, `--pos=-7124,1476,-5471 --direction=-1,-0.15,0`, rows 300-1080 | 1.88 % (2.95) → 0.41 % (0.87) | 0.51 % (1.22) → 0.12 % (0.50) | 0.01 % (0.78) → 0.01 % (0.50) |
| Eye level in the layer, `--pos=-4660,1280,-3065 --direction=-1,0.02,0`, rows 480-640 | 17.44 % (5.94) → 1.47 % (2.65) | 1.82 % (2.39) → 0.26 % (1.09) | 0.03 % (1.36) → 0.01 % (0.66) |

- **The cost.** D34's instrument at 1920x1080, GI off: `gpu_ms + march_gpu_ms`, the median of each launch's windows after the first two. The old and new march were timed back to back on one build, one pair per cell; the old column repeats D35's figures to 0.05 ms.

| Pose | Quarter | Half | Full |
|---|---|---|---|
| C1C under | 1.39 → 1.42 | 3.11 → 3.29 | 9.55 → 10.54 |
| C1C graze | 1.61 → 1.64 | 3.03 → 3.16 | 8.46 → 9.05 |
| C1C above | 1.87 → 1.89 | 3.11 → 3.18 | 6.94 → 7.26 |
| C1B | 1.74 → 1.72 | 2.84 → 2.86 | 6.86 → 7.20 |
| C1, eye level in the layer | 1.69 → 1.74 | 2.75 → 2.94 | 6.55 → 7.30 |
| C1, the author's pose | 1.55 → 1.62 | 2.55 → 2.80 | 5.93 → 6.70 |

- **The look.** At the author's pose the self-shadowed undersides no longer carry the per-frame hatch and vertical streaks at quarter and half; the lit-to-shadow edge holds still, and at quarter it keeps the target's stair-steps. At eye level the near heads draw solid with their shaded faces, where before the dome's painted clouds showed through them along a horizontal seam. C2's horizon (`--pos=-5630,205,-3422`) and C1C's from above read as before. From above, the far tops still fade into a flat fog-grey strip under the dome, which is A1's 8-24 km haze.
- **Not measured.** Motion: every frame is a still camera. A flown camera, TAA's history under motion and the Steam Deck's cost are not covered.

**Approach.** Cap the step by distance from the eye, so a level ray resolves near clouds, and read the first cloud's light at a depth set by its surface rather than by the dither.

**Model recommendation.** high: shader sampling and GPU cost.

**Verify.** The bursts and metric above at each resolution, the cost table, the 8-chapter sweep with zero shader errors, `graphics-volumetric-clouds-gi`, the units, and the author's eyes in flight.

## D43 ☑ Enclosed spaces read black under GI, since SDFGI replaces the flat ambient and `ambient_floor` did nothing

**Landed.** Both of the author's picks, read through two globals that are zero unless SDFGI stands (`WeatherRig.WriteGiGlobals`, declared in `csky_atmosphere.gdshaderinc`).
- **A working floor.** `ambient_floor` keeps its key and `gi` layer. It now sets `csky_gi_ambient_fill`, the zone's flat ambient (linear colour times energy) times the floor. The world-lit arm adds it as `EMISSION`, shaded by the cloud shadow's AO term. The default is 1, the GI-off fill; the range is 0 to 2. The Environment's ambient is no longer scaled by the floor.
- **Self-lit emission.** A new `gi` field, `self_lit_emission` (default 1, range 0 to 8), sets `csky_gi_self_lit`. The Enhanced fullbright arm writes `EMISSION = ALBEDO * csky_gi_self_lit`, leaving out the dome, the deck, clutter, water and the stereo interior. An unshaded EMISSION never reaches the screen, only SDFGI's voxels, so a self-lit hangar lights the models in it.
- **Defaults and data.** The author's four saved floors (C1, C1B, C4 and C5 `gi` layers) are dropped from `lighting.json`, so every zone takes the GI-off fill. The lab labels read "Ambient fill under SDFGI" and "Self-lit glow into SDFGI". The glow's note says a change waits for SDFGI to re-voxelise.

**Verified.** The orchestrator reviewed the before and after matrix and the author chose the fixes from the comparison montage; units, the sweep and the suites are in the commit message. The full battery runs in E42.

**Goal.** The author's reports:
- "In C1/IA1 the zeppelin hangar is lit by itself but the zeppelin in it and a car are nearly black."
- "could d41 also check the train tunnels in C1 they are too dark under gi and i found no setting where i could brighten them up without changing the look of the map significantly. I had the feeling that the "keep ambient" slider did not do anything there."

With GI on, the lit models in the hangar and the lit tunnel shells read as they do with GI off, without washing out open air.

**Evidence (confidence: traced in Godot's shader source; measured).**
- **The places.**
  - **The hangar** is C1's `g16107` at (-5239, 128, -6754). Its shell (`hangar_floor`, `mainhangar_roof`, `hangar_left`/`_right`, `terminal_win`) is authored `lighting: false`, so Enhanced draws it on the unshaded fullbright arm.
  - **The models inside it** are authored `lighting: true` and draw on the world-lit arm: the zeppelin (`dliner1`/`dxzep_skin`) and the Studebaker trucks (`hauler1`, `flatbed1`/`2`).
  - **The train tunnels** are the danger zones dz2 to dz4 on z -3856. Tunnel Mid runs from x -5784 to -5465. Its shell is `track01` (`rr_bricks*`, `lighting: true`) under the terrain tile `a3`.
- **The cause.**
  - **Godot replaces the ambient.** Godot's forward shader computes `ambient_light = mix(flat ambient, GI buffer, alpha)`, and `sdfgi_process` writes alpha 1 everywhere inside the cascades. The cascades span the chapter, so the Environment's ambient never reaches a pixel while SDFGI stands.
  - **The roof and the tunnel hide the sky.** SDFGI's sky light reads the background panorama, which the roof and the tunnel occlude.
  - **Consequence.** The old floor, a multiplier on that ambient, was a no-op: floor 0 and floor 1 drew identical frames at the hangar and in the tunnel.
- **Measured before the fix.** Probe settings: 1920x1080, `--freecam --graphics=enhanced --frames=300`, clouds off unless noted. Values are mean Rec.709 luma (0 to 255, sRGB). Zeppelin and truck pixels come from a `--tex-override` mask pair (SHOT-38). Poses:
  - Hangar: `--chapter=C1 --mission=IA1 --pos=-5301,142,-6454 --direction=-0.12,0.08,-1`.
  - Tunnel Mid inside: `--pos=-5560,134,-3856 --direction=-1,0,0`, its far mouth masked out.
  - Its mouth: `--pos=-5440,138,-3856 --direction=-1,-0.03,0`.

  Each candidate was a temporary hack.

| State | Zeppelin | Truck | Hangar wall | Tunnel inside | Tunnel mouth |
|---|---|---|---|---|---|
| GI off | 71.4 | 23.3 | 76.7 | 14.7 | 31.7 |
| GI on (old floor 0 or 1) | 0.8 | 2.9 | 76.7 | 0.1 | 17.1 |
| GI on, the author's old C1 tune (floor 0.08, shadow 0.31, clouds half) | 4.2 | 2.9 | 76.6 | 0.5 | |
| Fill as emission, 0.08 / 0.15 / 0.3 (C1's flat ambient is 0.15) | 52.6 / 71.2 / 96.3 | 17.3 / 24.7 / 35.7 | 76.7 | 9.2 / 14.8 / 23.6 | 26.4 / 32.2 (0.08 / 0.15) |
| Self-lit emission ×0.5 / ×1 / ×2 / ×4, no fill | 20.6 / 31.9 / 46.7 / 65.9 | 5.0 / 7.0 / 10.4 / 15.8 | 76.7 | 0.1 (×4) | |
| Self-lit shells cast no shadow | 0.8 | 2.9 | 76.7 | 0.1 | |
| SDFGI bounce feedback 1.0 / 1.5 | 0.8 / 3.6 | 2.9 / 3.5 | 76.7 | 0.2 (1.5) | |

- **The dead ends.**
  - **No shadow from self-lit shells.** It moved no pixel at any pose, with GI on or off.
  - **More bounce feedback.** Bounce feedback 1.5 lifted the zeppelin by 3 levels.
  - **The tunnels.** Self-lit emission cannot reach them, because they have no self-lit surface.

**Approach.** The fill as emission on the lit world, plus the self-lit surfaces' own colour as emission into SDFGI. Both are tune fields in the `gi` layer, read through globals so the lab's sliders apply without a shader rebuild.
- **Default fill.** 1, the zone's GI-off ambient.
- **Default self-lit gain.** 1. With the fill on, the gain only needs to take the zeppelin a little past GI off. ×2 reads 84 against the wall's 77, and ×4 alone (65.9) was tuned with no fill under it.

**Verify.** Same probes and statistic, after the fix, at the defaults.

| Pose | GI off | GI on, fill 0 and gain 0 | GI on, default |
|---|---|---|---|
| Hangar zeppelin / truck / wall | 71.4 / 23.3 / 76.7 | 0.8 / 20.4 / 76.7 | 78.2 / 35.9 / 76.8 (fill only: 71.7) |
| Hangar zeppelin, clouds half | 46.6 | 0.5 (before the fix) | 67.7 |
| Tunnel inside | 14.7 | 0.1 | 17.6 |
| Tunnel mouth | 31.7 | 17.1 (before the fix) | 32.7 |
| C1 lake, whole frame | 51.8 | 46.9 | 52.6 |
| C2 city, whole frame | 126.2 | 121.5 | 131.3 (all of it the fill; the gain moved no pixel) |
| C1B dzpath1 tunnel, whole frame | 31.7 | 23.1 (before the fix) | 27.3 |

- **GI off is unchanged.** Every GI-off shot (hangar, tunnel, C1 lake, C2 city) is pixel-identical to the same shot before the fix.
- **The fill applies live, the gain does not.** A temporary hook set the globals 150 frames after the zone apply, with the camera still. The fill dropped the zeppelin from 71.7 to 2.0 in the same run. A gain raised from 0 to 4 left it at 71.7, where 4 from the start reads 93.7. The slider's note says so.
- **Checks run:**
  - the 8-chapter `--freecam --graphics=enhanced --global-illumination=on` sweep: zero shader errors, the only stderr ERROR being the exit-time `finalize` line every probe prints;
  - `graphics-volumetric-clouds-gi` and `lighting-lab` pass;
  - units 8428 passed, with `LightingTableTests` holding the defaults and the `gi` round trip of `self_lit_emission`.
- **Goldens.** They run under `--det` with GI off, where both globals are zero, so they should not move; the full battery in E42 checks it.
## D44 ☑ Weapon sprites (tracers, flashes, gun smoke) are veiled by the clouds like the puffers (author's request, D40's follow-up)

**Landed.** While a slab stands, every weapon sprite pool wears a veiled `ShaderMaterial` twin of its own `StandardMaterial3D` and draws at the puffers' 105, dimmed by `csky_pc_veil_at` as they are.
- **Where.** `ProjectilePool.BandedPool` (`Flight/Weapons/Projectile.cs`). `End`, run each frame for each pool, compares `MultiMeshEmitterRenderer.DrawPriority` with the priority it last took. On a change it sets each side's `MaterialOverride` to the twin at that priority, or back to null at 0, so the mesh's own material draws again. An above-band side built later takes the same.
- **The twin.** `VeiledCode` is the text Godot generates for `AddMultiMesh`'s settings (unshaded, `depth_draw_never`, `cull_disabled`, the albedo texture times `COLOR`, `filter_linear_mipmap`, `repeat_disable`), with `blend_add` or `blend_mix` from the source and `ALPHA` multiplied by `csky_pc_veil_at(CAMERA_POSITION_WORLD, MODEL_MATRIX[3].xyz)` in `vertex()`. One shader per blend is shared by the process; each pool's twin carries its texture. With nothing between the eye and the sprite the veil is 1, and the twin draws what the `StandardMaterial3D` draws.
- **Unchanged.** The faithful path and Enhanced with the clouds off never build a twin, so they keep the `StandardMaterial3D`s. Heat shimmer stays at -1 as D40 left it.

**Verified.** Probes, cost and suites in the commit message: clouds off byte-identical, inside the deck the muzzle flash and gun smoke now stand in front of the slab, and the twin matches the original look where the veil is 1. The full battery runs in E42.

**Goal.** The author: "Do the weapon-sprite follow up". A tracer, flash or gun puff in front of a cloud draws over it, and one inside or behind it fades with the cloud between it and the eye, as a puffer does since D40.

**Evidence (confidence: traced-to-code).** Census of the weapon sprites' materials. Every pool is built by `AddMultiMesh` through `AddBandedPool` in `_Ready`: a `MultiMesh` of `Transform3D` instances with per-instance colour, one `MultiMeshInstance3D` per band side, over one `StandardMaterial3D`.

| Pool | Texture | Mesh | Blend |
|---|---|---|---|
| Tracer streaks, one per `TracerTextures` entry | the ammo's streak | `CrossedStreakMesh`, two crossed quads | add |
| Tip discs, one per `TipTextures` entry | the ammo's tip | unit quad | add |
| Muzzle flashes, one per `MuzzleAmmoTextures` entry | the ammo's flash | unit quad | add |
| Impact flashes | `slug_muzzle2` | unit quad | add |
| Gun smoke | `smoke101` | unit quad | mix |

Every material is unshaded, `Transparency.Alpha`, `CullMode.Disabled`, `DepthDrawMode.Disabled`, billboard off (`RenderSprites` orients each quad itself), `VertexColorUseAsAlbedo`, `TextureRepeat` off, the default `LinearWithMipmaps` filter, and render priority 0. A `StandardMaterial3D` cannot include `csky_volumetric_clouds.gdshaderinc`, so it cannot read the veil.

**Approach.** The twin, swapped through `MaterialOverride` only while a slab stands. The alternative, the `StandardMaterial3D`s moved to 105 with the veil folded into each instance's colour alpha, needs the slab's density march ported to C# beside the shader's, and a per-instance march on the CPU each frame; the twin reuses D40's function as it stands. Swapping the override, rather than replacing the mesh's material, leaves the faithful and cloudless draw untouched. Weapon sprites and puffers share 105 again, so they depth-sort together, which undoes D40's side effect of a tracer in front of smoke drawing under it. The rain stays at 106 and the head-locked tiers above it.

**Model recommendation.** medium.

**Verify.** `graphics-volumetric-clouds-gi`: a weapon pool built with the clouds off wears its `StandardMaterial3D`s at 0, veiled twins (`blend_add` for the tracer, `blend_mix` for the smoke, both carrying `csky_pc_veil_at(`) at 105 at quarter, full and half, and its own materials again off. The suite is windowed (`headlessShaders`), so both twin texts compile on a real renderer. Flown C1 probes firing guns (`--fire`) and rockets (`--fire-rockets`), before and after, clouds off, half and full, under the layer (`--pos=-6041,600,-4313`) and inside the deck (`--pos=-6041,1050,-4313`). Clouds off is byte-identical before and after. GPU cost at a firing pose with the clouds half. The 8-chapter `--freecam --graphics=enhanced --volumetric-clouds=half` sweep for shader errors; `xr-sprite-facing`, the weapon and projectile suites; units.

## D45 ☑ Pre-close review fixes and a clouds layer for the cloud lab

**Landed.**
- **The cloud lab's `clouds` layer.** A chapter entry in `cloud_shadows.json` may carry a sparse `clouds` block, applied over the entry while Volumetric Clouds stands on (`CloudShadowChapters.For(chapter, clouds)`). `CloudShadow` resolves its tune and drift from the layer the word names, re-resolving each frame and from `GameSession.FollowVolumetricClouds` when the word moves, which drops live edits. The cloud lab names the layer under its title, re-reads it when the word moves, and saves into it: a `clouds` save writes only the fields that differ from the entry and keeps the base fields, and a base save keeps the `clouds` block after the fields.
- **The volumetric fields** (`head_taper`, `deck_sun`, `deck_ambient`) need no routing. They draw only while the clouds stand, which is exactly when the lab edits `clouds`, so a lab save puts them there as differences. A base save also writes them, where nothing reads them.
- **The data.** Every value `d58606a68` changed in `cloud_shadows.json` moved into the chapter's `clouds` block, and each base field is back at its value before that commit. Values equal to the base's (`head_taper` 0.3, `deck_ambient` 0.6, C2's `deck_sun` 0.97, the defaults) are left out, as a lab save would leave them; the clouds-on tune is unchanged. In `lighting.json` every `god_rays` left the `gi` blocks for the matching `clouds` blocks; where both held one (C1, C1B) the `gi` value, the one drawn with both options on, replaced the `clouds` one.
- **Layer-owned lighting fields.** `ZoneLighting.Field.Layer` names the one layer a field belongs in: `god_rays` in `clouds`, `ambient_floor`, `self_lit_emission` and `sdfgi_energy` in `gi`. `LightingTable.WithZone` never writes such a field into another layer or the base (`LightingTable.Belongs`), and the lighting lab's save note names any edit it left unsaved. Routing a field into another layer than the edited one was not built: with both options on `god_rays` saves only with GI switched off for the flight.
- **The rain's draw order** follows the slab as the puffers' does: `Precipitation.DrawPriority` is 0 with no slab and `OverSlabPriority` (106) while one stands, set in `VolumetricClouds.BuildSlab` and reset in `FreeBuilt`, and each field takes a change on its next frame. `c4-snow` is back at `2ca089750a810401a9dbc2dbaf251bb1`.
- **The VIDEO page's ACCEPT** never saved a lab's unsaved word. The page's staged words are the options file's or the player's picks; a lab word only shows on a row with nothing saved, and ACCEPT then carries null and saves nothing for it.
- **Clean-up.** Plan and history references out of code comments; `rift_sky_lift`, the always-1 `cover_scale` and the march's `bottom`/`top` parameters removed with the shader output unchanged; the veil's declaration and line shared by the puffers and the weapon twins (`MultiMeshEmitterRenderer.VeilDecl`, `VeilSet`); stale comments in `VolumetricClouds` and `TerrainShadow`; the unused `using` in `CloudLab`. `softVEIL_MUL` is no typo but the `VEIL_MUL` hole after `soft`, as `GAIN_MUL` is. `PublishedGround`'s ungated `DeckGaps` draws nothing without a deck, since every reader gates on the deck's share.

**Verified.** Units, the touched suites, the C1C tune lines (clouds off reads the pre-E41 base, clouds on the author's values) and the sweep are in the commit message. The migration was re-run on the plan branch's data after the author's retune and is idempotent. The full battery runs in E42.

**Goal.** The author: "fix them, add a clouds layer to the cloud lab".

**Evidence (confidence: traced-to-code).** The pre-close code review found:
- the cloud lab saved into each chapter's base tune, which the cloud shadows draw with the clouds off too, so the tuning done under the clouds changed the clouds-off look (`d58606a68`);
- `god_rays` saved into `gi` layers, because the lighting lab edits `gi` whenever GI is on;
- `Precipitation.DrawPriority` was 106 in every mode, so the rain drew after the flare with no slab and `c4-snow` moved;
- plan references and prototype wording in comments, dead shader parameters, a misplaced comment, two shader lines run together, and an unused `using`.

**Approach.** A sparse `clouds` block per cloud-shadow entry, built as `LightingTable`'s layers are; a layer per lighting field that draws under one option alone, refused elsewhere; the rain's priority set by the slab as the puffers' is.

**Verify.** Units: the clouds layer parses, applies only while the clouds stand, saves only differences and round-trips without reordering (`CloudShadowChaptersTests`); a layer field is never written into another layer or the base (`LightingTableTests`); an options apply never saves a lab's word (`VolumetricCloudsAndGiSettingTests`). `graphics-volumetric-clouds-gi` checks the rain's priority and the cloud shadows' layer at each word; `lighting-lab`, `cloud-shadow-globals`. C1C probes: the cloud-shadow tune line reads the pre-`d58606a68` base with the clouds off and the author's values with them on. A C1C probe pair before and after the shader clean-up, pixel-identical. `c4-snow`'s golden. The 8-chapter `--freecam --graphics=enhanced --volumetric-clouds=half` sweep for shader errors.

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
