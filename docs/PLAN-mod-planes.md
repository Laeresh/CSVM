# Mod planes, new airframes as mods (first mod: the Avenger)

**ACTIVE PLAN** (written 2026-11-22). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. When every item lands, the closing
commit deletes this file, records the completion in its message, and clears the "Current status"
pointer; any live prose linking this file by path is unlinked in the same commit.

CSVM flies exactly the eleven stock airframes of `planes.zbd`, and that set is hard-wired: an
id-0..10 table (`CSVM/src/Flight/Hangar/StockAirframes.cs:17-22`) that the hangar, the campaign,
bots, lobbies and the net wire share, plus `CustomPlaneDef.MaxAirframe = 10`
(`CSVM/src/Flight/Hangar/CustomPlaneDef.cs:32`) and several more copies of the list. An HD model
pack (`docs/formats/model-packs.md`) only changes how an existing airframe looks; its colliders,
hit bodies, firepoints and handling stay the donor's. This plan adds a mod mechanism for genuinely
new airframes: a mod ships a model and a small def that inherits a stock plane's stats, and the
plane then appears in every picker, the campaign, bot seats, saves and multiplayer.

The first mod is the E-1C Avenger, from a third-party SketchUp model (`tmp/E1C avenger.obj`) with
reference images of the board-game miniature and box art (`tmp/avenger/`). It is a Crimson Skies
universe design of unknown model licence, so **it stays a local, gitignored mod and never enters a
commit or a release**; the repo ships the mod format, its loader and its documentation. The model
is prepared in Blender first and previewed in game as a local HD pack over the Brigand, which
works today, before the engine work starts. No `backlog` issue feeds this plan; it comes from a
grilling session, so nothing needed re-verifying against the tracker.

## Milestone goal

- A plane mod in `<dataRoot>/Mods/Planes/<id>/` (folder or zip) loads as a new airframe with its
  own model, colliders, control surfaces, props, markers, cockpit, paint, damage and menu art.
- Its stats are a stock parent's def (`kind_of`) with literal overrides.
- It can be picked everywhere a stock plane can, including the campaign hangar (with an unlock
  rule), bot seats that choose a plane, custom-plane builds and multiplayer.
- Saves keep a mod plane's id; a missing mod flies as its parent and nothing is lost.
- The Avenger flies as the first mod: Brigand physics, the Fury's `fd_speed`, the Warhawk's
  cockpit, no turret, its own canopy, pilot and baked 2048 px texture.

**Mods add airframes; they never change stock ones or the original's content.** Campaign mission
enemies stay as scripted, gear and canopy stay unanimated, and the Avenger itself is never
distributed, because the plan's deliverable is the mechanism, not that plane.

## Decisions (2026-11-22)

