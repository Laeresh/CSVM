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

1. ☐ [cloud] `PlaneRig` `cockpit` section: reader and validation
2. ☐ [cloud] The interior builder gets the airframe's pack scope

### Wave B, body cockpits on the flat screen

11. ☐ [local] Body-mode visibility: the body stays, `cockpit1` goes, the hide list applies
12. ☐ [local] Transplanted gauge assemblies and sight glass on the body
13. ☐ [cloud] code, [local] check: `--dump-markers` writes the `cockpit` template
14. ☐ [local] The Corvette's seat and dashboard

### Wave C, cockpit1 replacements

21. ☐ [cloud] code, [local] check: the export writes `cockpit1.glb`; unedited round trip
22. ☐ [local] Gauge table in interior units, and replaced dials that keep driving

### Wave D, under the headset

31. ☐ [local] Body cockpit under the headset
32. ☐ [local] Replaced `cockpit1` under the headset

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

## A1 ☐ [cloud] `PlaneRig` `cockpit` section: reader and validation

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

## A2 ☐ [cloud] The interior builder gets the airframe's pack scope

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

## B11 ☐ [local] Body-mode visibility: the body stays, `cockpit1` goes, the hide list applies

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

## B12 ☐ [local] Transplanted gauge assemblies and sight glass on the body

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

## B13 ☐ [cloud] code, [local] check: `--dump-markers` writes the `cockpit` template

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

## B14 ☐ [local] The Corvette's seat and dashboard

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

# Wave C, cockpit1 replacements

## C21 ☐ [cloud] code, [local] check: the export writes `cockpit1.glb`; unedited round trip

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

## C22 ☐ [local] Gauge table in interior units, and replaced dials that keep driving

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

## D31 ☐ [local] Body cockpit under the headset

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

## D32 ☐ [local] Replaced `cockpit1` under the headset

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
