# HD cockpits: body cockpits, transplanted gauges and cockpit1 replacements

**ACTIVE PLAN** (written 2026-10-22). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

The HD models plan (closed at c1afada1e) replaces gamez meshes node by node and moves an airframe's
markers through `rig.json`, but it left the cockpit interior out: cockpit view hides the airframe's
`healthy` body and draws the original `cockpit1` interior, so a pack that models a cabin (the
Corvette gag pack, whose car has a full interior) still shows the stock autogyro cockpit from the
pilot's seat. This plan adds two ways for a pack to change the cockpit, in this order. First, a
**body cockpit**: a `cockpit` section in `rig.json` keeps the replaced body drawn in cockpit view,
hides `cockpit1`, and places the original's gauge assemblies and sight glass on the body. Second, a
**cockpit1 replacement**: `planes/<model>/cockpit1.glb` replaces the interior node by node in its own
units, with the same gauge table able to move its dials. Both work on the flat screen and under a
headset.

Out of scope: pack-authored needles (a pack's own needle meshes driven by the gauge laws), hiding
glTF sub-nodes inside a merged replacement, the nose view (mode 7), and anything another player
sees: cockpit view is local, so nothing here touches network play. No `backlog` issue feeds this
plan; its items come from this session's grilling and code reading, and are not re-verified against
the tracker.

**Where each item runs.** Every item is tagged:

- **[cloud]**: needs no game data. The code and its proof are engine-free unit tests or a suite over
  `--synthetic-data`, so it can be done in a cloud session (the SessionStart hook
  `.claude/hooks/cloud-setup.sh` installs the .NET 8 SDK, PowerShell 7 and CI's Godot build).
- **[local]**: needs the player's install and `extracted/` (real airframes, a look at the screen or
  the headset), so it runs on the development machine.

## Milestone goal

- A `rig.json` `cockpit` section with `from: "body"` makes cockpit view draw the replaced body from
  inside, with the eye at `cockpit_camera`, and hides `cockpit1`.
- Its `hide` list takes named gamez nodes out of the pilot's own pane only.
- Its `gauges` table places the original's gauge assemblies and sight glass: position, rotation and
  scale each, in plane-frame metres for a body cockpit and in interior units for a replaced
  `cockpit1`. The code that drives the needles, lamps, readouts, belts and damage zones is unchanged.
- `planes/<model>/cockpit1.glb` replaces the interior node by node; the airframe export writes a
  `cockpit1.glb` beside it that, put unedited into a pack, draws as the original.
- `--dump-markers` writes the `cockpit` section as a starting template, every assembly at the place
  the original pilot saw it.
- Both modes work under the headset.
- The Corvette pack (in `.scratchKeep`, never committed) has its driver's seat and dashboard dials.

**A pack's cockpit changes what the pilot sees, never what the aircraft is.** No collider, hit box,
gun line or gauge law changes, and a pack without a `cockpit` section and without `cockpit1.glb`
draws exactly today's cockpit.

## Decisions (2026-10-22)

| # | Question | Decision |
|---|---|---|
| 1 | Where a pack's cockpit comes from | **Both, body first**: a body cockpit (the replaced airframe seen from inside) lands before the `cockpit1.glb` replacement. The Corvette needs the first. |
| 2 | Instruments in a body cockpit | **Transplant the original's gauge assemblies**, placed by the rig. Not the screen-space dials, not pack-authored needles. |
| 3 | The sight glass in a body cockpit | **Rig-placed like a gauge, else none.** The 2D pipper draws in every view, so aiming needs no glass. |
| 4 | How body mode is switched on | **A `cockpit` section in `rig.json`** (`from: "body"`). Without it the airframe keeps `cockpit1`, so the unedited Bloodhawk round trip is unchanged. |
| 5 | Parts in the way of the view | **A `hide` list** of nodes that leave the pilot's own pane only, by the layer trade `CockpitVisibility` already does. |
| 6 | What the hide list names | **Gamez nodes only**, like every other rig rule. An author splits a part onto its own gamez node or edits the model. |
| 7 | VR | **In scope** for both modes, checked at the headset. |
| 8 | Gauge placement for a replaced `cockpit1` | **One table for both modes**: plane-frame metres for a body cockpit, interior units in `cockpit1`'s frame for a replaced interior. One reader, one validator, one doc table. |
| 9 | Where a `cockpit1` starting file comes from | **Beside the airframe export**: `--export-gltf` and F10 also write `cockpit1.glb`, in interior units with gamez names. |
| 10 | The rig template | **`--dump-markers` writes the `cockpit` section live** (`from: "body"`), each assembly carried through the interior mount into plane-frame metres. Deleting the section keeps `cockpit1`. |
| 11 | Who fills in the Corvette's section | **The implementer**, measuring seat and dashboard from the model in `.scratchKeep\corvette\scripts\build_corvette_pack.py`; the user judges the look flat and in the headset. |
| 12 | Defaults stated at the grilling's end | In a body cockpit: the torn-skin cockpit panels (`pcdp4`, `pcdp6`) go with `cockpit1`, and the pilot sees the body's own damage; `CockpitReseat` and the hiding of pylon stores are off, since the outside parts are real; transplanted dials keep `cockpit1`'s shader and lighting, so a shadowed dashboard cannot hide them; a bad `cockpit` section fails like any rig error (loud on the CLI, flown without the rig plus the menu notice on a menu launch). |

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

### Wave A, rig section and pack scope (all [cloud])

1. ☑ [cloud] `PlaneRig` `cockpit` section: reader and validation
2. ☑ [cloud] The interior builder gets the airframe's pack scope

### Wave B, body cockpits on the flat screen

11. ☑ [local] Body-mode visibility: the body stays, `cockpit1` goes, the hide list applies
12. ☑ [local] Transplanted gauge assemblies and sight glass on the body
13. ☑ [cloud] code, [local] check: `--dump-markers` writes the `cockpit` template
14. ☑ [local] The Corvette's seat and dashboard
15. ☑ [local] Flat body cockpits drawn steadily, through the cockpit pass

### Wave C, cockpit1 replacements

21. ☑ [cloud] code, [local] check: the export writes `cockpit1.glb`; unedited round trip
22. ☑ [local] Gauge table in interior units, and replaced dials that keep driving

### Wave D, under the headset

31. ◐ [local] Body cockpit under the headset
32. ◐ [local] Replaced `cockpit1` under the headset

## Dependency and parallelism notes

A1 and A2 are independent and can run in parallel (A1 owns `Mech3/PlaneRig.cs` and its tests, A2
owns the interior `SceneBuilder` set-up in `Mech3/PlaneBuilder.cs`). B11 needs A1. B12 needs B11.
B13 needs A1 and B12's mount arithmetic. B14 needs B12 and B13. C21 needs A2. C22 needs A1, A2 and
C21. D31 needs B12. D32 needs C22. The user's look checks (B14 flat, D31 and D32 headset) batch into
one ordered run at the end.

File contention: A2, B12 and C21 all edit `Mech3/PlaneBuilder.cs`; B11 and B12 both edit
`Flight/Hud/CockpitVisibility.cs` and the cockpit bind in `Session/Roster/HumanFlightAdapter.cs`;
D31 and D32 both edit `Flight/Hud/XrCockpit.cs`. Never run those pairs in parallel worktrees.

---

# Wave A, rig section and pack scope

## A1 ☑ [cloud] `PlaneRig` `cockpit` section: reader and validation

**Landed.** `PlaneRig.Cockpit` (`Mech3/PlaneRig.cs`) is a `RigCockpit`, or null when `rig.json` has
no `cockpit` key. The reader parses and validates only; nothing it reads is drawn yet. The schema:

```json
"cockpit": {
  "from": "body",
  "hide": ["g12", "roofnode"],
  "gauges": {
    "speedometer": { "position": [x, y, z], "rotation": [rx, ry, rz], "scale": s },
    "glass": { "position": [x, y, z] }
  }
}
```

- `cockpit` is an object with the keys `from`, `hide` and `gauges`, each optional; any other key fails.
- `from` is the string `"body"` (`RigCockpit.FromBody` true) or absent (false: the airframe keeps
  `cockpit1`). Any other value, the empty string included, fails.
- `hide` is an array of non-empty strings (`RigCockpit.Hide`, in file order). It fails without
  `"from": "body"`, even when empty. A name listed twice, compared without case, fails.
- `gauges` is an object from assembly name to pose (`RigCockpit.Gauges`, a dictionary whose lookup
  ignores case). A name defined twice, compared without case, fails.
- A pose (`RigGaugePose`) is an object with the keys `position`, `rotation` and `scale`; any other key
  fails. `position` is required: 3 finite numbers, plane-frame metres in body mode, `cockpit1`'s own
  frame and units otherwise. `rotation` is optional: 3 finite numbers, Euler degrees in Godot's YXZ
  order (basis `Ry * Rx * Rz`), the convention of `Node3D.RotationDegrees`, so B12, B13 and C22 can
  assign and read it without a conversion. `scale` is optional: one finite number above zero, a
  uniform factor. An absent optional field is null and keeps the assembly's own value in that frame.
- Non-finite numbers fail, including a literal past float range.

The name checks run in `PlaneRigPatch.CheckCockpit`, called at the top of `PlaneRigPatch.Apply` before
the first edit. They read the GAMEZ tree, never the built one, so an AI or non-interior build rejects
the same rig. Every failure is an `InvalidDataException` naming the file and the entry, so the menu
launch fallback (`PlaneRigPatch.PlayerNotice`, flown without the whole rig plus the notice) covers the
section unchanged.
- A `hide` name must be an airframe node outside `cockpit1`, `cockpit2`, `destroyed` and
  `player_damage_off`, matched without case like every other rig name.
- A `gauges` name must be a direct child of the first `gauges` node inside the root's direct child
  `cockpit1`, matched without case. The error lists the airframe's own names.
- The reserved key `glass` (`RigCockpit.GlassKey`) needs a polygon in `cockpit1` whose texture is
  `cockpit6` or `crosshair` (`GunsightReticle.IsGlass`).

The data's assembly names per player airframe (pinned by
`PlaneRigTests.EachPlayerAirframeAcceptsItsOwnAssembliesAndGlass`): every airframe has `comp`,
`altimeter`, `speedometer`, `damageindicator`, `gungauge`, `missilegauge` and `nitrogauge`. The
horizon assembly is `horizn` (Bloodhawk, Autogyro, Brigand), `horiz` (Devastator, Avenger, Balmoral,
Kestrel, Peacemaker), `horizm` (Fury) or `hparent` (Warhawk, Firebrand). The Brigand's and Firebrand's
sight glass is itself a `gauges` child (`g730`, `g184`); on every other airframe it sits elsewhere in
`cockpit1` (`inside`, `dashboard` or a `g` node under `inside`). B12 has to treat a rig naming both
`glass` and `g730` on the Brigand as one polygon placed twice.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** `rig.json` accepts a `cockpit` section with `from` (`"body"`; absent means the airframe's
`cockpit1`), `hide` (gamez node names) and `gauges` (assembly name to position, rotation and scale),
and rejects a malformed one as loudly as the existing sections.