| # | Question | Decision |
|---|---|---|
| 1 | Reskin through an HD pack, or a real new airframe? | **A mod mechanism for new airframes**, not a one-off; the HD pack cannot carry stats, colliders or a new id |
| 2 | Where do a mod plane's stats come from? | **Stock parent (`kind_of`) plus overrides**, reusing `PlaneStats`' def-chain resolution |
| 3 | How are overrides written? | **Literal values only.** Borrowing another stock plane's value means copying the number; no `{"from": …}` resolver |
| 4 | How does the model name its parts? | **The original's node names** (`l_aileron1`, `r_elevator`, `l_rudder1`, `propN`, `firepointN`, `pylonN`, `cockpit_camera`, `head`, `cockpit1`, `pdpanelN`, `destroyed`), the HD-pack convention; a stock export is the template |
| 5 | Multiplayer | **Every peer needs the same mods**, matched by id + content hash; the wire sends a string id instead of the 0-10 index |
| 6 | Where can a mod plane be picked? | **Everywhere, including the campaign hangar** |
| 7 | When does it unlock in the campaign? | **An optional `unlock` field in plane.json**: `start`, `with_parent` (default) or a mission/chapter id |
| 8 | A save names a missing mod | **Keep the record, fly the parent** (stored beside the id) with a one-line notice |
| 9 | Hangar builder | **Customisable, inherited from the parent**: engine rows, weapon limits, armor caps, default loadout; slot count from the model's markers |
| 10 | Paint | **Own base texture + one tint mask** (primary / secondary / trim) that the pattern's or militia's swatch colours tint; decals on `*_noselogo` / `*_taillogo` / `*_winglogo` surfaces |
| 11 | Cockpit | **Own `cockpit1`, else a named stock plane's interior, else the parent's**, placed at the mod's `cockpit_camera` |
| 12 | Damage | **Optional `pdpanelN` and `destroyed` nodes; without them an auto-wreck** cuts the mesh into the four collider regions, with smoke and fire at the region centres |
| 13 | Animations | **Control surfaces and props only**, as stock planes; no gear, canopy or keyframe clips |
| 14 | Packaging | **`<dataRoot>/Mods/Planes/<id>/`, folder or zip**; the id is the folder name |
| 15 | Menu art | **Mod image, else a headless auto-render** made once at load and cached |
| 16 | Engine sound | **The parent's, overridable by naming another stock plane** |
| 17 | Bots | **Seats that choose or randomise a plane may use mods**, with the parent's AI def; campaign mission enemies stay scripted |
| 18 | Avenger distribution | **Local mod, gitignored**; the repo may carry a self-made sample for tests only |
| 19 | Avenger stats | **Brigand physics, the Fury's `fd_speed` (literal), the Warhawk's cockpit**, from the model's author |
| 20 | The Brigand's inherited rear turret | **Cleared with `"turrets": []`**; the Avenger is a forward-gun fighter |
| 21 | Avenger hardpoints | **The Brigand's slot count**, placed on visible spots; guns may sit in pairs (inner/outer wing, lower/upper fuselage) |
| 22 | Gear and modelled stores | **Gear deleted; modelled rockets deleted and replaced by `pylonN` empties**, pylon rails and nacelle pods kept |
| 23 | Who prepares the model | **Claude through Blender MCP, scripted**; the user judges a render after each stage |
| 24 | Texture method | **Procedural in Blender, baked**, colours from the references (deep blue, yellow trim, red-orange spinners); the wing emblem stays out (IP) |
| 25 | Texture resolution | **2048 px**, HD-pack level |
| 26 | Canopy | **See-through glass (glTF `BLEND`) plus a painted frame** in the trim region; static |
| 27 | Pilot | **A new low-poly helmeted head and shoulders named `head`**, as the originals carry it in the plane model; no game data |
| 28 | Order | **Model first, previewed as a Brigand HD pack**, then the engine work with the Avenger as its first mod |

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

### Wave A, the Avenger model (Blender, previewed as a Brigand pack)

1. ☑ Export the Brigand and Warhawk as templates and read their node trees
2. ☐ Clean and orient the model: stand, gear, modelled rockets, frame
3. ☐ Cut and hinge the control surfaces, split the props, apply stock names
4. ☐ Model the canopy (frame + glass) and the pilot `head`
5. ☐ Place firepoints, pylons and `cockpit_camera`
6. ☐ UV unwrap and bake the procedural 2048 px texture and tint mask
7. ☐ Preview in game as a local HD pack over the Brigand

### Wave B, mod loading and airframe identity

11. ☑ Mod discovery and the `plane.json` format
12. ☐ One airframe registry in place of the fixed eleven-plane lists
13. ☐ Mod stats: synthetic def from `kind_of` plus literal overrides
14. ☐ Build a mod plane from its GLB

### Wave C, the plane's surfaces

21. ☐ Cockpit: own, named stock, or parent's
22. ☐ Paint: base texture, tint mask and decals
23. ☐ Damage: optional nodes and the auto-wreck
24. ☐ Engine sound: parent's or a named stock plane's
25. ☐ Hangar builder for mod airframes
26. ☐ Menu art: mod image or cached auto-render

### Wave D, saves, campaign, bots, multiplayer

31. ☐ Saves keep the mod id; a missing mod flies as its parent
32. ☐ Campaign unlock field
33. ☐ Bot seats may fly mod planes
34. ☐ Multiplayer: string airframe id and a matching mod set

### Wave E, the Avenger as the first mod

41. ☐ Package the Avenger as a local mod and fly it through every surface

## Dependency and parallelism notes