**Evidence (confidence: lead-only).** The section's shape is this session's design, not code. The
existing reader and its loud validation are `Mech3/PlaneRig.cs` and `PlaneRigPatch.Load`
(`Mech3/PlaneBuilder.cs:275`), documented in `docs/formats/model-packs.md` (`rig.json`). The
assemblies the table names are the children of `cockpit1`'s `gauges` subtree, `altimeter`,
`speedometer`, `damageindicator`, `comp`, `horizn`/`horiz`, `gungauge`, `missilegauge`,
`nitrogauge` (`docs/formats/hud.md:71-77`), plus the sight glass, which is a polygon wearing
`cockpit6` or `crosshair` rather than a named assembly (`Mech3/GunsightReticle.cs:49`).

**Approach.** Extend the record and parser beside `markers`/`props`/`surfaces`/`flares`. Name
checks that need the built interior (an assembly the airframe lacks, a `hide` node the airframe
lacks) run where the rig is applied, as the existing node checks do. Decide the rotation encoding
<TODO: euler degrees vs axis-angle vs quaternion; the session did not settle it> and the sight
glass's key in the table <TODO: e.g. `glass`, since the glass is a polygon, not a node>.

**Model recommendation.** <TODO>

**Verify.** <TODO: unit tests in `CSVM.Tests` for the accepted shape and each rejection; the menu
launch fallback still drops the whole rig.>

**⚠ Traps.** An assembly's name differs between airframes (`comp` vs the binary's `compass`,
`horizn` vs `horiz`); `CockpitGauges` already resolves both (`Flight/Hud/CockpitGauges.cs:41-48`),
and the table must accept the data's names, not invent a canonical one without saying so.

## A2 ☑ [cloud] The interior builder gets the airframe's pack scope