Wave A needs nothing from the engine and can run now; A1 feeds A3 to A5 (names, slot count,
template frame), and A2 → A3 → A4 → A5 → A6 → A7 is a chain on one Blender scene, so it runs
serially. B11 and B12 block the rest of the engine work; B13 and B14 both need B11, and B14 needs
B13 for the def it builds against. Wave C items need B14 and are independent of each other except
C22 and C26 (the auto-render wants the paint). D31 to D34 need B12; D34 also needs B11's content
hash. E41 needs every earlier item. File contention: B12 touches `StockAirframes.cs`,
`CustomPlaneDef.cs`, `MarkerRig.cs`, `InstantAction.cs`, `EffectCatalogue.cs` and the net seat
code, which D31 and D34 also edit, so run those serially; C25 and D31 both touch the hangar's
custom-plane code.

---

# Wave A, the Avenger model (Blender, previewed as a Brigand pack)

## A1 ☑ Export the Brigand and Warhawk as templates and read their node trees

**Landed.** Both planes export with `RunProbe.ps1 --viewer --plane=player_brigand
--export-gltf=Z:\CSVM\.scratchKeep\mod-planes\templates\brigand\brigand.gltf` (and the same for
`player_warhawk` into `templates\warhawk\`), after a `dotnet build` of the worktree;
`--plane=` chooses the viewer plane, and each run also writes `cockpit1.gltf` beside the airframe.
The templates live in `.scratchKeep/mod-planes/templates/`, untracked game data.

- **Frame.** Metres, Y up, nose toward -Z, verified from the bounds: the Brigand's `nosecone`
  reaches z -2.73 and its tail z +4.19, `ground_level` sits at y -1.33 and the pilot's head tops
  out at y +0.96.
- **Brigand size.** Wing span 11.55 m (wing meshes to x ±5.78, wingtip `pdp2_h`/`pdp3_h` to
  ±5.95), length 6.93 m, height 2.18 m without the prop disc (3.37 m diameter, so 3.37 m with it).
- **Brigand tree, viewer build.** `player_brigand > geometry > {healthy, dontmove, markers}`.
  `healthy > nearest > {nose, tail, leftwing, rightwing, pilot_pos, player_damage_on}` and
  `healthy > l13 > brigand_turret1 > brigturret > {g12436, hgun > {hfirepoint, g935, hfirepoint1}}`.
  `dontmove > {staticprop1, nosecone}`. The export drops what the static viewer skips: `shadow`,
  the `l12`/`l14` LOD groups, `brig_hook` (inactive), `destroyed > piece1..4`, `pdp1..pdp8`, and
  `prop1`, `prop1b`, `nitroprop1`, `wing_flare1/2` (all present in planes.zbd's `nodes.json`).
- **Control surfaces.** Hinge group, then surface: `lailer1 > l_aileron1`, `railer1 >
  r_aileron1` (under `leftwing`/`rightwing`), `lft_elev > l_elevator1`, `rt_elev >
  r_elevator1`, `lrudder1 > l_rudder1`, `rrudder1 > r_rudder1` (under `tail`, twin rudders at the
  boom ends, x ±1.70). Each surface's origin is its hinge group's origin; the groups carry the
  tilt (elevators rolled ±6.2° about Z, ailerons about 11° about Z plus a few degrees of X/Y).
- **Props.** One nose prop: `staticprop1` at (0, 0, -2.10), with `prop1`, `prop1b`,
  `nitroprop1` in the full tree; `nosecone` is a separate mesh. No `propN` reaches the viewer
  export.
- **Pilot.** `pilot_pos > pilot > body > head`; `body` is the torso at (0, 0.33, 0.93), `head`
  the helmeted head at (0, 0.63, 0.88). The Warhawk names it differently: its `head` is the torso
  and the head is its child `g1149`.
- **Markers.** `cockpit_camera` (0, 0.83, 0.72); `firepoint1..8` at two positions per side,
  each used twice: fp1 ≡ fp4 (+1.40, -0.50, -1.30), fp2 ≡ fp3 (-1.40, -0.50, -1.30),
  fp5 ≡ fp8 (-2.34, -0.32, -1.15), fp6 ≡ fp7 (+2.34, -0.32, -1.15); `pylon1/3/5/7` port at
  x -3.26, -2.76, -2.26, -1.79 and `pylon2/4/6/8` starboard at x +3.20, +2.72, +2.21, +1.74
  (y -0.37 to -0.69, z 1.05 to 1.22); plus `exhaust1/2`, `ground_level`, `target`,
  `cockpit_light`, `map`, `ladder_pos`, `cf_light`.
- **Stock loadout** (`stock_loadouts.json` `pbrigand`): W1 Outer Wing Guns cal 60 on fp7,8; W2
  Outer Wing Guns 2 cal 30 on fp5,6; W3 empty (fp3,4); W4 Rear Turret cal 30 on fp1,2
  (`turret: true`, a nominal binding, the turret fires from `hfirepoint`/`hfirepoint1`);
  hardpoints `count` 4, four `wep_06`, which take `PylonFillOrder`'s prefix: pylons 1, 5, 2, 6.
  This matches the eight firepoints and eight pylons on the model.
- **Warhawk.** Three engines (`staticprop1..3` nose and wing nacelles at x ±8.1), wing span
  18.46 m, length 15.02 m, height 3.75 m without props. Eight distinct firepoints, pairs (1,2)
  outermost x ∓4.4 to (7,8) innermost ∓2.6, odd port; eight pylons, odd port. Its `l_rudder1`
  hangs directly under `leftwing` with no hinge group while `r_rudder1` sits under `rrudder`.
  `cockpit1.gltf` is in its own units, eye at the origin, extent 355 x 74 x 251 units (14.2 x
  3.0 x 10.0 m at `PlaneBuilder.InteriorScale` 0.04), and carries its own `*_aileron2`,
  `*_elevator2`, `*_rudder2` surfaces.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** The Brigand's node names, frame, slot count (`firepointN`, `pylonN`), turret mount nodes
and `head` node are known, and the Warhawk's `cockpit1` export is on disk as the cockpit reference.

**Evidence (confidence: lead-only).** `--export-gltf=<path>` exports the viewer plane and its
interior to `cockpit1.glb`, headless (`docs/cli.md:468`). A stock export carries a `head` node
beside `body`, `nose`, `tail` and `prop1` (the Hoplite export under
`.scratchKeep/corvette/HDModels-real/corvette/planes/autogyro/`); the Brigand's own tree is unread.
The pack frame is metres, Y up, nose toward -Z (`docs/formats/model-packs.md:62`, per the
exploration agent).

**Approach.** Export `player_brigand` and `player_warhawk` through `RunProbe.ps1` with
`--export-gltf=` into `.scratch/`, never `tmp/` or a tracked path. List node names, count markers,
note the turret subtree.

**Model recommendation.** <TODO: tier not settled>

**Verify.** The exported tree's node list is in the item's commit message; slot counts match the
Brigand's stock loadout in `stock_loadouts.json`.

**⚠ Traps.** The exports are game data: they stay in `.scratch/` and never enter a commit. The
Godot guard blocks a direct `Godot_v4*.exe` launch; go through `RunProbe.ps1`.

## A2 ☐ Clean and orient the model: stand, gear, modelled rockets, frame

**Goal.** One clean Avenger in CSVM's frame, without display stand, landing gear or modelled
rockets, with pylon rails and nacelle pods kept.

**Evidence (confidence: traced for the file facts, from this session).** `tmp/E1C avenger.obj` is a
SketchUp export: 7,886 vertices, 16,067 triangles, about 90 unnamed groups (`Mesh_1`…`Mesh_88`,
`Gear_40`/`Gear_67`, `Cone_*`, `Fins_*`, `WingPylon_*`, `Base_*`, `WarbirdMesh_*`); the `.mtl` it
names is missing. Its header says inches, but the bounds (13.5 × 3.0 × 8.8) read as a plausible
span and length in metres, which the Blender import shows. The UVs are SketchUp per-face
projections (30,784 `vt` for 7,886 `v`), unusable as an unwrap.

**Approach.** In the "Avenger" Blender scene: split by loose parts or the OBJ groups, identify the
stand, gear and rockets by position, delete them, rotate into the template frame from A1, apply
transforms. Show the user a four-view render.

**Model recommendation.** <TODO: tier not settled>

**Verify.** User approves the four-view render.

**⚠ Traps.** The `Base_*` groups may be nacelle bases rather than the display stand; identify by
geometry before deleting. Do not trust the OBJ's units comment.

## A3 ☐ Cut and hinge the control surfaces, split the props, apply stock names

**Goal.** Ailerons, elevators and rudder are separate meshes with their origins on the hinge line,
the two props are separate, and every part carries the stock name `ControlSurfaces` matches.

**Evidence (confidence: lead-only).** The model's wings and tail are solid (user's screenshot,
the Blender renders). Surfaces are found by node-name regex, `^[lr]_aileron[0-9]+$`,
`^[lr]_elevator[0-9]*$`, `^[lr]_rudder([0-9]+|_rotate)$`, each under a hinge-parent group, hinge
axis X for ailerons and elevators, Y for rudders (`CSVM/src/Mech3/ControlSurfaces.cs:31-33,79-84`,
per the exploration agent). Props are `propN` / `propNb`, spun by `spin_props_anim`.

**Approach.** Knife the trailing-edge surfaces out of the wing and tail meshes, close the cut
faces, set each surface's origin on its hinge line, parent under a hinge group, name by A1's tree.
Split each spinner and blade set into `prop1`, `prop2`. Render with surfaces deflected.

**Model recommendation.** <TODO: tier not settled>

**Verify.** User approves a render with every surface deflected about its hinge.

**⚠ Traps.** Prop rotation direction for the two engines is not decided; settle it in A7 by
looking. A single rudder needs the name form the regex accepts (`l_rudder1` style).

## A4 ☐ Model the canopy (frame + glass) and the pilot `head`

**Goal.** A static canopy with a painted frame and see-through glass panes, and a helmeted pilot
head and shoulders named `head` visible under it.

**Evidence (confidence: lead-only).** The originals carry the pilot as a `head` node in the plane
model (the Hoplite export); no CSVM code places a pilot. Pack materials support `alphaMode`
`BLEND` and `doubleSided` (`docs/formats/model-packs.md:100-106`). The references show a
multi-frame canopy.

**Approach.** Model frame bars and glass panes over the existing canopy shape; glass as its own
material with `BLEND` and a slight tint. Low-poly head with helmet and goggles, seated at the
cockpit position.

**Model recommendation.** <TODO: tier not settled>

**Verify.** User approves a close render from outside showing the pilot through the glass.

**⚠ Traps.** Blended glass can sort wrongly against clouds and smoke; judge it in A7, not in
Blender.

## A5 ☐ Place firepoints, pylons and `cockpit_camera`

**Goal.** The Brigand's count of `firepointN` and `pylonN` empties sit on the Avenger's visible
barrels and rails, and `cockpit_camera` sits at the pilot's eyes.

**Evidence (confidence: lead-only).** Hardpoints are marker nodes on the plane (`firepointN`,
`pylonN`); default loadouts come from `stock_loadouts.json` (`PlaneStats.cs:103`, per the
exploration agent). The Brigand's turret is a separate `turrets` mount
(`CSVM/src/Flight/Weapons/TurretController.cs:273-300`), so it does not count against the forward
firepoints.

**Approach.** Place empties by A1's names and count, guns in pairs (inner/outer wing, lower/upper
fuselage) where the count is even; render with markers shown.

**Model recommendation.** <TODO: tier not settled>

**Verify.** User approves the marker render; A7 shows rounds leaving the barrels.

**⚠ Traps.** The Brigand carries eight `firepointN` and eight `pylonN`, and the Avenger keeps
all sixteen names. Guns bind by slot, W*n* to `firepoint(9-2n)`,`firepoint(10-2n)`, so each
binding pair (1,2), (3,4), (5,6), (7,8) must be one port and one starboard marker; the Brigand
does not keep a fixed side per number (fp1 starboard, fp3 port), so place each pair by side, not by
odd/even. Its stock W1 and W2 share one outer-wing position and W3 and W4 share the inner one;
the Avenger may separate them (inner/outer, lower/upper). W4 is the Brigand's Rear Turret on
fp1,2, so with `"turrets": []` that slot needs a decision in B13 before fp1,2 are treated as a
forward gun. Pylons are odd to port and even to starboard, numbered outboard to inboard, and a
stock 4-rocket fit lands on pylons 1, 5, 2, 6, so those four must sit on the visible rails.

## A6 ☐ UV unwrap and bake the procedural 2048 px texture and tint mask

**Goal.** A clean UV unwrap, a 2048 px base texture (panel seams, rivets, wear, metal parts,
glass) and a primary/secondary/trim tint mask in the same UVs.

**Evidence (confidence: lead-only).** References in `tmp/avenger/`: deep blue body, yellow
cheatline and trim rings on nacelles, wingtips and tail tip, red-orange spinners, nose radiator
grille, exhaust stacks along the fuselage. Blender's AI generators are off or missing in the
installed addon and only make new models.

**Approach.** Smart-project or seam-based unwrap; shader-node texture (seams from mesh edges,
rivet rows, grime); bake base colour and the mask to PNG; decal placeholder surfaces named
`*_noselogo`, `*_taillogo`, `*_winglogo`.

**Model recommendation.** <TODO: tier not settled>

**Verify.** User approves renders of the baked texture from four sides.

**⚠ Traps.** The wing sunburst emblem and tail number art are IP; leave them out of the bake. The
tint mask has no consumer until C22; in A7 the texture draws as fixed paint.

## A7 ☐ Preview in game as a local HD pack over the Brigand

**Goal.** The Avenger draws in game in place of the Brigand, surfaces deflect, props spin, and the
user judges look, glass and markers at the controls.

**Evidence (confidence: lead-only).** A pack replaces mesh by mesh under the original's node
names, or with a single root file `planes/player_brigand/player_brigand.glb`; `rig.json` overrides
markers, props and surfaces (`docs/formats/model-packs.md:45,50,140-182`, per the exploration
agent). A material not named after an original skin texture draws as fixed paint (`:127`).

**Approach.** Export GLB from Blender into a local pack under `<dataRoot>/HDModels/avenger/`, add
`rig.json` for markers and the cockpit camera, fly a Brigand.

**Model recommendation.** <TODO: tier not settled>

**Verify.** User at the controls: look, glass sorting, surface motion, prop direction, rounds from
the barrels. <TODO: screenshot poses>

**⚠ Traps.** The Brigand's colliders, hit body and shadow stay in force under a pack
(`model-packs.md:248-254`), so contact and hits do not match the Avenger's shape until B14. The
pack lives in the data root, never in the repo.

# Wave B, mod loading and airframe identity

## B11 ☑ Mod discovery and the `plane.json` format

**Landed.** **Verified.** <pending orchestrator run>

**Goal.** Folders and zips in `<dataRoot>/Mods/Planes/` load as mod records (id, display name,
`kind_of`, literal overrides, `unlock`, optional cockpit and sound names, optional portrait) with a
content hash; an invalid mod is skipped with a log line and a menu notice.

**Evidence (confidence: traced-to-code).** HD packs discover subfolders and top-level zips through
`Extraction/PackIndex.cs`, but merge every pack into one key map; a mod is a unit of its own, so the
loader enumerates the same way (subfolders and `.zip`s in name order) without `PackIndex`, as
`Extraction/Subtitles.cs` does.

**Approach.** `CSVM/src/Flight/Hangar/ModPlanes.cs`, beside `StockAirframes.cs`, which it validates
against (an `Extraction` module would have had to name `Flight`). `ModPlanes.Open(dataRoot)` reads
`Mods/Planes`: each subfolder and top-level `.zip` is a `ModPlane` with id = folder or zip base name.
A zip's top is where `plane.json` sits, at its root or under one top folder; two top folders holding
one each skip it. `plane.json` keys: `kind_of` (required, a stock node), `name` (default the id),
`overrides` (def field name to finite number, plus `"turrets": []` as `ClearsTurrets`), `unlock`
(`start`, `with_parent` default, or any other non-empty string as a mission or chapter id),
`cockpit` and `sound` (stock nodes), `portrait` (a path that must exist in the mod); `_`-prefixed
keys are prose, any other key fails. `plane.glb` at the top is required. The id may not be a stock
node or an earlier mod's id (matched without case). The hash is SHA-256 over every file sorted by
lower-case path (path, zero byte, 64-bit length, bytes), dot files excluded, so a folder and a zip of
the same files hash alike. A failing mod is logged (`flight` category) and listed in
`ModPlanes.Skipped`. `ModPlanes.Current` exists but nothing sets or reads it yet; the launch call
belongs to B12 with a `--det` rule so engine suites never read a user's mods. Format page:
`docs/formats/mod-planes.md`.

**Verify.** `CSVM.Tests/ModPlanesTests.cs`, 17 cases over mods built in a temp data root: every
field, the defaults, folder vs flat zip vs nested zip with one hash, the hash's content rules, eleven
invalid `plane.json` forms, the colliding, duplicate and incomplete mods. No suite reads the user's
modding folders.

**Original approach (kept for reference).** <TODO: loader location, schema, hash scope>. Document
the format in a new `docs/formats/` page. Model recommendation: <TODO: tier not settled>. Verify:
<TODO>. Test mods are built in `ctx.ScratchDir`; no suite reads the user's modding folders.

**⚠ Traps.** The id must not collide with a stock node name (`player_avenger` is the Hellhound's,
`StockAirframes.cs:19`).

## B12 ☐ One airframe registry in place of the fixed eleven-plane lists

**Goal.** Every place that enumerates airframes reads one registry: stock ids 0-10 plus mod ids.

**Evidence (confidence: lead-only, sites per the exploration agent).** Copies of the fixed list:
`StockAirframes.cs:17-22`, `MarkerRig.cs:29-42` (`PlayerAirframes`), `Tooling/Probes.cs:56`,
`EffectCatalogue.cs:243`, `InstantAction.cs:65`, `Testing/SuiteConstants.cs:19`,
`CustomPlaneDef.MaxAirframe` (`CustomPlaneDef.cs:32`, traced), `SessionSpec.cs:3149` (coerces an
unknown `--vs-bot` plane to a random stock one).

**Approach.** <TODO: registry shape; whether ids stay ints for stock planes>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## B13 ☐ Mod stats: synthetic def from `kind_of` plus literal overrides

**Goal.** `PlaneStats` loads a mod plane as its parent's def with the mod's literal overrides
applied, including `fd_speed` and `"turrets": []`.

**Evidence (confidence: traced for the fields named).** `PlaneStats.LoadCore`
(`CSVM/src/Flight/Airframe/PlaneStats.cs:490`) resolves `vehicle.json`'s `kind_of` chain; the def
must derive from `player_airplane` (comment at `:530`, per the exploration agent). `fd_speed` is
the reference level-flight speed (`PlaneStats.cs:140,637`) that `FlightModel` scales by throttle
(`FlightModel.cs:628`); `EnginePower` comes from the airframe's engine row (`PlaneStats.cs:145`).
Turret mounts are `stats.TurretMounts` (`TurretController.cs:279`).

**Approach.** <TODO: inject a synthetic def into the chain vs. post-load override>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** The Fury's top speed copied as `fd_speed` still scales by the Brigand's engine rows,
so the Avenger's upgraded top speed is not the Fury's upgraded one. That follows from Decision 3
and is expected.

## B14 ☐ Build a mod plane from its GLB

**Goal.** `PlaneBuilder` builds a mod airframe from its GLB with control surfaces, props, markers
and colliders working as on a stock plane.

**Evidence (confidence: traced for colliders, lead-only otherwise).** `PlaneBuilder.cs:300` finds
the root by `_gamez.FindByName` and throws when missing (per the exploration agent).
`PlaneCollider.Build` derives hulls from the built model's triangles with no per-plane data
(`CSVM/src/Flight/Airframe/PlaneCollider.cs:55-58`), so a mod plane's colliders follow its own
mesh. Pack meshes import through `Mech3/HdModelMeshes.cs`.

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

# Wave C, the plane's surfaces

## C21 ☐ Cockpit: own, named stock, or parent's

**Goal.** A mod plane's cockpit view uses its own `cockpit1`, else the stock plane plane.json
names, else the parent's, placed at the mod's `cockpit_camera`.

**Evidence (confidence: lead-only).** `cockpit1` is the stock interior (about 25 units per metre,
scaled 0.04 at `cockpit_camera`, `model-packs.md:63`); gauges come from `CockpitTransplant.cs`;
`PlaneBuilder.cs:192,324` reads `cockpit_camera` (per the exploration agent).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C22 ☐ Paint: base texture, tint mask and decals

**Goal.** The chosen pattern's or militia's swatch colours tint the mod's mask regions, and decals
land on the decal surfaces, everywhere the airframe is built.

**Evidence (confidence: lead-only).** Liveries reach only UV-compatible surfaces, through
per-pattern region masks in the original skin layout, and a pack cannot carry its own masks
(`docs/formats/model-packs.md:113-128`, traced in this session). The skin prefix is read off
material names (`PlanePainter.PrefixFor`, `Mech3/PlanePainter.cs:94`, per the exploration agent).
A network seat sends its livery as pattern, swatch and decal indices (`model-packs.md:128`).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO: which swatch colour feeds which mask channel>

## C23 ☐ Damage: optional nodes and the auto-wreck

**Goal.** A mod with `pdpanelN` / `destroyed` nodes uses them; one without breaks into its four
collider regions as wreck pieces, with smoke and fire at the region centres.

**Evidence (confidence: lead-only).** Damage zones are `nose`, `tail`, `leftwing`, `rightwing`
(`PlaneStats.cs:36-53`, `PlaneDamage.cs`); torn-skin panels are `pdpanelN` flipped by injure anims
(`PlaneBuilder.cs:184,508`); the wreck is the `destroyed` subtree (`PlaneBuilder.cs:353`), per the
exploration agent. `PlaneCollider` already partitions the airframe into regions
(`PlaneCollider.cs:18,344`, traced).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C24 ☐ Engine sound: parent's or a named stock plane's

**Goal.** A mod plane sounds like its parent, or like the stock plane plane.json names.

**Evidence (confidence: lead-only).** The engine loop and pitch come from `engines.json` and the
original's banks. <TODO: where the sound is keyed to the airframe>

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C25 ☐ Hangar builder for mod airframes

**Goal.** A mod airframe can be built and customised in the plane builder with its parent's engine
rows, weapon limits and armor caps, its own slot count and its parent's default loadout.

**Evidence (confidence: traced for the store format).** `CustomPlaneStore` persists CSVM's own
JSON; the 204-byte record is import-only (`CustomPlaneDef.cs:22-26`). Engine rows are keyed per
airframe (`CustomPlaneBuild.cs:30`).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** The import path from the original's 204-byte files stays limited to ids 0-10.

## C26 ☐ Menu art: mod image or cached auto-render

**Goal.** Every menu that shows a plane's portrait or icon shows the mod's image, else a headless
side-view render made once and cached.

**Evidence (confidence: lead-only).** The hangar, paint screen and pickers show the original's flat
art (`PX_ICON`, portraits) and never a model (`model-packs.md:128`).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

# Wave D, saves, campaign, bots, multiplayer

## D31 ☐ Saves keep the mod id; a missing mod flies as its parent

**Goal.** Campaign saves and custom planes store a mod plane's id with its parent; without the mod
the plane loads as the parent with a notice, and the record stays untouched.

**Evidence (confidence: lead-only).** <TODO: where campaign profiles store the airframe id>

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** A load must never rewrite the stored id to the parent's.

## D32 ☐ Campaign unlock field

**Goal.** A mod plane enters the campaign hangar at `start`, with its parent (default) or at the
named mission.

**Evidence (confidence: lead-only).** <TODO: where the campaign's stock unlock list lives>

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D33 ☐ Bot seats may fly mod planes

**Goal.** Seats that pick or randomise a plane (vs-bot, races, multiplayer bot fill) may use mod
planes with the parent's AI def; scripted mission enemies are unchanged.

**Evidence (confidence: lead-only).** `BotSeats.cs:225`, `AiAirframePool.cs` and
`PlaneStats.LoadForAi` work from the stock defs; `SessionSpec.cs:3149` coerces non-stock planes
(per the exploration agent).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D34 ☐ Multiplayer: string airframe id and a matching mod set

**Goal.** A seat's airframe travels as a string id; a lobby advertises its mod planes by id and
hash, and a peer without a matching mod is told which one to install instead of joining with a
mismatch.

**Evidence (confidence: lead-only).** The wire sends an index into `StockAirframes.Nodes`
(`Launch/NetFlight.cs:347`, `Launch/SeatFields.cs:76,159`, `Net/NetPlaneMessages.cs`, per the
exploration agent); `StockAirframes.cs:24-26` states both peers read one order (traced).

**Approach.** <TODO: protocol version bump, lobby field>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** Packs are cosmetic and may differ per machine (`model-packs.md:263-267`); mods are
gameplay and must not.

# Wave E, the Avenger as the first mod

## E41 ☐ Package the Avenger as a local mod and fly it through every surface

**Goal.** The Avenger loads from `<dataRoot>/Mods/Planes/<id>/` with Brigand physics, the Fury's
`fd_speed`, the Warhawk's cockpit, `"turrets": []`, its tint mask live, and flies in free flight,
the hangar, the campaign, a bot seat and a two-machine multiplayer match.

**Evidence (confidence: lead-only).** Decisions 18 to 28.

**Approach.** Copy the A7 GLB and textures into the mod folder, write `plane.json` with the
Fury's `fd_speed` as a literal, retire the A7 preview pack.

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO: the at-the-controls batch>

**⚠ Traps.** The mod folder and every Avenger file stay out of the repo; the id avoids
`player_avenger`.