**Landed.** `PlaneBuilder.MountCockpitInterior` sets the interior builder's `HdAirframe` to the
airframe builder's just before it builds `cockpit1`, and logs its own line, `HD models: N cockpit
interior mesh instance(s) of <model> drawn from the model pack`, beside the airframe's. No change to
`SceneBuilder`: the replacement path already runs on whichever builder draws the node, so a
replaced interior surface takes the interior's `DepthBiasScale`, gamma blend, alpha-coverage and
stereo-interior shader keys.

- **Exclusions checked.** `KeepsOriginalMesh` is set by `WorldBuilder` alone, never on a plane
  builder. The sprite, glow and facade rules and the meshless-node rule apply to the interior as to
  the airframe. Gauge parts (needles, lamps, readout cells, belt and zone quads) are ordinary
  meshes and are replaced like any node; their bindings ride the node.
- **C22's question.** A texture-named replacement surface does land in the interior's
  `TexturedMaterials`: `MappedReplacement` resolves it through `OriginalMaterialFor` to a gamez
  material index and takes `GetMaterial`, whose cache miss runs `BuildMaterial`, which registers
  every textured material at `SceneBuilder.cs:2151`; a cache hit returns one already registered.
  The exceptions are the original's own: a billboard texture (no match) and an absent texture (the
  grey/magenta fallback). A non-texture-named surface wears `PackMaterial` and is never in the list.
  The suite checks the instance id against `PlaneBuilder.InteriorMaterials`.
- **The airframe root file.** Yes, it reaches the interior: an interior node's ancestor walk passes
  `cockpit1` and then the airframe root, so `planes/<model>/<model>.glb` replaces an interior node
  it holds a glTF node for (suite case `Root`). No meshed interior node on any of the ten airframes
  shares a name with a node outside `cockpit1` (`extracted/planes/nodes.json`), so the exported
  round-trip file, which holds only exterior names, reaches none. Names repeat inside one interior
  (`g784` under both `altimeter` and `speedometer` on the Bloodhawk), which the root file's
  path match settles, so C21's export must keep the interior's hierarchy.
- **Suite `hd-model-cockpit1`** (`Testing/HdModelSwapSuites.cs`, ci tier): `cockpit1.glb`, the
  airframe root file and a per-node file each replace exactly the one named interior node
  (`damageindicator` on the install, the dash on the synthetic tree) with the pack's mesh in the
  node's frame; a pack holding only an exterior node replaces no interior node. On the install the
  gauge bind over the replaced `damageindicator` finds the same needles, lamps, readouts, belts and
  zones as without the pack (`CockpitGauges.NeedleCount`, `LampCount`, `ReadoutCount` added).
  Without the `HdAirframe` line the suite fails.
- **For D32.** The stereo path's head-plate cut omits gamez polygons by identity, so a replaced
  node carrying the plate draws whatever the file holds. `GunsightReticle.Find` reads the gamez
  glass polygons and the overlay is added whenever the glass texture was split, which the
  replaced node's own gamez mesh still does (it is built for `GamezMeshMeta`), so the collimated
  reticle stays at the original glass's place whatever the replacement draws, and a
  texture-named `cockpit6`/`crosshair` replacement surface wears the reticle-free glass layer.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** `planes/<model>/cockpit1.glb` (a root-scope file) and per-node files under the airframe
key reach the nodes of `cockpit1`, and the "never replaced: the cockpit interior" rule leaves
`docs/formats/model-packs.md`.

**Evidence (confidence: traced).** The interior is built on its own `SceneBuilder`
(`Mech3/PlaneBuilder.cs:129-141`) that never sets `HdAirframe`, and `SceneBuilder.Replacement`
returns null for a builder with neither `HdChapter` nor `HdAirframe` (`Mech3/SceneBuilder.cs:1526`).
The rule is stated at `docs/formats/model-packs.md:161`. A root file is looked up from the nearest
ancestor outward (`Mech3/SceneBuilder.cs:1546-1561`), so a node under `cockpit1` tries
`cockpit1.glb` before the airframe's root file.

**Approach.** Set the interior builder's `HdAirframe` where the airframe builder's is set, and count
its replacements in the build's `HD models: N mesh instance(s)` log line. <TODO: whether an
airframe root file (`player_bhawk.glb`) holding no interior nodes can ever match one; the round trip
file has none (measured below)>.

**Model recommendation.** <TODO>

**Verify.** <TODO: a synthetic-data suite with a generated `cockpit1.glb` replacing one interior
node; the replacement is drawn and every gauge still binds (`cockpit gauges bound` debug line,
`Flight/Hud/CockpitGauges.cs:58`).>

**⚠ Traps.** The interior builder bakes `DepthBiasScale = 1 / InteriorScale` into its meshes and
materials (`Mech3/PlaneBuilder.cs:124-134`); a replacement surface must go through the same builder,
never the airframe's. The interior is in its own units, about 25 per metre at `InteriorScale` 0.04
(`Mech3/PlaneBuilder.cs:21-26`), and the airframe and interior "are not a similarity apart", so a
replacement is authored in the export's interior units, never converted from metres.

# Wave B, body cockpits on the flat screen

## B11 ☑ [local] Body-mode visibility: the body stays, `cockpit1` goes, the hide list applies

**Landed.** The rig's section reaches the flight code by one path, which B12 extends:
`PlaneBuilder.Cockpit` (`Mech3/PlaneBuilder.cs`) is the `RigCockpit` of the rig `Build` applied, set
right after `PlaneRigPatch.Apply` returns and null for no rig, no section, or a rig the player's launch
left out. `HumanFlightAdapter.Assemble` passes it to `CockpitVisibility.Bind(..., cockpit:)`, and the
bind's `BodyCockpit` is what `FirstPersonDressing.Show` reads.

- **Rules.** `CockpitVisibility.Rules(mode, firstPerson, bodyCockpit)` changes the first-person Cockpit
  pose alone: body, `markers` and `dontmove` drawn, interior hidden, and the new `Shown.Listed` false.
  Nose and every external pose answer as without the section. The reseated parts and stores follow
  `Interior`, so they stay in view.
- **Hide list.** `Bind` collects every node a `hide` name matches, without case, outside `cockpit*`
  subtrees (the rig rules' every-node-of-that-name match; `FindGroup` itself is unchanged). `Apply`
  moves them off `OwnAirframeLayer` onto `FirstPersonLayer` with the same `Move`, after the groups,
  so a hide node inside a hidden group stays out. Nothing goes through `Visible`.
- **Panel.** `FirstPersonDressing.Show` asks the same rule with the bind's `BodyCockpit`, so a body
  cockpit drives no `cockpit1` panel and keeps the screen-space cluster on. B12 turns that when the
  transplanted dials draw.
- **Reseat.** The adapter binds no `CockpitReseat` for a body cockpit (decision 12).
- **Headset.** The adapter hands the bind no section under the stereo path (`xrInterior`, which is
  `XrCockpit.Live` for a seat with a pane), so a headset seat keeps `cockpit1`. The one-line comment
  above `bodyCockpit` is where D31 lifts it.
- **Eye and near plane.** The eye stays at `CockpitCameraOffset`, which the rig's `cockpit_camera`
  already moves. Every flight camera keeps Godot's default near plane, 0.05 m: neither the Launcher's
  camera nor a split-screen pane sets one, and only the cockpit pass (0.01) and the spyglass and photo
  cameras do. The Corvette pack's autogyro built in body mode puts the eye at (0, 0.75, -0.2) with the
  nearest drawn surface 0.77 m away (no triangle within 0.25 m): rays from the eye hit the car
  0.77 m below and 1.37 m behind, nothing ahead or to either side, and above only the rotor, at
  1.29 m. The eye sits above the car's roofline, so the near plane clips nothing there, and the cockpit
  view shows sky and ground with no car in frame. B14 seats the eye in the cabin. A seated eye clips
  only a surface within 5 cm of it, closer than a headliner or pillar sits to a driver's head, so
  the default near plane needs no change for a real-scale cabin.
- **Tests.** `CockpitVisibilityTests` (`ABodyCockpitKeepsTheBodyAndDropsTheInteriorAndTheHideList`,
  `ABodyCockpitLeavesNoseAndEveryExternalPoseUnchanged`). Suite `plane-rig-cockpit-body`
  (`Testing/PlaneRigSuites.cs`): a pack `rig.json` with `"from": "body"` hiding a body leaf reaches
  `PlaneBuilder.Cockpit` and the bind; in Cockpit the pilot's pane draws the rest of the body and none
  of the hide node or the interior, a second pane draws the whole body, nothing is hidden through
  `Visible`, Nose drops the body and an external pose draws all of it.
- **The look.** `.scratch/b11-shots/corvette-body-cockpit.png` (the Corvette pack with
  `"cockpit": { "from": "body" }`) and `.scratch/b11-shots/corvette-cockpit1-no-section.png` (the
  pack as shipped, the stock `cockpit1`).

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** With `from: "body"`, cockpit view draws the replaced body for the pilot, draws no
`cockpit1` shell, and takes each `hide` node out of the pilot's own pane; every other view, every
other pane and the nose view are unchanged.

**Evidence (confidence: traced).** `CockpitVisibility.Rules` shows the interior and hides `healthy`
in cockpit view (`Flight/Hud/CockpitVisibility.cs:50-54`), and hides by trading the pilot's airframe
layer for the first-person layer, never through `Visible`, since panes share one tree (`:82-115`).
`CockpitReseat` copies wingtip lights and prop discs onto the interior, and its originals and the
pylon stores leave the pilot's pane while the interior shows (`Flight/Hud/CockpitReseat.cs:12-18`,
`CockpitVisibility.cs:22-24`).

**Approach.** Give `Rules` the body mode: body shown, interior shell hidden, reseated parts and
stores left in view, the hide list moved by the same `Move`. Turn `CockpitReseat` off for a body
cockpit. The eye is `cockpit_camera` as the rig already sets it.

**Model recommendation.** <TODO>

**Verify.** <TODO: a unit on `Rules` for the body mode; a split-screen check that the other pane
still sees the hidden nodes; a Corvette flight in cockpit view.>

**⚠ Traps.** `FindGroup` is breadth-first and skips `cockpit*` subtrees because the gauge
sub-assemblies carry their own `markers` children (`CockpitVisibility.cs:143-167`); keep that when
the gauges move out of `cockpit1` (B12). <TODO: camera near plane against a real-scale cabin around
the eye; the session did not measure it.>

## B12 ☑ [local] Transplanted gauge assemblies and sight glass on the body

**Landed.** A flat build with a body cockpit builds `cockpit1` as before, then
`CockpitTransplant.Build` (`Mech3/CockpitTransplant.cs`, new) moves the rig's named assemblies out of
it onto the airframe, exposed as `PlaneBuilder.CockpitPanel`: a hidden `cockpit_panel` node under the
model root holding a `gauges` node (only when an assembly moved) and a `glass` node (only when named).
The rest of `cockpit1` stays built and hidden, so its torn panels and bullet holes go with it.

- **Re-parented, not rebuilt.** Each named assembly is the built node itself, moved with its meshes,
  materials and metas, so `CockpitGauges.Bind(PlaneBuilder)`, which now prefers `CockpitPanel` over
  `CockpitInterior`, binds the same needles, lamps, readouts, belts and zones through the unchanged
  `InteriorMaterials`. An assembly the table does not name stays in the hidden interior and is not
  bound. The panel's `cockpit` prefix keeps it out of `FindGroup`, `CockpitReseat` and
  `ControlSurfaceAnimator`'s interior-blind walks, which the assemblies' own `markers` children need.
- **Mount arithmetic.** `PlaneBuilder.FlatMount(marker, scale)` is the flat mount the interior already
  took (marker origin, `HeadPitchOffsetRad` tilt, `InteriorScale`); `InteriorToPlane` carries an
  interior-frame transform through it and `InteriorFromPlane` back, for B13 and C22.
  `CockpitTransplant.Posed(original, pose)` sets the pose's position and, where given, its YXZ rotation
  and uniform scale over the assembly's original plane-frame transform, so a position-only pose keeps
  the dial's original apparent size (0.04 m per unit) and facing. `CSVM.Tests/CockpitTransplantTests.cs`.
- **The glass.** `CockpitTransplant.GlassFrame` puts the glass's frame at the centre of its polygon's
  corners (`GunsightReticle.Find`), unrotated in the interior frame; a rig's glass position is that
  centre, and B13 writes the template from the same function. Every built surface whose material
  `InteriorMaterials` names `cockpit6`/`crosshair` is copied into a one-surface `ArrayMesh` on a new
  `MeshInstance3D` (layers, shadow setting and `node_bias` copied) under `glass`. ⚠ Not
  `Node.Duplicate()` of the source instance: under the shipped separate render thread that crashes
  the process (0xC0000005) on the first frames, every run, while `--render-thread safe` and headless
  survive it. `PlaneRigPatch.CheckCockpit` fails a rig naming `glass` beside an assembly that holds the
  glass (the Brigand's `g730`, the Firebrand's `g184`), naming the file and both keys.
- **When it shows.** `CockpitVisibility.Shown` gains `Transplant`, true exactly where the interior
  would be (`Rules(..., bodyCockpit: false).Interior`); `Bind` takes the panel. `Apply` sets the
  panel's `Visible` and, on each show, stamps it onto `SplitScreen.WorldFlareLayer(pilot)`, the band
  only that pilot's pane camera draws (the spyglass disc and the Danger Zone photograph drop it), since
  `SeatAirframe` stamps the whole model after the bind. `FirstPersonDressing.Show` turns the
  screen-space cluster off for the transplant only when `Panel` bound, so a table with no assembly
  (glass alone, or nothing) keeps the 2D dials. The headset path keeps `cockpit1`, as B11 left it.
- **Lighting.** `SceneBuilder.NoShadowReceive` (new key bit, `shadows_disabled`) is set on the interior
  builder for a flat body cockpit before it builds, so the airframe's shadow over its dashboard cannot
  dim the dials under Enhanced (decision 12); shader, gamma blend and no-coverage choices are
  `cockpit1`'s own. The transplanted glass composites as `cockpit1`'s glass does under
  `--no-cockpit-pass` (main world), fainter than through the cockpit pass's premultiplied composite;
  compare `stock-cockpit1-no-pass.png` with `stock-cockpit1.png`.
- **Depth bias.** No change was needed. The bias is `VERTEX *= 1 - bias` in view space, a fraction of
  view distance, so a dial's own layers keep their order at any rig scale or distance; the 25x
  `DepthBiasScale` makes one priority level 0.5 % of the distance (face 0.005, needles 0.04 and 0.045
  of it), millimetres at a dashboard's 0.5 to 0.8 m, far above reverse-Z float precision there. The
  suite reads face < needle on the moved dial's own materials; `corvette-scaled-dials.png` shows a
  dial at 3.75x and one at a quarter scale with needle, lamp window and face in order. The cost is
  that a priority-9 needle is pulled 4.5 % of its distance toward the eye, so a body surface less than
  that in front of it (about 3 cm at 0.6 m) loses to it; B14 should keep hoods and wheel rims clear by more.
- **Tests.** `CockpitTransplantTests` (7), `CockpitVisibilityTests.ATransplantedPanelShowsExactlyWhereCockpit1Would`,
  `PlaneRigTests.TheGlassKeyAndTheAssemblyHoldingTheGlassMayNotBothBeNamed` and the per-airframe theory
  (the Brigand and Firebrand reject `glass` with their holder). Suite `plane-rig-cockpit-gauges`
  (`Testing/PlaneRigSuites.cs`, install only): every assembly at its original place but the
  speedometer moved, turned and scaled, plus the glass; the speedometer stands at `Posed`'s pose and
  `InteriorToPlane` matches the built mount; the bind counts equal the stock `cockpit1`'s; the moved
  `speed` needle reads `GaugeCluster.SpeedAngleDeg`; the glass is one cut-loose glass surface at the
  rig's centre; the panel draws in the pilot's pane (split and alone) and in no second pane, disc or
  photograph, and hides in Nose and outside; naming only the speedometer moves it alone, and a body
  section naming nothing binds no panel.
- **The look.** `.scratch/b12-shots/corvette-body-cockpit.png` (the Corvette pack, every autogyro
  assembly and the glass at positions only, about 15 cm further out than `cockpit1` drew them), against
  `stock-cockpit1.png`. The eye still sits above the car's roof until B14, so the dials float over the
  ground.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** In a body cockpit, each assembly the `gauges` table names is drawn at its rig pose on the
airframe, with its needles, lamps, readouts, belts and damage zones driven as in `cockpit1`; an
unnamed assembly is not drawn, and the glass is drawn only when named.

**Evidence (confidence: traced for the binding, lead-only for the mount).** `CockpitGauges.Bind`
finds the `gauges` subtree by name and binds every driven part by node name or by the texture a
material resolved from (`Flight/Hud/CockpitGauges.cs:29-58`, `:72-80`). Needles, the horizon ball
and the compass drum rotate in their own node's frame (`:200-308`), so moving an assembly's root
carries them. Readouts are one surface per character, ordered by x (`:371-453`). Dials are flat, in
dial-local coordinates with bezel radius 1 (`docs/formats/hud.md:77-78`).

**Approach.** Build the interior as today, then re-parent each named assembly (and the glass
polygon's node) under the airframe at the rig's pose, and hide the rest of `cockpit1`. Bind
`CockpitGauges` to the moved assemblies. Keep `cockpit1`'s shader, lighting and depth-bias choice
for the dials (decision 12). <TODO: the depth bias at a metre-scale mount, since the interior
builder's bias is baked for 0.04; how the glass, a polygon of a larger mesh, is cut loose.>

**Model recommendation.** <TODO>

**Verify.** <TODO: a suite that moves `speedometer` and reads the `speed` needle's angle against
`GaugeCluster`; a Corvette cockpit-view screenshot with dials on the dashboard.>

**⚠ Traps.** `CockpitGauges.Bind` takes the interior's materials for the texture lookup
(`InteriorMaterials`, `Mech3/PlaneBuilder.cs:208`); a transplant that rebuilds surfaces must keep
them in that list or the belts and damage zones go silent (the bind's debug line counts them).

## B13 ☑ [cloud] code, [local] check: `--dump-markers` writes the `cockpit` template

**Landed.** `MarkerRig.Extract` (`Mech3/MarkerRig.cs`) also reads `CockpitPoses`, so every dumped
`./.scratch/rigs/<model>/rig.json` of an airframe whose root has a `cockpit1` child carries
`"cockpit": { "from": "body", "gauges": { ... } }`. `ProbeRunner.DumpMarkers` is unchanged.

- **Poses.** Each child of `cockpit1`'s first `gauges` node (depth-first, as `CheckCockpit` and the
  transplant find it), in file order, is its gamez locals below `cockpit1` carried through
  `PlaneBuilder.InteriorToPlane` on the gamez `cockpit_camera`. `cockpit1`'s own local is left out, since
  the built mount replaces it. The glass is `CockpitTransplant.GlassFrame(GunsightReticle.Find(...))`
  through the same mount. Every entry writes `position`, `rotation` (YXZ degrees of the orthonormalised
  basis) and `scale`, rounded to a micrometre, 1e-4 degree and 1e-7, so float noise such as `2E-14`
  never reaches the author. Every dumped pose is a rotation times a uniform positive scale (0.04), so
  the three fields express it exactly.
- **Brigand and Firebrand.** An assembly holding a glass polygon (`PlaneRigPatch.HoldsGlass`, now
  internal, over its subtree) stands in for `glass`, which is then not written. `g730` and `g184` each
  hold every glass polygon their `cockpit1` has, so naming the holder draws the whole glass.
- **Tests.** `MarkerRigTests`: a synthetic airframe's template poses each assembly and the glass
  through the flat mount, skips `cockpit1`'s local, reads back through `CockpitTransplant.Posed`, and
  passes `CheckCockpit`; a speedometer holding the glass replaces the key; the real-data theory over
  the eleven player airframes checks names, `CheckCockpit`, uniform scale, read-back, and the holder's
  whole glass. Suite `plane-rig-cockpit-template` (`Testing/PlaneRigSuites.cs`, install only) takes each
  template from `Probes.Markers`, copies it unedited into a pack with no model, and builds every player
  airframe against its stock `cockpit1` build: every assembly's plane-frame transform within 0.1 mm
  (measured 0.0006 to 0.0007 mm), every glass vertex within 0.1 mm (measured at most 0.0009 mm), and
  the same needle, lamp, readout, belt and zone counts.
- **The look.** `.scratch/b13-shots/bloodhawk-stock-cockpit1.png` against
  `bloodhawk-dumped-template-body.png`: by eye the dials sit at the same places; the template shot also
  draws the Bloodhawk's own body around them, and its glass composites fainter, as B12 recorded.
- **Flat eye against stereo eye (for D31).** `StereoMount` with the factor 1.0 every airframe has in
  `vr_cockpits.json` puts each dial this far from the flat mount, before the headset's `eyeOffset`:
  Bloodhawk 5 to 14 mm, Devastator 7 to 12, Fury 4 to 10, Hoplite 5 to 11, Hellhound 7 to 15,
  Balmoral 5 to 11, Firebrand 26 to 59, Brigand 36 to 49, Peacemaker 72 to 73, Kestrel 85 to 89,
  Warhawk 150 to 163, plus the flat mount's 4.7° head-pitch tilt the stereo mount lacks. The gap grows
  with the marker's distance from the airframe origin (0.04 × marker), so D31 cannot take the
  template's poses as they stand on the airframes whose marker sits far from the origin.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** The dumped `rig.json` carries `cockpit: { from: "body", gauges: {...} }` with every
assembly and the glass at the original's apparent place, in plane-frame metres.

**Evidence (confidence: lead-only).** `--dump-markers` writes `./.scratch/rigs/<model>/rig.json`
(`docs/formats/model-packs.md:126`). The flat interior mount is `cockpit_camera`, `InteriorScale`
and the head-pitch tilt `HeadPitchOffsetRad` (`Mech3/PlaneBuilder.cs:21-33`, `:463-506`); carrying
an assembly's interior pose through it gives the plane-frame pose.

**Approach.** Reuse the mount arithmetic B12 uses, so a template copied unedited draws each dial
where `cockpit1` drew it.

**Model recommendation.** <TODO>

**Verify.** <TODO: dump the Bloodhawk, copy the rig into a pack with no model, and compare
cockpit-view screenshots against stock `cockpit1` for dial placement.>

**⚠ Traps.** The flat eye is the interior's origin, the stereo eye is the `cockpit_camera` numbers
in interior units (`Mech3/PlaneBuilder.cs:198-202`); the template follows the flat mount, and D31
must not inherit a pose that only lines up on the flat screen. The dumped rig is no longer a no-op:
copying it unedited switches the cockpit to body mode (decision 10), which the doc must say.

## B14 ☑ [local] The Corvette's seat and dashboard

**Landed.** `.scratchKeep\corvette\scripts\build_corvette_pack.py` step 4c measures the cabin by
rays through the placed car and writes `cockpit_camera` and a `"cockpit": { "from": "body",
"gauges": {...} }` section into each pack's `planes\player_autogyro\rig.json`; the AI `autogyro`
rig keeps its markers alone. Nothing is tracked or committed; the pre-B14 rigs and script are in
`.scratchKeep\corvette\backup-b14\`. The build commands are `-- <car.glb> <pack> 4.57 -0.5` (real)
and `-- <car.glb> <pack> 6.7 -0.6` (stretched).

- **The driver.** The seat is the port half of the seat material (`Mesh9Mtl`); the cushion is the
  lowest point downward rays find along its centreline; the eye is 0.70 m above that and 0.20 m
  ahead of the backrest at eye height. Human sizes scale with the car (`k` = length / 4.57), so the
  stretched car seats a 1.47x driver and frames the cabin as the real one does (its shots match the
  real car's).

  | Variant | Seat x | Cushion y (at z) | Backrest z | Eye | Nearest surface | Headliner above |
  |---|---|---|---|---|---|---|
  | real (k 1) | -0.381 | -0.551 (1.484) | 1.706 | (-0.381, 0.149, 1.506) | 0.187 m | 0.225 m |
  | stretched (k 1.466) | -0.559 | -0.431 (1.656) | 1.982 | (-0.559, 0.595, 1.688) | 0.274 m | 0.330 m |

  No surface is within 5 cm of either eye, so the default near plane clips nothing.
- **The binnacle.** The hood is the dash top walked from the windscreen toward the driver until it
  drops (real: y 0.076, lip z 0.756); the instrument face is what a forward ray from behind the lip
  finds. The steering wheel's rim and airbag hub leave the eye a strip of that face (real: y -0.084
  to 0.011 where both dials stand), and the speedometer (left) and altimeter (right) fill it, 0.15 m
  apart at a bezel radius of 4.5 cm (6.6 cm stretched). The rim covers the speedometer's left edge
  (about a quarter of its sample points), as a real wheel covers a gauge.
- **The centre stack.** Below the flat dash top (real y 0.023): `gungauge`, `damageindicator` and
  `missilegauge` in a row 7 cm under the top, all at 4 cm radius, and `nitrogauge` (3 cm units)
  below the row's right end. `horizn` is left out: on the stack it stood half hidden in the centre
  console, and the user had it removed rather than moved. The compass
  drum stands on the dash top at the stack's rear edge, its window tilted 10 degrees back from
  facing, as `cockpit1` tilts it.
- **Facing and clearance.** Every assembly faces the eye with its x level (YXZ: pitch
  `-asin(d.y)`, yaw `atan2(d.x, d.z)`). Each is moved along the sight line off its surface until no
  sample point (bezel rim, drum window, bar corners) has a car surface nearer the eye by less than
  4.5 % of its distance (3 cm at least) or less than 1 cm behind it, B12's bias band: 3 to 4 cm on
  the real car, 4.4 to 5.9 cm stretched. Every assembly passes with no point in that band.
- **The glass.** The original's sight glass offset from its eye (`--dump-markers`: 0.448 m ahead,
  1.4 cm low, tilted -4.7 degrees, scale 0.04), scaled by `k`, so the painted crosshair sits on the
  2D pipper as in `cockpit1`: real (-0.381, 0.134, 1.058), stretched (-0.559, 0.575, 1.032) at
  scale 0.0586.
- **Regeneration.** The pylon loop reused `k` as its counter, which moved the wreck's front-bumper
  cut to 1.65 m behind the nose; the loop now counts with `step`, and the wreck pieces regenerate
  byte-identically to the shipped packs. Of the 124 pack files only the rigs changed: both
  `player_autogyro` rigs (the new entries) and the real `autogyro` rig, whose `pylon3`/`pylon4` x
  the script rounds to 0.261 where the shipped file held 0.26. The source `.glb` is unchanged.
- **Not seen.** `nitrogauge` draws only with a nitrous injector fitted (`CockpitGauges`), which the
  CLI cannot fit, so no shot shows it; its placement passed the clearance check. The Deck's copy is
  not updated.
- **The look.** `.scratch/b14-shots/corvette-<real|stretched>-level.png`, `-look-left.png` (door,
  side window and mirror), `-look-right.png` (passenger seat, stack and compass) and `-bank.png`
  (rolled right).

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id). At the controls the user keeps the stretched pack and judged its seat and dials good, the dials standing partly on the stack's vents, which the gag does not need perfected; the horizon was dropped on that verdict.

**Original approach (kept for reference).**

**Goal.** Both Corvette packs (`.scratchKeep\corvette\HDModels-real`, real and stretched, and the
stretched copy on the Deck's `~/CSVM/HDModels/corvette`) seat the eye in the driver's seat with the
dials on the dashboard; the user judges it at the controls.

**Evidence (confidence: lead-only).** The car is one merged mesh on `nose` (and the LOD copies
`g437`, `g59`; each `.glb` 2998972 bytes), so the hide list cannot remove parts of it (decision 6).
The pack is built by `.scratchKeep\corvette\scripts\build_corvette_pack.py`.

**Approach.** Measure the seat and the dashboard in the build script, write `cockpit_camera` and the
`cockpit` section into both rigs, and send cockpit-view screenshots before the user's check.

**Model recommendation.** <TODO>

**Verify.** The user's look at the controls, flat (and in the headset with D31).

**⚠ Traps.** Third-party asset: never commit the pack or put it in a tracked path, and leave the
original file untouched.

## B15 ☑ [local] Flat body cockpits drawn steadily, through the cockpit pass

**Landed.** On the flat screen, with the cockpit pass on, a body cockpit's pass holds the
transplanted panel and a twin of each drawn body mesh, all at small numbers around the pass's
origin. `--no-cockpit-pass` keeps the main-world body cockpit, and a rig without `"from": "body"`
takes the stock path unchanged.

- **Twins.** `PlaneBuilder.FlatBody(built, panel, hide)` (`Mech3/PlaneBuilder.cs`) hangs a hidden
  twin under the panel for each `mesh` instance of a gamez node, choosing the meshes as D31's
  `PairTwins` does. A twin shares its original's `Mesh`, `node_bias` and shadow setting, and wears
  `ShaderTwins.Copy` copies of its materials, one per source material, because the pass writes
  `light_origin` on every material it holds. No second airframe build is needed. `HumanFlightAdapter`
  makes them for a flat seat with a pane, after the race-ghost stamp and before the stores hang. The
  dressing's `Interior` is then the panel and its new `Body` holds the eye (`cockpit_camera`) and the
  twins.
- **The pass.** `GameSession.BuildCockpitPasses` builds through the new
  `FirstPersonDressing.BuildPass`. It calls `CockpitOverlay.BuildBody` for a body cockpit and
  `Build` otherwise. The body's mount is the model root's local transform times the panel's, with the
  eye subtracted. `Sync` places it as `attitude × (wobble × mount.Origin − eye)`, so the body turns
  about the shake pivot as its originals do. The stock interior's mount origin and eye are both zero,
  so its transform is the one it had. Each `Sync` gives every twin its original's plane-frame pose
  (`PlaneRigPatch.PlaneFrame` to the model root, through `XrCockpit.InInterior`) and
  `IsVisibleInTree`, the minimal copy of `XrCockpit.Mirror`'s loop. Each original joins
  `CockpitVisibility.Reseat`, so it leaves the pilot's pane while the panel is drawn. A second pane
  and every outside view keep drawing the originals. `CockpitVisibility` is unchanged.
- **Pass camera.** It now culls nothing (`CullMask = uint.MaxValue`). The panel wears
  `SplitScreen.WorldFlareLayer` (bit 20 and up), past Godot's default 20-bit mask, so the pass
  first drew an empty frame. The stock interior's layers all sit under bit 20.
- **What the pass does not take.** The hide list, `cockpit*` subtrees and meshes under an override
  material (the flares) get no twin, as in D31. Point-sprite `lights` are not `mesh` instances.
  Pylon stores and anything else hung after the twins are made also get none. They stay in the
  main world under the pass, which matches decision 12 (stores in view, not hidden). The pass draws
  over the world, so a store, smoke or a world object nearer than a body surface hides behind it, as
  it does behind the stock interior (the trap below). No other class of body part is held back:
  windows and canopies have to be in the pass, because the body seen through them is.
- **Look.** The dials keep `cockpit1`'s shader and no shadow (decision 12). The twins cast and
  receive the pass sun's shadow among themselves, but a world object's shadow no longer reaches the
  body, as with the stock interior. The sight glass now composites as stock `cockpit1` does through
  the pass (`origin-pass.png` against `origin-no-pass.png`, glass region mean 71 against 81). The
  car's translucent windows darken in the same way: sky seen through the windscreen reads 69 against
  97 under `--no-cockpit-pass`, and through the side window 66 against 100. Opaque surfaces match
  exactly. This is the pass's after-tonemap composite of a translucent surface (SHOT-46), the same
  composite that makes the stock glass read stronger. Whether the window tint is acceptable is the
  user's look, owed with B14.
- **Tests.** Suite `plane-rig-cockpit-pass` (`Testing/PlaneRigSuites.cs`, install only, headless)
  runs on the Bloodhawk with a body section that hides a body leaf and names a moved, turned and
  scaled speedometer and the glass. It checks that 32 of 32 drawn body meshes are twinned and the
  hide node is not, with mesh, shader and bias shared and the materials copied, and that every twin
  starts hidden. The pass must take the panel into its own viewport, and its camera must draw the
  panel. On a rotated pose 20 km out with a wobbled pivot, each shown twin stands at its original's
  offset from the eye within 0.98 µm, and within 1 cm of the world's float32 globals. The speedometer
  must stand at the rig's pose, `light_origin` must reach the twins and none of the originals, and
  the pilot's pane must drop every twinned original and the hide node while a second pane draws
  them. A turned original turns its twin and a hidden one hides it. An external pose takes the pass
  off and gives the originals back.
- **Jitter and registration** (SHOT-45, SHOT-48, INSTR-30; least-squares sub-pixel shift with gain
  and offset fitted, `.scratch/b15-jitter/reg.py`, ten regions clear of the pipper, four of them
  dials). The run used the Corvette real pack (`.scratch/b15-pack`) on `--stage=empty
  --plane=player_autogyro --view=cockpit --direction=-0.743,0,-0.669 --frames=60 --shots=24`. At
  `--pos=14142,400,14142` (20 km) the pass's worst frame-to-frame step is 0.006 px, a speedometer
  needle, and 0.002 px over the body regions. Under `--no-cockpit-pass` at the same pose it is
  3.76 px (mean 0.36 to 1.15 px per region; D31 measured 2.43 px over its regions). At the origin the
  figures are 0.006 px with the pass and 0.063 px without. The pass at the origin registers with the
  `--no-cockpit-pass` frame within 0.07 px in nine regions. The tenth, the A-pillar, reads 0.20 px
  because it takes in the darker side window. The pass at the origin and at 20 km agree within
  0.07 px.
- **Shots** (`.scratch/b15-shots/`): `origin-pass.png` against `origin-no-pass.png`, and
  `far-20km-pass.png` against `far-20km-no-pass.png`.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** On the flat screen a body cockpit (the body seen from inside, its transplanted dials and
its glass) draws as steadily far from the chapter origin as the stock interior does.

**Evidence (confidence: direction-sound).** D31 measured the flat body cockpit's worst frame-to-frame
step over 24 frames 20 km out at 2.43 px, against 0.006 px for its stereo twin (D31's commit
message). The flat stock interior does not jitter because `Flight/Hud/CockpitOverlay.cs` draws it
in a world of its own, camera and interior both at the origin, so float32 rounds the remaining
camera-relative pose far below a pixel; B11 and B12 left the body and the transplanted panel in the
main world at chapter-scale coordinates. B12 also saw its transplanted glass read fainter than
through the cockpit pass, the same as stock `cockpit1` under `--no-cockpit-pass`.

**Approach.** Draw a flat body cockpit through the cockpit pass: the transplanted panel, and twins of
the drawn body meshes kept in step with their originals as D31's `XrCockpit.Mirror` does, with the
originals leaving the pilot's pane while the pass draws them. <TODO: what of the body the pass must
not take, e.g. parts whose depth against the world matters.>

**Model recommendation.** <TODO>

**Verify.** The same jitter series as D31 on the flat path (24 frames, 20 km, a rotated pose), and a
flat registration against the current main-world body cockpit at the origin.

**⚠ Traps.** The pass composites over the main view, so a world object nearer than a twinned body
surface hides behind it, as it does behind the stock interior.

# Wave C, cockpit1 replacements

## C21 ☑ [cloud] code, [local] check: the export writes `cockpit1.glb`; unedited round trip

**Landed.** The interior was left out because `PlaneBuilder` builds it hidden and the exporter frees
every hidden `Node3D`; in cockpit view it would have ridden in the airframe file, mounted.
`GltfExporter.Export` now takes an optional `cockpitPath`. It finds the mounted interior in its
copy (the direct child of the plane root whose gamez name is `cockpit1`, where
`PlaneBuilder.MountCockpitInterior` adds `CockpitInterior`), detaches it before the airframe write,
and writes it to `cockpitPath` with every state kept (`allStates`), shown, and its root `cockpit1`
at identity: no 0.04 mount, no tilt, no `cockpit_camera` offset, so each node is in the interior's
own frame and units with the gamez hierarchy intact.

- **Call sites.** `--export-gltf=<path>` writes `cockpit1.glb` (or `.gltf`, following the path's
  format) beside the airframe file (`GltfExporter.CockpitPathBeside`), the key a pack reads it
  under. F10 and NodeLab's single export (`ExportToExports`) write `<file>_cockpit1.glb` beside the
  timestamped file, so a later export never overwrites an earlier interior; the author renames it
  `cockpit1.glb` in the pack. `Launch/Launcher.cs` is unchanged: both call sites already go through
  `Tick` and `ExportToExports`. A subtree with no mounted interior (a world node, an AI build) writes
  no second file.
- **The airframe file drops the interior always.** Two copies would not fight on the round trip
  (`cockpit1.glb` is the nearer root and wins), but an author editing the interior in the airframe
  file would see no change, and the file would carry a mounted copy at 0.04 and the head tilt. One
  file per scope keeps one source.
- **Untextured unlit surfaces.** The first round-trip shot differed at the artificial horizon's ball:
  `pfhorizon`'s surfaces are untextured on a `lighting: false` model, exported under a writer-given
  name, and a non-texture-named replacement surface is lit by default (`SceneBuilder.MappedReplacement`).
  The exporter now writes `KHR_materials_unlit` on an untextured surface whose skin draws unlit
  (`GltfExporter.DrawsUnlit`: an `unshaded` remake shader without the light include). A texture-named
  surface is unchanged, since it already takes its model's lighting flag.
- **Suite `hd-model-cockpit1`, extended** (`CheckInteriorRoundTrip`): exports the built airframe
  with `CockpitPathBeside`, checks the airframe file holds no `cockpit1` node and `cockpit1.glb`'s
  root is `cockpit1` at identity, packs it unedited, and compares every meshed interior node:
  replaced 67 of 67 on the Bloodhawk, each with the original's vertex count, bounds in the node's
  frame, texture names and unlit surfaces (`g784` under both `altimeter` and `speedometer` resolves
  by path). Without the unlit fix the suite fails on `pfhorizon` alone.
- **Shots** (`.scratch\c21-shots\`): a CLI export (`--viewer --plane=player_bhawk --export-gltf=`)
  packed as `HDModels/roundtrip/planes/player_bhawk/{player_bhawk,cockpit1}.glb` logs `67 cockpit
  interior mesh instance(s)`, and its `--fly --stage=empty --view=cockpit` shot differs from the
  pack-free one in 0.03 % of pixels (275 px over 8 levels), all in the frame's bottom ten rows, where
  a pale rim surface draws over the padding. The likely cause, one draw priority per texture on a
  replacement surface, is in `SceneBuilder` and untraced.
- **Not covered.** The aircraft export path is the viewer's parked plane (`--viewer`), which builds
  the flat interior. A stereo build's interior would carry the head-plate cut and the reticle
  overlays, and in flight the session's subject is the world root, whose interiors sit too deep to
  be found, so neither reaches `cockpit1.glb`.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** `--export-gltf=<path>` and F10 write `cockpit1.glb` beside the airframe file, in interior
units with gamez node names and texture-named materials; put unedited into
`planes/<model>/cockpit1.glb`, it draws as the original.

**Evidence (confidence: traced).** The exported Bloodhawk round-trip file
(`.scratchKeep\hd-roundtrip\roundtrip\planes\player_bhawk\player_bhawk.glb`) holds 146 names and no
interior node (measured this session: only `bld_gencockpit` materials and `cockpit_camera`). The
exporter is `Tooling/GltfExporter.cs`; the viewer builds with `cockpitInterior: spec.Viewer`
(`Launch/InspectionLabs.cs:229`).

**Approach.** <TODO: where the export drops the interior today, and how the second file gets the
interior's own frame rather than the mounted one.>

**Model recommendation.** <TODO>

**Verify.** <TODO: export, drop unedited into a pack, compare cockpit-view screenshots with and
without the pack.>

**⚠ Traps.** The matched glTF node's own transform is ignored and each mesh is in its gamez node's
frame (`docs/formats/model-packs.md:56`); an export that bakes the 0.04 mount into `cockpit1`
round-trips at the wrong size.

## C22 ☑ [local] Gauge table in interior units, and replaced dials that keep driving

**Landed.** A build whose rig section has no `"from": "body"` and names gauges places them inside
`cockpit1`: `PlaneBuilder.MountCockpitInterior` calls `CockpitTransplant.PoseInside` after the body
branch, on the flat and the stereo path alike, since the pose is in `cockpit1`'s own frame whatever
the mount. A body section under the stereo path still keeps `cockpit1` untouched (B11).

- **Posing.** Each named assembly stays the built node under `cockpit1`'s `gauges`, so the interior's
  own `CockpitGauges.Bind`, visibility and reseat are unchanged. `CockpitTransplant.PosedUnder(parent,
  original, pose)` is `Posed` in interior units, put under the parent's interior-frame transform; an
  absent `rotation` or `scale` keeps the assembly's own. An unnamed assembly is not touched. `FlatMount`,
  `InteriorToPlane`, `InteriorFromPlane` and `GlassFrame` keep their signatures.
- **The glass.** Cut loose with B12's `CutGlass` into a `glass` node of the interior at
  `Posed(GlassFrame, pose)`, now with `strip`: each source instance holding a glass surface takes a
  new `ArrayMesh` of its other surfaces, since the builder's cached mesh is shared. Not
  `SceneBuilder.OmittedPolygons`: that drops gamez polygons by identity at build time, so it would
  leave nothing to cut and would miss a texture-named `cockpit6`/`crosshair` replacement surface,
  which the material-id cut reaches. `GunsightReticle.Find` reads the gamez tree and the split glass
  texture is still built, so the stereo overlay still builds; it stays at the original glass's place.
  **For D32:** a moved glass under the headset needs its overlay re-hung in the glass's frame.
- **Replaced dials.** A2's finding holds: a texture-named replacement surface wears a material in
  `InteriorMaterials`, so belts, zones and readouts bind. The readout rule (one surface per character,
  left to right by mean x) is in model-packs.md's new "Gauge parts" row.
- **C21's residual, traced and fixed.** The Bloodhawk's `pf_canopy2` and `g366` wear `sparbead1`
  (and `g366` `sparbead`) at priorities 0 and 5, and `inside` wears `cockpit10` at 0 and 5
  (`extracted/planes`, scan in `.scratch/c22-prio/Scan.ps1`). The original builds each as two
  surfaces and the export writes both, but `MappedReplacement` gave every surface of a texture the
  highest priority, so the rim's priority-0 half drew at 5 over the padding. Now a file holding
  exactly as many surfaces of a texture as the original builds (`SceneBuilder.OriginalSurfacesByTexture`,
  the `BuildMesh` group key) gives each, in file order, the original surface's material and priority;
  any other count keeps the highest, so the decal rule stands. The unedited round trip's cockpit shot
  went from 296 differing pixels (275 over 8 levels, bottom rows) to 9, none over 8 levels. The
  rest is untextured `pfhorizon`, exported under a writer name and drawn at priority 0 against the
  original's -5, which the format documents.
- **Tests.** `CockpitTransplantTests.AnInteriorPoseLandsInCockpit1sFrameUnderAMovedParent`. Suite
  `plane-rig-cockpit-inside` (`Testing/PlaneRigSuites.cs`, install only): the speedometer moved,
  turned and scaled in interior units stands at `Posed`'s pose under `cockpit1`'s `gauges`, an unnamed
  assembly stays, the bind equals the stock one, the `speed` needle reads `GaugeCluster.SpeedAngleDeg`,
  and the moved glass draws once (glass surfaces and total interior surfaces equal the stock count).
  Its second case exports the stock `gungauge` subtree with `GltfExporter.Export`, packs it as
  `planes/<model>/gungauge.glb`, moves it with the table, checks every meshed node below it is drawn
  from the file, its readouts and cell surfaces equal the stock's, and runs `DrivenBelts` (a spent
  belt light turns red). `hd-model-cockpit1`'s round trip now also compares each textured surface's
  depth bias, and fails on the three nodes above without the fix.
- **Shots** (`.scratch/c22-shots/`): `cockpit-original.png`, `cockpit-roundtrip.png` and
  `cockpit-diff-x8.png` (the fixed round trip), and `cockpit-dials-swapped.png`, a rig-only pack
  swapping the speedometer and altimeter positions.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id).

**Original approach (kept for reference).**

**Goal.** A replaced `cockpit1` can move, turn, scale or drop its assemblies through the same
`gauges` table, in interior units, and a replaced dial face keeps its belts, damage zones and
readouts working.

**Evidence (confidence: lead-only).** A texture-named replacement surface "wears exactly the
material an original surface of that texture wears" (`docs/formats/model-packs.md:69-80`), and the
interior's textured materials are registered at `Mech3/SceneBuilder.cs:2151`; whether a
replacement surface lands in that list is unchecked. Readouts need one surface per character
(`Flight/Hud/CockpitGauges.cs:421-453`).

**Approach.** Apply the table's poses inside `cockpit1` with A1's validator. <TODO: confirm the
texture-named path reaches `TexturedMaterials`; state the one-surface-per-cell rule in the doc.>

**Model recommendation.** <TODO>

**Verify.** <TODO: a suite that replaces `gungauge` with a texture-named copy and reads a belt
retexture; a moved dial in a screenshot.>

**⚠ Traps.** The gauge sub-assemblies carry their own `markers` children (`CockpitVisibility.cs:143`);
a rig `markers` entry must not reach into `cockpit1`.

# Wave D, under the headset

## D31 ◐ [local] Body cockpit under the headset

**Landed.** Under the stereo path a rig with `"from": "body"` draws the body cockpit, its hide list,
its transplanted dials and a named glass with the collimated reticle, all from the XR origin's frame.
`HumanFlightAdapter` no longer gates the section on `xrInterior`. The user's look in the headset is
owed, batched with B14.

- **What is lifted.** The stereo build's `CockpitPanel` is what the stereo interior follows:
  `XrCockpit.Body(panel, eyeOffset, cockpit_camera)` lifts it top level into the XR origin's frame the
  way the stock interior is lifted, with the panel's mount at identity, one metre per metre. It holds
  the transplanted `gauges` and `glass` (built through the stereo interior shader on the interior
  builder), the glass's reticle overlay, and a twin of every drawn body mesh. The twins come from a
  second airframe `SceneBuilder` keyed `XrPreciseInterior` (`PlaneBuilder.StereoBody`): the airframe
  subtree is built again, each `mesh` instance of a gamez node is paired by node index and child order
  with its original, and the copy's tree is freed. Each twin shares the copy's `Mesh`, takes the
  original's `node_bias`, casts no shadow (the original casts the real one) and is a direct child of
  the panel. `XrCockpit.Mirror` sets each twin's pose (the original's local transforms up to the
  model root, `InInterior` through the identity mount) and its visibility from `IsVisibleInTree` on
  every `Follow`, so control surfaces, props, torn panels and the hook move in the twins. A mesh
  under an override material (the wingtip flares, whose facade shader cannot be lifted), the
  point-sprite `lights`, a twin whose shader is not the stereo variant, the hide list's subtrees and
  every `cockpit*` subtree get no twin. Pylon stores hang through the existing `Hang` at their true
  pylon poses (the body's `XrCockpit` has zero store offset and spread 1).
- **Doubles.** Each twinned original goes to `CockpitVisibility.Reseat`, whose list now leaves the
  pilot's pane while the interior *or* the transplanted panel is drawn (`Reseated: Interior ||
  Transplant`). The pilot's pane draws the twins (the panel's `WorldFlareLayer` stamp) and none of
  their originals; a second pane, the spyglass disc, the photograph and every outside view draw the
  originals as before. An external pose lowers the panel back onto the airframe at identity and gives
  the originals back. The flat body cockpit binds no reseated parts, so its rules are unchanged.
- **Scale and eye.** The panel's dials use the flat mount (`FlatMount(cockpit_camera, InteriorScale)`)
  on both paths, so one rig puts each dial at one plane-frame pose and size flat and in the headset.
  The interior builder's depth bias is set to `1 / InteriorScale` for a body cockpit, so the
  per-plane `interiorScaleFactor` reaches neither the dials nor their bias; it scales only the hidden
  `cockpit1` rest. `SetMountScale` and `PlaceStores` do nothing on a body `XrCockpit`; the tuning
  lab's eye offset still applies. `EyeInAircraft` is `cockpit_camera` plus the seated-eye offset.
- **The tilt.** The view under the headset takes the aircraft's attitude untilted, as the stock stereo
  interior does: the tracked head supplies the pitch, and the 4.7° head-pitch tilt exists to put the
  flat camera's authored sight near the guns. The flat mount's tilt reaches a body cockpit only as the
  default facing of a dial whose pose gives no `rotation` (B13's template always gives one), so it
  never enters the view.
- **Head plate.** Not cut for a body cockpit (`OmittedPolygons` null); `cockpit1`'s shell never draws.
- **Glass and reticle.** `CockpitTransplant.Build` now returns `glassCarried`, how the glass moved
  from `cockpit1`'s frame into the plane frame (`Carried`): from the `glass` key's pose, or from the
  move of the assembly that holds the glass (the Brigand's `g730`, the Firebrand's `g184`). The
  overlays are built from the glass corners carried through it, in the panel's plane frame, read from
  `cockpit_camera` (`authored_eye`) with a margin of `EyeReachM` metres, and take the glass's own
  depth bias. They hang on the panel, never on `cockpit1`.
- **Limits.** The twins' opaque surfaces write the interior's stencil mark, so the whiteout quad leaves
  the body clear as it leaves the stock interior clear, and a store's stereo copy (drawn under the
  interior) hides where it overlaps the body even when it is in front. The wingtip flares and point
  sprites stay at world coordinates in the pilot's pane, as they do in the stock stereo cockpit. A
  material-level animation on an original (none of the airframe animators makes one) would not reach
  its twin. `XrCockpit.DeclareEyeGlobals` declares the eye feed's three globals once without making
  the process stereo; `DeclareEyeFeed` calls it.
- **Tests.** `CockpitTransplantTests.AGlassCornerIsCarriedToWhereTheMovedGlassDrawsIt`,
  `XrCockpitTests.ARealScaleTwinLiftsToItsOwnOffsetFromTheEye`. Suite `plane-rig-cockpit-stereo`
  (`Testing/PlaneRigSuites.cs`, install only, windowed through `analysis/headless-limits.json`
  `headlessDiffers`, since the reticle needs the glass texture's pixels): on the Bloodhawk with a body
  section hiding a body leaf and naming a moved, turned, scaled speedometer and the glass, the panel
  carries a stereo-shaded twin of each of the 32 drawn body meshes and none for the hide node, no head
  plate is cut, one reticle overlay hangs on the panel reading `cockpit_camera`; lifted at a rotated
  pose 20 km from the origin each twin stands at its original's offset from the eye within 0.48 µm,
  none past 6.1 m, the speedometer and glass at the rig's plane-frame poses; a turned original turns
  its twin and a hidden one hides it on the next `Follow`; the pilot's pane draws the twins and none of
  their originals or the hide node, a second pane draws all the originals; the scale and store fields
  leave the mount alone; an external pose lowers the panel.
- **Registration and jitter** (SHOT-45 and SHOT-48; least-squares sub-pixel shift with gain and
  offset fitted, over seven cockpit regions clear of the pipper, `.scratch/d31-reg/reg.py`). The
  Corvette pack (`.scratch/d31-pack`, the real pack with its body section) on `--stage=empty`,
  `--view=cockpit --no-cockpit-pass`, heading `-0.743,0,-0.669`, the left eye on the flat camera with
  `--xr-sim=eye=1280x720,fov=96.4268,ipd=20,head=0.01/0/0,shot=left,pitch=-4.70001`. At the origin
  the stereo left eye registers within 0.078 px of the flat frame in every region. Between the stereo
  frames at 0 and 20 km the wheel, mirror, glovebox, door and pillar regions agree within 0.09 px (the
  dial regions differ by their needles' readings). Over 24 consecutive frames at 20 km the stereo
  cockpit's worst frame-to-frame step is 0.006 px (0.032 px at the origin, the moving needles); the
  flat body cockpit, drawn at world coordinates, steps up to 2.43 px there (mean 0.26 to 0.88 px per
  region) against 0.064 px at the origin. So the flat 20 km frame is no registration reference.
- **For B14.** The flat body cockpit jitters by up to 2.4 px at 20 km from the origin, since its body
  and dials are drawn at world coordinates; this item does not change the flat path.
- **For D32.** `AddGunsightReticles(parent, root, toParent, eye, metresPerUnit)` now builds overlays in
  any frame, so a moved glass inside a replaced `cockpit1` can take its overlay through `Carried`
  under the interior (`toParent` the glass's move in interior units, `eye` `InteriorEye`). The stereo
  `cockpit1` path is otherwise unchanged; the body branch skips it whole.
- **Shots** (`.scratch/d31-shots/`): `flat-0km.png` against `xr-left-0km.png` (flat and stereo left
  eye from one eye point), `flat-20km.png`, `xr-left-20km.png`. Jitter series in `.scratch/d31-jitter/`.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id). The user's look at the controls is owed, so the item stays open.

**Original approach (kept for reference).**

**Goal.** A body cockpit, its transplanted gauges and its glass draw steadily under the headset,
with the eye at `cockpit_camera` and the collimated reticle on a named glass.

**Evidence (confidence: traced for the existing path, lead-only for the body).** The stereo interior
is lifted to the XR origin so it draws at small coordinates (`Flight/Hud/XrCockpit.cs:8-27`), and
`Hang` hangs an outside part built through the stereo interior's shader under the interior
(`:195-200`), placed by the store offset and spread. A body drawn in the world at real coordinates
can jitter at the head far from the chapter origin (decision 7's premise).

**Approach.** Build the body and its gauges through the stereo interior shader and hang them like
the stores, without the store offset or spread. <TODO: the per-plane `VrCockpitTuning` scale, which
a real-scale body should not take; the head-plate cut, which does not apply.>

**Model recommendation.** <TODO>

**Verify.** The user's look in the headset, batched with B14. <TODO: an instrument for jitter, per
the axis-aligned-pin warning in `docs/verification.md`.>

**⚠ Traps.** <TODO>

## D32 ◐ [local] Replaced `cockpit1` under the headset

**Landed.** A replaced or rig-arranged `cockpit1` under the stereo build draws as the stock one: the
head plate is left out of a replacement, and the collimated reticle sits on the glass wherever the
build drew it. The user's look in the headset is owed, batched with B14 and D31.

- **Where the plate is.** On every player airframe the plate's 5 or 7 polygons share one node with
  canopy structure: `structure` itself (Autogyro, Avenger) or a node under it (`g366` on the
  Bloodhawk, `g715` Pfighter, `canopy2` Fury, Balmoral and Kestrel, `g150` Warhawk, `g803` Brigand and
  Peacemaker, `g347` Firebrand). The C21 export writes it into that node's mesh.
- **The rule: cut by shape.** `CockpitPlate.IsPlateShape` is `IsPlate` without the priority and gamez
  material checks: every corner on one z plane 0 to 8 interior units aft of the `cockpit1` origin, face
  normal aft by the corners' order. `CockpitPlate.Kept` drops the triangles of one surface that meet it.
  `PlaneBuilder.CutReplacedPlate` runs it, under the stereo path outside a body cockpit, on every
  instance drawn from the pack (`GamezMeshMeta`) and only on surfaces whose material is not texture-named
  (not in `InteriorMaterials`); the instance takes a mesh of its own, since the cached replacement is
  shared. Counted in `ReplacedPlateTriangles`. Chosen over the alternatives because it needs nothing
  of the export or the author: a pack convention (a glTF node or material name) would need the export
  to split the plate out of a node whose surfaces it shares by material, and "keep the plate on its own
  node" cannot hold, since the plate's node is canopy structure an author replaces. The untextured
  condition is needed: over the eleven interiors, triangulated as the build draws them, the shape alone
  takes exactly the plate's untextured triangles, but textured triangles of that shape stand on the
  Autogyro (2), Brigand (3) and Kestrel (7). An authored plate is cut when it stays flat, aft-facing and
  untextured; the rule is in model-packs.md's "Under a headset" row.
- **The reticle follows the glass.** `GunsightReticle.Drawn(interior, materials, gamez glasses)` reads the
  glass off the built meshes: each surface whose active material is a glass texture's (from
  `InteriorMaterials`) is one glass, its vertices carried into the interior's frame
  (`PlaneRigPatch.PlaneFrame`), its UVs from the surface, its priority the gamez glass's of that
  texture. `MountCockpitInterior` now adds the overlays after `CockpitTransplant.PoseInside`, from
  `Drawn`, so a glass the C22 table moved, one riding a moved assembly (the Brigand's `g730`), and a
  pack's texture-named `cockpit6`/`crosshair` surface at a new place each carry it. A glass under any
  other name has no reticle. `Overlay`'s pane takes its first edge to the first corner off the first,
  since a drawn surface repeats corners. The body cockpit keeps D31's `Carried` path;
  `AddGunsightReticles` now takes the glasses in the parent's frame.
- **Tuning.** `XrCockpit` reads only the mount and the eye, so the per-plane `interiorScaleFactor` and
  seated-eye offset reach a replaced `cockpit1` unchanged, and the C22 interior-unit poses and the
  overlay (in interior units, its margin `EyeReachM / _interiorScale`) scale with the mount.
- **Tests.** `CockpitPlateTests.TheShapeCutDropsExactlyThePlatesUntexturedTriangles` (all eleven
  airframes) and `TheShapeCutKeepsEveryTriangleButTheAftFacingOnes`. Suite `hd-model-cockpit1-stereo`
  (`Testing/HdModelSwapSuites.cs`, install only, windowed through `headless-limits.json`
  `headlessDiffers`): stock omits the 7 plate polygons; the unedited round trip cuts the plate's 25
  triangles, draws stock's 1151, and its one overlay equals stock's (plane, UV gradients, depth bias,
  eye); a rig moving `glass` by (1.5, -0.75, 0.5) units and a `dashboard.glb` holding the glass surface
  moved by (-1, 0.5, -0.25) each put the overlay in the moved glass's plane with stock's centre texel;
  the round trip plus the table at a 1.25 scale factor mounts at `StereoMount(marker, 0.05)`, cuts the
  same plate, keeps the glass's interior-unit pose and the overlay, and a seated-eye offset gives the
  stock `EyeInAircraft`. With the cut and `Drawn` switched off, the suite fails on six checks.
- **Shots** (`.scratch/d32-shots/`, SHOT-45's `--xr-sim=eye=1280x720,fov=96.4268,ipd=20,head=0.01/0/0,
  shot=left,pitch=-4.70001`, `--fly --stage=empty --plane=player_bhawk --view=cockpit --no-cockpit-pass`):
  `xr-left-stock.png` and `xr-left-roundtrip.png` differ in 3 pixels by 1 level, and register at 0.000 px
  in seven regions (struts, compass, three dials, the reticle; `.scratch/d32-reg/reg.py`).
  `xr-left-roundtrip-glass-moved.png` has the table move the glass 3 units right and 1.5 down, with the
  reticle on it. `flat-stock.png` is the flat frame: it sits 6 to 11 px off both stereo frames in the
  dial and strut regions, the same for stock and the round trip, since the stereo eye is the
  `cockpit_camera` marker in interior units and the flat eye the interior's origin.
- **Not covered.** The flight log line `head plate left out (N polygons)` counts the gamez polygons
  only (it is written by `HumanFlightAdapter`, outside this item); the cut triangles are in
  `ReplacedPlateTriangles`. A pack glass that is not flat, or whose UVs are not affine over it, draws a
  wrong reticle.

**Verified.** The complete `RunTests.ps1` passed on the merged plan tree carrying every item: 6981 units, 614 engine suites with engine errors clean, and 38 goldens hash-identical. The item's own suites and units are named in its landing commit (`git log --grep=` the item id). The user's look at the controls is owed, so the item stays open.

**Original approach (kept for reference).**

**Goal.** A replaced `cockpit1` draws under the headset as the original does: no head plate behind
the eye, a collimated reticle on its glass, the per-plane tuning applied.

**Evidence (confidence: traced).** The head-plate cut omits original polygons by index
(`Mech3/CockpitPlate.cs`, `Mech3/PlaneBuilder.cs:481-483`), so it does nothing to a replacement
mesh. The reticle overlay is built from the glass texture `cockpit6`/`crosshair`
(`Mech3/GunsightReticle.cs:49-58`, `Mech3/PlaneBuilder.cs:530-537`).

**Approach.** <TODO: how a replacement marks or omits its head plate, and how its glass is found
when the glass surface is replaced.>

**Model recommendation.** <TODO>

**Verify.** The user's look in the headset. <TODO>

**⚠ Traps.** <TODO>
