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
| 19 | Avenger stats | **Brigand physics, the Warhawk's cockpit**, from the model's author; the speed is Decision 31's, not the Fury's `fd_speed` the author first named |
| 20 | The Brigand's inherited rear turret | **Cleared with `"turrets": []`**; the Avenger is a forward-gun fighter |
| 21 | Avenger hardpoints | **The Brigand's slot count**, placed on visible spots; guns may sit in pairs (inner/outer wing, lower/upper fuselage) |
| 22 | Gear and modelled stores | **Gear deleted; modelled rockets deleted and replaced by `pylonN` empties**, pylon rails and nacelle pods kept |
| 23 | Who prepares the model | **Claude through Blender MCP, scripted**; the user judges a render after each stage |
| 24 | Texture method | **Procedural in Blender, baked**, colours from the references (deep blue, yellow trim, red-orange spinners); the wing emblem stays out (IP) |
| 25 | Texture resolution | **2048 px**, HD-pack level |
| 26 | Canopy | **See-through glass (glTF `BLEND`) plus a painted frame** in the trim region; static |
| 27 | Pilot | **A new low-poly helmeted head and shoulders named `head`**, as the originals carry it in the plane model; no game data |
| 28 | Order | **Model first, previewed as a Brigand HD pack**, then the engine work with the Avenger as its first mod |
| 29 | Avenger size | **Canon size from the aircraft review the user supplied (`tmp/avenger/1932 Grumman E1-C Avenger - Google Docs.pdf`): span 38'5" (11.71 m), length 24'10" (7.57 m)**, a uniform 0.867 scale of the model; the pilot keeps human size |
| 30 | Avenger guns | **6 × .30 and 2 × .60 by default**, so the Brigand's empty W3 (upper nose, fp3/4) carries a .30 pair; this needs a default-loadout field in plane.json, built in C25 |
| 31 | Avenger speed | **250 mph (about 112 m/s) as `fd_speed`**, the review's figure |
| 32 | Avenger armour | **The Brigand's zone armour total (105) redistributed by the review's ratios, one facing per wing**: nose 50, each wing 30, tail 30 of 140, giving nose 37.5, each wing 22.5, tail 22.5, so the nose is the toughest zone. Summing a wing's lead and tail facings (60) would have made the wings outrank the nose. Range (+50 % fuel) is not adopted |
| 33 | Hangar weight capacity | **plane.json may override the hangar's weight capacity (lb), built in C25**; the Avenger's value, set in E41, lets its default 6 × .30 + 2 × .60 guns, its armour and all eight hardpoints fit (the Brigand's 11,015 lb overflows by about 130 lb before armour with eight rockets) |

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
2. ☑ Clean and orient the model: stand, gear, modelled rockets, frame
3. ☑ Cut and hinge the control surfaces, split the props, apply stock names
4. ☑ Model the canopy (frame + glass) and the pilot `head`
5. ☑ Place firepoints, pylons and `cockpit_camera`
6. ☑ UV unwrap and bake the procedural 2048 px texture and tint mask
7. ☐ Preview in game as a local HD pack over the Brigand

### Wave B, mod loading and airframe identity

11. ☑ Mod discovery and the `plane.json` format
12. ☑ One airframe registry in place of the fixed eleven-plane lists
13. ☑ Mod stats: synthetic def from `kind_of` plus literal overrides
14. ☑ Build a mod plane from its GLB

### Wave C, the plane's surfaces

21. ☑ Cockpit: own, named stock, or parent's
22. ☑ Paint: base texture, tint mask and decals
23. ☑ Damage: optional nodes and the auto-wreck
24. ☑ Engine sound: parent's or a named stock plane's
25. ☑ Hangar builder for mod airframes
26. ☑ Menu art: mod image or cached auto-render

### Wave D, saves, campaign, bots, multiplayer

31. ☑ Saves keep the mod id; a missing mod flies as its parent
32. ☑ Campaign unlock field
33. ☑ Bot seats may fly mod planes
34. ☑ Multiplayer: string airframe id and a matching mod set

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

## A2 ☑ Clean and orient the model: stand, gear, modelled rockets, frame

**Landed.** The OBJ has no display stand and no landing gear: the `Base_*` groups are prop bases and the bodies of the two finned underwing rockets, and the `Gear_*` groups are prop hubs (the "struts" under the nacelles are the downward prop blades). The rockets and their fins are deleted, the pylon rails kept. The groups are joined into named parts, centred on the fuselage axis at the wing (Blender (1.69, -4.0, 3.0) in OBJ space to the origin) and turned nose +Y in Blender, which the glTF exporter writes as nose -Z. Span 13.5 m, length 8.8 m; the OBJ's inch label is wrong, its numbers read as metres.

**Verified.** Renders went to the user after each stage; the user's corrections (the nose grille, a rail on every pylon, the 3-view drawing) are applied. The look at the controls is A7's check.

**Original approach (kept for reference).**

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

## A3 ☑ Cut and hinge the control surfaces, split the props, apply stock names

**Landed.** The surfaces are cut with bisect planes and closed faces from the untouched OBJ wing, stab and fin, sized from the user's 3-view drawing (`tmp/avenger/Grumman_E1c_Avenger.png`, about 102 px/m): ailerons from 3.5 to 5.13 m out at 28 % chord, elevators from 0.46 m to the tip at 30 % chord, one rudder at about 45 % chord reaching down between the elevators. Each sits in a hinge group with the stock names (`lailer1 > l_aileron1`, `railer1 > r_aileron1`, `lft_elev > l_elevator1`, `rt_elev > r_elevator1`, `lrudder1 > l_rudder1`); the group carries the hinge position and tilt (local X along the aileron and elevator hinges, local Y in glTF along the rudder hinge) and every surface node has an identity transform, as on the stock planes. The blades are `staticprop1` (port) and `staticprop2` (starboard) with pivots on the spinner axes; the inner flaps the drawing shows are not modelled, because CSVM animates no flaps.

**Verified.** Renders went to the user after each stage; the user's corrections (the nose grille, a rail on every pylon, the 3-view drawing) are applied. The look at the controls is A7's check.

**Original approach (kept for reference).**

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

## A4 ☑ Model the canopy (frame + glass) and the pilot `head`

**Landed.** The canopy is the hump's front 1.2 m (the drawing's glazed section): glass `canopy_glass` (glTF `BLEND`), a frame `canopy_frame` of five hoops, two sill rails and a top rail, and a solid painted `canopy_fairing` behind. The pilot is new low-poly geometry in the stock chain `pilot_pos > pilot > body > head` (torso, neck, head, leather helmet, goggles), seated under the glass. Six exhaust stacks per side (`exhausts`) follow the drawing.

**Verified.** Renders went to the user after each stage; the user's corrections (the nose grille, a rail on every pylon, the 3-view drawing) are applied. The look at the controls is A7's check.

**Original approach (kept for reference).**

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

## A5 ☑ Place firepoints, pylons and `cockpit_camera`

**Landed.** Firepoints on the barrel muzzles in Brigand slot order, odd port: fp1/2 lower nose, fp3/4 upper nose, fp5/6 inner wing, fp7/8 outer wing (W1, the Brigand's heavy pair). Pylons 1-8 at 5.6, 5.13, 4.6 and 4.05 m out, odd port, outboard to inboard, each under a copy of the model's pylon rail. `cockpit_camera` at the pilot's eyes, `exhaust1/2` on the rearmost stacks, `ground_level` 1.2 m below the origin, `target` at the origin. The positions in the plane frame are A7's rig.json.

**Verified.** Renders went to the user after each stage; the user's corrections (the nose grille, a rail on every pylon, the 3-view drawing) are applied. The look at the controls is A7's check.

**Original approach (kept for reference).**

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

## A6 ☑ UV unwrap and bake the procedural 2048 px texture and tint mask

**Landed.** One smart-project UV atlas over every painted part, one procedural material baked at 2048 px into three maps in `tmp/avenger/tex/`: `avenger_albedo.png` (deep blue; yellow cheatline, nacelle rings, wing, stab and fin tips; red-orange spinners on the nacelle nose cones; gunmetal guns, rails, blades and stacks; leather pilot; a black radiator grille with horizontal bars on the nose face; faint panel seams and grime), `avenger_mask.png` (R primary, G secondary including the canopy frame, B trim spinners; the grille is never tinted) and `avenger_detail.png` (grey detail). The blur discs get `avenger_disc.png`, a streaked translucent disc. The wing emblem and tail number art are left out.

**Verified.** Renders went to the user after each stage; the user's corrections (the nose grille, a rail on every pylon, the 3-view drawing) are applied. The look at the controls is A7's check.

**Original approach (kept for reference).**

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

**Landed.** The Avenger draws in place of the Brigand in the viewer and in flight, from a local pack
at `Z:\CSVM\tmp\avenger\pack\avenger\planes\player_brigand\` loaded with
`--hd-models=Z:\CSVM\tmp\avenger\pack`, with no Brigand part left visible. The pack holds three
files, written by `Z:\CSVM\tmp\avenger\make_pack.ps1` from `tmp\avenger\export\avenger.glb` (rerun
it after each export):

- `player_brigand.glb`, the root file: the export with renames and re-parents only, no transform
  or vertex changed. A pack replaces only a node the original draws a mesh for, and the Brigand's
  `tail`, `leftwing` and `rightwing` are meshless groups whose meshes are `g836`, `g837` and
  `g838`, so `tail_body`, `Wing_L` and `Wing_R` become those names and the zone's other meshes move
  under them. A replacement draws in the Brigand node's own frame (`model-packs.md`, What a file
  must hold), and the Avenger's surfaces, pilot and props sit in frames of their own, so those
  nodes are renamed `av_*` and merge, static, into their zone's mesh (`pilot_pos` moves under
  `nose`; the blur discs stay under the meshless `dontmove` and draw nowhere).
- `geometry.glb`, empty nodes keyed by the `geometry` ancestor, which hide the Brigand parts the
  Avenger has no stand-in for: all six surfaces, `body`, `head`, `pdp1`..`pdp8`, `pdp2_h`,
  `pdp3_h`, the turret's `g12436` and `g935`, `staticprop1`, `prop1`, `prop1b`, `nitroprop1`,
  `nosecone`.
- `rig.json`, the GLB's markers: `cockpit_camera`, `exhaust1/2`, `ground_level`, `target`,
  `firepoint1`..`8` and `pylon1`..`8`, each binding pair one port and one starboard, odd pylons
  port. No `props` or `surfaces` section: the Brigand has no node for a second prop, and its
  surface nodes are hidden.

What the preview cannot show: the control surfaces do not deflect and the props neither spin nor
show blur discs. A pack spins a prop about its own node's origin and the Brigand has one, on the
nose, so the twin props wait for B14; a deflecting surface would need its geometry expressed in the
Brigand's surface frame plus a `surfaces` hinge override, a transform rebase that is not a rename.
The Brigand's colliders, hit body, ground shadow, wreck pieces, wingtip flares (x ±5.75 m, inside
the Avenger's wing) and turret (still fires, from the Brigand's tail, unseen) stay in force, and
cockpit view draws the Brigand's `cockpit1` at the moved eye. The GLB needs no change for B14,
which builds from its own frames; every change above is preview-only and lives in the script.

**Verified.** <pending orchestrator run: user at the controls>

**Original approach (kept for reference).**

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

## B12 ☑ One airframe registry in place of the fixed eleven-plane lists

**Landed.** `CSVM/src/Mech3/AirframeRegistry.cs` is the one list of player airframes. It sits in
`Mech3` because `MarkerRig` and `InstantAction` read it and the family order forbids `Mech3`
naming `Flight`. `AirframeRegistry.Stock` is the eleven stock `AirframeEntry(Node, DisplayName,
StockNode)` rows in airframe-id order (the display names are `camparam.json`'s block keys);
`Shared.All` is the stock rows then the mods in registration order; `Find(node)`,
`StockNodeOf(node)` (a mod's parent, a stock or unknown node unchanged) and
`Register(id, displayName, parentNode)`, which throws `ArgumentException` for a blank argument, an
id that is a stock node or already registered (case-insensitive), or a parent that is not a stock
node. Registration swaps a copied array under a lock, so readers need none.

- `StockAirframes` (Flight.Hangar) keeps its int-id API (`Nodes`, `Node(int)`, `IdOf`), now built
  from `AirframeRegistry.Stock`; `IdOf` answers null for a mod. Every caller of it is a site that
  needs a string id in D31/D34/D33/D32/C25.
- `MarkerRig.PlayerAirframes` is `Stock` projected to (node, display), in id order (it was the
  Bloodhawk-first dump order). `Probes.StockAirframes` returns `StockAirframes.Nodes`, so
  `--dump-markers`, `--dump-flight=all` and the parity ledger list airframes in id order.
- `InstantAction` keeps only the ia.json names in the langui 3700 order and takes each row's node
  from `Stock[i]` (the two orders are the same).
- `EffectCatalogue.DestroyAnimFor` and `CamParams.Load` resolve the plane node through
  `StockNodeOf`, so a mod plays its parent's destroy def and reads its parent's camera block.
- `CustomPlaneDef.MaxAirframe` is `StockAirframes.Nodes.Count - 1` (a static readonly, no longer a
  const); `SuiteConstants.PlayerAirframes` is gone, `CombatSuites` counts `MarkerRig.PlayerAirframes`.
- `SessionSpec`'s `--vs-bot` check asks `AirframeRegistry.Shared.Find`, so a registered mod id is
  kept, provided mods are registered before the spec is parsed.
- Coverage: `CSVM.Tests/AirframeRegistryTests.cs`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

## B13 ☑ Mod stats: synthetic def from `kind_of` plus literal overrides

**Landed.** A post-load override, not a synthetic def in the chain. `ModPlane.Stats(zrdr, forAi,
aiDefName)` (`Flight/Hangar/ModPlanes.cs`) loads the parent through `PlaneStats.Load` or, for a
bot, `PlaneStats.LoadForAi` on the parent's AI def (Decision 17), then sets each override through
`PlaneStats.OverrideDefField(name, value)` and clears `TurretMounts` under `"turrets": []`. It
lives on `ModPlane` because nothing else in `Flight` may name `Flight.Hangar`
(`FamilyOrderTests`). A post-load override serves both loads with one rule, since every accepted
field reads down the player chain on both, and a synthetic def would have needed a single-float
check (`rec_moments_inertia` is a triple) and a copied `destroyable_parts` block. Accepted names:
the nine scalar `dynamics` keys (`pitch_torque`, `roll_torque`, `rudder_torque`, `return_rate`,
`ang_momentum_damp`, `fd_speed`, `drag_factor`, `veh_weight`, `ref_area`), `flight_ceiling` and
`fuel` on the def, and `<zone>_hp` / `<zone>_armor` for `nose`, `tail`, `leftwing`, `rightwing`;
any other name logs `mod plane <id>: override <name> is no def field CSVM reads, ignored` and the
mod still loads. `DefName` and `NodeName` stay the parent's, which keys the stock fit and the AI
def. The turret slot: `ModPlane.StockLoadout(StockLoadouts)` returns the parent's fit with each
`turret: true` gun slot copied as a forward slot on the firepoints it names (the Brigand's W4 on
`firepoint1`, `firepoint2`), calibre, ammo and twin bit unchanged; every turret slot in
`stock_loadouts.json` already names its slot's binding pair. The stock fits are untouched. Format:
`docs/formats/mod-planes.md`, "Overrides". Coverage: `CSVM.Tests/ModPlaneStatsTests.cs`, four cases
over the synthetic tree's defs and the committed loadouts.

Wiring left for later items: B14 or the session builds a mod plane's stats with `mod.Stats(zrdr)`
and its fit with `mod.StockLoadout(stock)` where a stock plane uses `PlaneStats.Load` and
`StockLoadouts.For(stats.DefName)`; D33 passes `forAi: true`; C25 hands `StockLoadout`'s fit to
`CustomPlaneBuild` as the stock base, and gives a turret-clearing mod a zero `HangarEconomy`
turret mask. A turret-converted slot keeps its stock `Mount` label ("Rear Turret"), which only logs
and labs print.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

## B14 ☑ Build a mod plane from its GLB

**Landed.** The mod's model is grafted into planes.zbd rather than given a build path of its own, so
every reader that takes a plane node (markers, `PlaneCollider`, the hit body, gauges, the crash rig,
the livery lookups, the viewer) reads a mod as a stock airframe with no change of its own.
`Mech3/ModAirframes.cs` `Add(planes, id, cockpitFrom, glb)` parses `plane.glb` through
`HdModelMeshes` (under the held key `planes/<id>/<id>`, `HdModels.Holding`) and appends one gamez
node per glTF node at its own transform under a root named the id, a meshed node carrying its
triangles (original winding, `HdModelMeshes.ReversedSurfaces`) under one untextured material. A lone
scene root named the id or a stock node (an export's `player_brigand`) stands for the root. A file
with no `cockpit1` under its root gets a copy of `plane.json`'s `cockpit`, else the parent's interior,
meshes shared, which `PlaneBuilder` mounts at the mod's `cockpit_camera`; that settles most of C21,
which keeps the own-`cockpit1` check. `GameZ.TablesGrew` drops the lazy maps the append outgrows.
Drawing goes through the model pack path: `SceneBuilder.OwnMeshes` (set by `PlaneBuilder` from
`ModAirframes.MeshesFor` on the airframe, interior and stereo body builders) replaces the pack, so
each node draws the file's mesh by the root-file rule and the pack's material rules, with the gamez
twin under `GamezMeshMeta` for the gameplay readers. A user's HD pack never reaches a mod's meshes;
its `planes/<id>/rig.json` does.

- **Launch.** `Launcher` opens `ModPlanes.Current = ModPlanes.Open(dataRoot, folder)` beside the
  model packs, the folder being `--mods=<folder>` (new, `docs/cli.md`) or `Mods`, and none under
  `--det` or `--run-tests` unless the flag names one. `BindGameData` (each game data bind, the source
  picker's included) calls `BindModPlanes`: `ModAirframes.Add` per mod, then
  `AirframeRegistry.Shared.Register` once; an `InvalidDataException` (the model does not import, the
  id is taken) or `ArgumentException` goes through the new `ModPlanes.Skip(mod, reason)`, which logs
  it and lists it in `Skipped`. The `--vs-bot` check still runs at parse, before registration, so a
  mod there flies as Random (D33).
- **Stats and fit.** `GameSession.StatsFor` takes `mod.Stats(zrdr)` and `AiStatsFor`
  `mod.Stats(zrdr, forAi: true, aiDef)` for a mod id; `InspectionLabs` (the viewer's damage lab) takes
  `mod.Stats` too. `HumanFlightAdapter` binds `mod.StockLoadout(stock)` with no `--loadout=`.
- **Fallbacks for later items.** Camera block and destroy def through `StockNodeOf` (B12); AI stats
  through the parent's AI def; no wreck pieces or damage panels without `destroyed`/`pdpN` nodes
  (`BuildDestroyed` returns null and the crash plays without pieces, C23); the parent's engine sound
  through the def (C24); the painter reads its skin prefix off the copied interior's decal textures
  (the Brigand's `bri`), so the file's surfaces named after that airframe's skins wear the livery and
  every other material is fixed paint (C22); the hangar, menus, saves, campaign, bots and the wire still
  list the stock eleven (C25, C26, D31 to D34). `AiFlightAssembler` builds a mod by node unchanged.
- **Gitignore.** `/Mods/` beside `/HDModels/`.
- Format: `docs/formats/mod-planes.md`, "`plane.glb`" and "Flying a mod". Coverage:
  `CSVM.Tests/ModAirframesTests.cs` (the graft), `HdModelsTests.AHeldModelAnswersOnlyItsOwnKey`,
  `SessionSpecParserTests.TheModsFolderFlagParses`, and the `mod-plane-build` suite: the Brigand's
  flight build exported into the scratch directory and wrapped as a mod with `fd_speed` 123.5 and
  `"turrets": []`, grafted into `ctx.PlanesCopy()`; every drawn mesh outside the interior comes from
  the file, 1315 visible triangles, 15 hulls, 8 surfaces, 2 props, 17 weapon markers, the reference
  markers and the cockpit camera match the Brigand, and the fit binds 4 hardpoints with every gun group
  firing forward. Probes over the same files (`--mods=<scratch>\Mods --plane=suite_mod_plane`) flew C1
  in chase and cockpit view with guns firing, crashed with `--crash=30`, and opened `--viewer`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

## C21 ☑ Cockpit: own, named stock, or parent's

**Landed.** B14's graft already chose the interior (the file's own `cockpit1`, else a copy of
`plane.json`'s `cockpit`, else the parent's), and `PlaneBuilder.MountCockpitInterior` mounts it at
the mod's `cockpit_camera` through `FlatMount`/`StereoMount` as on a stock plane. The own branch
needed no change: a stock `cockpit1.glb` export put under the root draws from the file by the root-file
rule, its gauges bind by the stock node names and its belts and zones by the texture names in
`InteriorMaterials`, and the stereo build cuts its head plate by shape (`CockpitPlate.CutReplaced`).
`cockpit1`'s own transform is dropped by the mount, so its subtree is read in interior units with the
eye at its origin.

- **Fixed: a copied interior drew from the mod's file.** The interior builder resolved the copy's
  nodes through the mod's file like the airframe's, and a scoped lookup takes the one file node of a
  name wherever it sits. Every interior shares generic names (`gauges`, `horiz`, `pfhorizon`, `speed`,
  `comp`, `mgarrow`), so a file holding any interior under another name (a modder's leftover reference)
  replaced 59 of the copied Warhawk interior's 69 meshes with the Fury's. `ModAirframes.Graft` now
  returns whether the file holds its own `cockpit1`, `ModAirframes.HasOwnInterior(node)` exposes it,
  and the interior builder takes no `HdAirframe` for a copied interior, so it draws the archive's meshes
  alone; a user's HD pack stays off it, as B14 already kept it.
- **Headset.** The stereo interior of each branch builds: a copy omits its head plate polygons
  (`CockpitPlate.Find` on the archive), the own file's plate is cut by shape (25 Fury triangles), and a
  model pack's `{ "cockpit": { "from": "body" } }` rig under `planes/<mod id>/` builds the body cockpit
  panel and its stereo body twins on an own-interior and a copied-interior mod.
- Format: `docs/formats/mod-planes.md`, `plane.glb` Cockpit row and Evidence. Coverage: the
  `mod-plane-cockpit` suite (Brigand mods over `ctx.PlanesCopy()`: the Fury's interior exported into
  the file as `cockpit1` with every state kept and the Warhawk named, the Warhawk named over a file
  holding the Fury's interior as `fury_reference`, and no name; each checks the mount at the mod's
  `cockpit_camera`, which meshes come from the file, the visible triangles and the gauge bind against
  the donor on its stock plane, and the stereo plate; then the body cockpit rig), and
  `ModAirframesTests.TheDonorCockpitIsCopiedOnlyWhenTheFileHasNone` for `Graft`'s answer. With the fix
  reverted the suite fails on the named branch (`from_file=59 drawn=69`, 1098 triangles against the
  Warhawk's 1060). Probe: the Avenger mod in C1 cockpit view draws the Warhawk interior with live
  gauges, `Z:\CSVM\.scratchKeep\mod-planes\c21\cockpit.png`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A mod plane's cockpit view uses its own `cockpit1`, else the stock plane plane.json
names, else the parent's, placed at the mod's `cockpit_camera`.

**Evidence (confidence: lead-only).** `cockpit1` is the stock interior (about 25 units per metre,
scaled 0.04 at `cockpit_camera`, `model-packs.md:63`); gauges come from `CockpitTransplant.cs`;
`PlaneBuilder.cs:192,324` reads `cockpit_camera` (per the exploration agent).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C22 ☑ Paint: base texture, tint mask and decals

**Landed.** `plane.json` takes an optional `"tint": { "mask": "<png>", "detail": "<png>",
"materials": ["<glTF material>", ...] }`, validated in `ModPlanes` (an object; both paths `.png` files
inside the mod; a non-empty list of names; `_` keys prose, any other key skips the mod) and exposed as
`ModPlane.Tint`. `Launcher.BindModPlanes` decodes the two PNGs into a `Mech3/ModTint.cs` and hands it
to `ModAirframes.Add`, which keeps it per id beside the file and the interior donor (`TintFor`,
`DonorOf`); a PNG that does not decode skips the mod through `ModPlanes.Skip`.

- **Rendering.** Per texel `base * (1 - cover) + detail / 128 * (r * colour1 + g * colour2 + b *
  colour3)`, `cover` the mask channels' sum capped at 1 (a larger sum shared out by channel), alpha
  the base's, mask and detail sampled nearest at the base texture's size. The proposed `detail ×
  colour` is kept with grey 128 as the neutral point instead of 255: the Avenger's detail map sits
  at grey 105 to 145 inside the masked regions (mean 142), so a `/255` reading would show every
  livery at about 55 % brightness, while `/128` shows the swatch colour itself on flat panels.
  Outside the mask, with `--paint=none` and on a mod without `tint`, the base texture shows unchanged.
- **Channel mapping.** R = `paint_color1` (body), G = `paint_color2` (dark trim), B = `paint_color3`
  (light trim), the scheme's own slot order (`paint.md`, "Slot order, confirmed"). A pattern, a
  militia's scheme and `--paint-color=` all arrive as those three colours, so every livery source
  tints the same way, and any pattern's colours reach a tinted mod, even one outside the donor
  airframe's pattern list (`PlanePainter.PatternMissesAircraft` is false under a tint).
- **Decals.** No prefix to declare: on a mod's own surfaces any material ending in `_noselogo`,
  `_taillogo` or `_winglogo` takes that decal, the suffix test `PlanePainter.Substitute` already
  made. A material named after a donor placeholder (`bri_noselogo`) still goes the original's way.
- **Reach.** `SceneBuilder.MappedReplacement`, for a mod's own file only (`OwnMeshes`), passes each
  non-original material's name and base texture through the builder's substitute and records it for
  `Repaint`; a model pack's surfaces are untouched. `PlanePainter` takes the tint as an optional
  argument: `PlaneBuilder` (flown aircraft, network seats, the viewer's livery lab through `Repaint`,
  damage panels and wreck) reads `ModAirframes.TintFor(root)`, and `AiFlightAssembler.PainterFor`
  passes it for AI aircraft. The skin prefix (pattern list, donor skins) is the interior donor's;
  `PrefixFor` falls back to `DonorOf` so a mod with its own `cockpit1` still paints.
- Format: `docs/formats/mod-planes.md`, "Paint" and the `tint` key. Coverage:
  `CSVM.Tests/ModTintTests.cs` (5 cases: channel per slot, partial blend with detail, an overfull
  sum, nearest sampling, short images), `CSVM.Tests/ModPlanesTests.cs` (the field and nine invalid
  forms), and the `mod-plane-paint` suite over a hand-made GLB in the scratch directory (flown builds
  under two schemes, an AI build's shared painter, a livery lab repaint, no livery, the noselogo decal
  per scheme). Viewer probes of the local Avenger under `player_fortune`, `blackhat`, `hughes` and
  `none` are in `Z:\CSVM\.scratchKeep\mod-planes\c22\`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

## C23 ☑ Damage: optional nodes and the auto-wreck

**Landed.** A mod that ships `destroyed` (pieces under the stock names) or `pdpN`/`pdpN_h` nodes uses
them unchanged: B14's graft already built both, so this item adds nothing on that path. For the rest,
`Flight/Airframe/AutoWreck.cs` runs in `WorldEffectsFactory.CrashRigBuild.BuildWreck`, for a mod only
(`ModAirframes.MeshesFor`), before the bind indexes the rig's names. Stock planes never reach it.

- **Pieces.** `AutoWreck.Cut(planeModel, controller.Collider.Parts)` walks the drawn meshes under
  `healthy` (hidden subtrees and blur discs skipped, as `PlaneCollider.Collect` does) and gives every
  triangle the zone of the first collider hull holding its centroid, else the nearest hull, mapped
  through `PlaneDamage.MapStruckPart`, so a piece is exactly the zone a hit there is charged to. The
  collider's own regions are fuselage, tail and wing, not the four zones; the mapping splits the
  fuselage at z 0 and the wings by side, as the damage ledger does. `Build()` returns `destroyed >
  piece1..piece4` = nose, tail, left wing, right wing, each piece's origin on its zone's bounds centre
  and each mesh a copy of its source's vertex arrays with only that zone's indices and the source's
  active material (so the livery and repaint reach it). The stock crash and destroy defs bind pieces by
  name, so the parent's `player_crash_*` and destroy def fling them with no new choreography. The nose
  is `piece1` because the stock destroy def flies `piece1` on the aircraft's momentum alone while
  pieces 2 to 4 add their own 1 to 3 m/s (`docs/org/objectMotion.md`).
- **Fire and smoke.** The parent's defs site them at named nodes, found by a `--debug-anim` probe of
  the Avenger: `player_crash_default` hangs `large_firetrail` on `piece1`, `piece3`, `piece4` and
  `call_crash_trails` on `destroyed`; `pdpanel1`..`8` call `gimmeflakes`, `short_firetrail`,
  `small_fireball(_follow)` and `yellow_sparks_follow` onto `pdp1`..`pdp8`; `player_fuelleak` puts
  `fuel_trail` and `3040slug_gunhit` on `pdp1`/`pdp3`. Before C23 all of these resolved nothing on the
  Avenger and landed on the airframe root. The crash fires now ride the pieces, so they start at the
  zone centres. `AddAnchors(damage.Zones)` adds an empty node under `healthy` at the zone centre for
  each missing `pdpN` whose `pdpanelN` sits on that zone's ladder (Brigand parent: 1, 2, 6 right wing,
  3 to 5 left, 7 nose, 8 tail), and for each missing AI-stage anchor (`prop1` nose, `lailer1`,
  `railer1` wings, `lft_elev`, `rt_elev` tail) and zone name (`nose`, `tail`, `leftwing`,
  `rightwing`). The Avenger carries the zone and AI names, so it gets the eight `pdpN`.
- **No torn skin** on a mod without `pdpN`: the stage plays its effects at the anchor and flips nothing.
- Format: `docs/formats/mod-planes.md`, "Damage". Coverage: the `mod-plane-wreck` suite over a
  hand-made fuselage, two wings and a fin in the scratch directory (four pieces, each piece's
  triangles in its own zone and on its own side, all 48 drawn triangles in exactly one piece, every
  missing anchor at its zone centre, the model's own `nose` kept), plus a second mod whose own
  `destroyed > piece1` builds from its file. The Avenger probe logs `zones cut from the mesh nose
  12492, tail 5803, leftwing 2758, rightwing 2758 triangle(s), 8 effect anchor(s) added` and no
  unresolved retarget left in the crash or damage runs; captures in
  `Z:\CSVM\.scratchKeep\mod-planes\c23\`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

## C24 ☑ Engine sound: parent's or a named stock plane's

**Landed.** The engine sound is chosen per airframe only by the def chain's engine-slot keys,
`engine_sound`, `cockpit_engine_sound`, `damaged_engine_sound` (with its pitch range) and
`prop_sound` (the whine, unauthored install-wide), which `PlaneStats` reads down the player chain on
the flown and the AI load alike. engines.json carries engine power only. The volume and pitch curves
are player.json's and every airframe's, the Enhanced mode's `EnhancedSoundTuning` is one set of
curve amounts for every airframe, and a sound pack replaces a sound by its WAV name, so none of them
keys on the airframe and each follows the defs. `ModPlane.Stats` therefore ends with
`stats.TakeEngineSounds(PlaneStats.Load(zrdr, Sound))` when `sound` is set, a new `PlaneStats` method
copying those seven fields (`DamagedTimer`, the def's shared re-arm timer, stays the parent's). Every
non-test stats load of a mod goes through `ModPlane.Stats` (`GameSession.StatsFor`/`AiStatsFor`,
`InspectionLabs`), so `FlightAudio`, `AiEngineAudio` and the sound lab hear it. No new audio file or
loader.

- Coverage: `ModPlaneStatsTests.SoundTakesTheNamedPlanesEngineSoundsAndAbsentKeepsTheParents` (the
  synthetic tree's Kestrel mod with `"sound": "player_warhawk"` carries the Warhawk's engine-slot defs
  on the flown and the bot load, keeps the parent's `fd_speed` and def name, and without `sound` the
  Kestrel's loop).
- Probe, the local Avenger in C1: without `sound`,
  `[flight] audio: engine=snd_brigandengine damaged=snd_damagedengine whine=none (no def names prop_sound) rattle=snd_planeshake`;
  with `"sound": "player_warhawk"` added for the run and removed after,
  `[flight] audio: engine=snd_warhawkengine damaged=snd_damagedengine whine=none (no def names prop_sound) rattle=snd_planeshake`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A mod plane sounds like its parent, or like the stock plane plane.json names.

**Evidence (confidence: lead-only).** The engine loop and pitch come from `engines.json` and the
original's banks. <TODO: where the sound is keyed to the airframe>

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## C25 ☑ Hangar builder for mod airframes

**Landed.** A build on a mod airframe is a `CustomPlaneDef` whose `Airframe` is the parent's stock
id and whose new `ModAirframe` is the mod's id, so every int-indexed reader (stat table, engine
rows, art, prose, campaign, wire) reads the parent with no change, and the readers that know mods
read the id.

- **plane.json.** Two keys, parsed in `ModPlanes.Load` and documented in
  `docs/formats/mod-planes.md`: `hangar_capacity` (whole lb, 1 to 100000, `ModPlane.HangarCapacity`)
  and `loadout` with `guns` (exactly four slots W1-W4, each `null` or `{ "calibre": 30|40|50|60|70,
  "twin": true|false }`, twin by default, `ModPlane.DefaultGuns` as `GunChoice`s) and
  `hardpoints_per_wing` (0-4, `DefaultHardpointsPerWing`). Any other key, a wrong count or an
  out-of-range value skips the mod with the reason. The Avenger's default is
  `[{"calibre": 60}, {"calibre": 30}, {"calibre": 30}, {"calibre": 30}]`.
- **Default loadout.** `ModPlane.StockLoadout` is the parent's fit with the turret conversion, then
  the loadout's guns and pylons through `CustomPlaneBuild.LoadoutFor` (the hangar's own join), each
  part kept from the parent when absent. Flight with no `--loadout=` and the hangar defaults both
  read it; `ModPlanes.StockFitFor(stock, node)` picks it or the stock table by node (Instant
  Action's loadout screen, the local lobby's plane panel).
- **Economy.** `HangarEconomy.StatsFor(airframe, mod)` is the parent's row with `hangar_capacity`
  and, for `"turrets": []`, turret mask 0, so the converted slot is priced and weighed as a wing
  gun. `Price(def, mod)` prices on it; `Price(def)` and `StatsFor(def)` read the mod from
  `ModPlanes.Current`. `HangarDescriptions.Airframe` takes the row for the TURRETS and capacity
  lines.
- **Builder.** `HangarFeature` airframe ints are rows: 0-10 stock, 11 + i the i-th mod of `Mods`
  (else `ModPlanes.Current`). `AirframeRows`, `AirframeRowOf(def)`, `ModOfRow`, `StockIdOfRow`,
  `Price(def)` and `ScratchStats` are new; `PickAirframe`, the defaults ask, `LoadAirframeDefaults`
  (mod fit, engine 1, armour off `mod.Stats` so `<zone>_armor` overrides count), the names and every
  bill take rows. `OriginalHangarScreen` lists `AirframeRows` and draws a mod row with its parent's
  blueprint and prose under the mod's name. New aid `--menu=plane-construction:mod`.
- **Store.** `CustomPlaneStore` writes `"modAirframe"` after `"airframe"` only for a mod build, so a
  stock file is byte for byte as before; no version bump (the loadout block's rule).
- **Missing mod (D31's rule for custom planes).** `CustomPlaneDef.ModIn(mods)` is null for a mod
  that is not installed, with one `flight` warning per id; the record keeps its id, and the build
  flies, prices and lists as the stored parent. `FlownNode(mods)` is the mod id or the parent node.
- **Pickers.** `PlayerSetupFeature.BuildRoster` (every Original picker through
  `OriginalRosters.Roster`) and `PlanePickerRoster.Build` fly a mod build as the mod's id. Instant
  Action's Pilot Plane list matches a build to the feature's stock airframe by its parent
  (`StockNodeOf`), so the build is pickable and launches on the mod node with its def.
- **Int-only sites, parent's id.** `CampaignWallet.Purchase` (the profile's `OwnedPlane.Airframe`,
  so the campaign flies the parent), `IHangarWallet.IsAirframeAvailable` (the parent's availability,
  D32's `with_parent` default), `CustomPlaneWire.Of` (the wire, lobbies and `SeatFields` see a stock
  build of the parent), `CustomPlaneBuild.EnginePowerFor` (the parent's engines.json rows, which is
  the inheritance), the inventory icon and the campaign pages. `CustomPlaneRecord` imports stay
  stock. The seat picker's silhouette and ratings (`OriginalSeatPlane`) skip a mod node, as before.

Coverage: `CSVM.Tests/ModPlaneHangarTests.cs` (both fields and 16 invalid forms, the Avenger's
6 x .30 + 2 x .60 forward fit, the capacity and turret-mask pricing, the store round trip, the
missing-mod fallback, the builder offering, defaulting, committing and reopening a mod build) and
the `menu-original-hangar-mod` suite (Original's hub with a scratch mod: the list's twelfth row,
the pick, the loadout, the capacity, the commit and the picker roster). Screenshot:
`.scratchKeep/mod-planes/c25/builder-avenger.png`, the airframe list open on the E-1C Avenger with
capacity 12,500 lb and no turret.

The Avenger's weight at 12,500 lb: its default guns, engine tier 2 and the parent's two pylons a
wing weigh 10,645 lb before armour; all eight pylons weigh 12,565 lb before armour (each armour
press 20 lb). E41 sets the capacity Decision 33 asks for against those figures.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A mod airframe can be built and customised in the plane builder with its parent's engine
rows, weapon limits and armor caps, its own slot count and its parent's default loadout. Two
plane.json fields arrive here: a default loadout (Decision 30) and a hangar weight capacity in lb
(Decision 33), both literals that replace the parent's.

**Evidence (confidence: traced for the store format).** `CustomPlaneStore` persists CSVM's own
JSON; the 204-byte record is import-only (`CustomPlaneDef.cs:22-26`). Engine rows are keyed per
airframe (`CustomPlaneBuild.cs:30`).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** The import path from the original's 204-byte files stays limited to ids 0-10.

## C26 ☑ Menu art: mod image or cached auto-render

**Landed.** `CSVM/src/UI/Hangar/ModPlaneArt.cs` holds three pictures per mod, by node: the portrait
(`plane.json`'s `portrait` image, decoded by extension, else a three-quarter render from ahead, left
and above in the mod's own paint with no livery) and black plan and head-on silhouettes, rendered
whether or not the mod has a portrait. `Launcher.BindModPlanes` calls `ModPlaneArt.Bind`, which
decodes the portrait and loads the cached renders from `mod_art/` under Godot's user folder, named
`<id>_<hash[..16]>_<view>.png`; a deferred `RenderPending` builds what the cache lacks on the first
frame. The render reuses the suites' synchronous viewport read (`ForceDraw` twice, then `GetImage`):
`PlaneBuilder` over the launch chapter's textures, an orthographic camera fitted to the visible meshes'
box in a 512 px transparent `SubViewport` with its own world, one sun and white ambient, the read
cropped to its used rect. A render in `_Ready` reads back blank, hence the deferral, and
`OriginalPresentation`'s tick recomposes once on `TakeLanded`. A headless run renders nothing.
`Picture(node, view, x, y, w, h)` fits one into a slot's box (aspect kept, centred) as `Held` art, or
answers null to keep the parent's art.

- **Slots covered.** Plane Construction's picture on every tab (`OriginalHangarScreen.ComposePlanePicture`:
  the focused mod row while listing, else the build's `ModAirframe`), in the parent blueprint's
  358 x 335 box, so the paint tabs show the portrait in place of the parent's tinted icon set; the
  per-seat aircraft screen (`OriginalSeatPlane`) shows a mod build's plan silhouette in the 80 px
  `FC_PlaneIcons` frame, and its ratings, which skipped a mod node, now rate it on the parent's stat
  row and `ModPlanes.StockFitFor`; Instant Action's Weapon Loadout draws a mod build's plan and head-on
  silhouettes in the `OL_PLANEDIAGRAMS*` frames.
- **Left on the parent's art.** The campaign pages (flight check, plane selection, cabin photo, ammo
  diagrams, previous missions), because the campaign profile flies the parent's stock id (C25, D32);
  the network lobby's plane and ammo icons and the bot panel, because the wire and bots carry stock ids
  (D33, D34); the local lobby's Select Plane column, which already names a mod build as "Stock <parent>"
  with the parent's ratings, so a mod icon there would contradict its caption.
- Format: `docs/formats/mod-planes.md`, "Menu art". Coverage: `CSVM.Tests/ModPlaneArtTests.cs` (cache
  name, portrait choice, fit), the `mod-plane-art` suite (windowed, `analysis/headless-limits.json`:
  a Brigand export renders a coloured portrait and black silhouettes into a scratch cache under its
  hash, a second bind renders nothing and keeps the files, a changed `plane.json` renders under its new
  hash, a mod with a 6 x 4 portrait shows it and renders only its silhouettes, a stock node has no
  art), and `menu-original-hangar-mod`, whose mod now carries a portrait the hub draws in the
  blueprint's place. Screenshot of the local Avenger (no portrait, so the render branch):
  `Z:\CSVM\.scratchKeep\mod-planes\c26\builder-avenger.png`, with its three renders beside it.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Every menu that shows a plane's portrait or icon shows the mod's image, else a headless
side-view render made once and cached.

**Evidence (confidence: lead-only).** The hangar, paint screen and pickers show the original's flat
art (`PX_ICON`, portraits) and never a model (`model-packs.md:128`).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

# Wave D, saves, campaign, bots, multiplayer

## D31 ☑ Saves keep the mod id; a missing mod flies as its parent

**Landed.** Custom planes already kept the id (C25); this item adds the campaign profile. An owned
plane is `OwnedPlane { Airframe, ModAirframe }` in `Session/Campaign/CampaignProfileStore.cs`, the
parent's stock id beside the mod's id, the same pair as `CustomPlaneDef`.

- **Profile field.** `profile.json` writes `"modAirframe"` right after a plane's `"airframe"`, only
  for a mod plane, so a stock profile is byte for byte as before. No version bump: the reader takes
  each field by name and a missing one reads at rest, the rule every optional field of this store
  follows; version 3 stays. An older build reads such a file and flies the parent, and drops the key
  if it saves.
- **Missing mod.** `ModPlanes.FindOrWarn(id, record, parent)` is the one lookup for a saved record's
  mod id, with the per-id warning set moved there from `CustomPlaneDef`; `CustomPlaneDef.ModIn` and
  the new `OwnedPlane.ModIn`/`FlownNode` call it. A missing mod resolves to null, the plane flies and
  is named as `Airframe`, the log says `campaign plane <name>: mod airframe <id> is not installed, it
  flies as <parent>` once per id, and nothing writes the record back changed.
- **Campaign sites and how each resolves a mod.**
  - `CampaignWallet.Purchase` takes the build's `ModAirframe` (through `IHangarWallet.Purchase`,
    from `HangarFeature.Commit`) and records it; a rebuild on a stock airframe clears it. Its
    starting-spec fallback in `BuildFor` copies the id.
  - `CampaignFeature.BuildExit`, the flight check's FLY MISSION: the seat's node is
    `plane.ModIn(ModSet)?.Id`, else the parent's node; the build is the stored one, which carries
    the id. `CampaignFeature.Mods` (null reads `ModPlanes.Current`) mirrors `HangarFeature.Mods`.
  - `CampaignDirector.ResolveSeatedPlane`, a `--campaign=` launch: `plane.FlownNode(ModPlanes.Current)`.
  - `CampaignFlightField.CopyOf`, a local split-screen seat's copy, keeps the id.
  - `CampaignPlaneSelectionPage` and `CampaignFlightCheckPage` name an installed mod's plane by its
    plane.json `name` (slot label, combo entry, export notice); a missing mod's by the parent.
  - Kept on the parent's stock id: a co-op host's launch (`BuildExit(networked: true)`, which
    `OriginalCampaignScreen` passes for a co-op host, since a guest sees the host's seat through the
    stock-index wire), the co-op hangar words and wingman message (`CampaignFeature`, D34), the
    wingman's own plane (`CampaignDirector.BindWingman`, so the host's and a guest's wingman stay one
    airframe), the mission records (`MissionRun.Airframe`, the per-airframe kill tallies), the art
    (`PC_P_HANGAR<n>`, the plane diagram, the inventory icon) and the stock-fit fallbacks for an
    unbuilt plane (a mod plane is always built). `CampaignDirector.CreditKill` reads scripted
    enemies, which stay stock (Decision 17).
- Docs: `docs/formats/mod-planes.md` "The campaign", `docs/formats/saved-games.md` "Funds and the
  plane list", the `CampaignProfileStore`, `CampaignWallet`, `CampaignFeature` and `ModPlanes`
  architecture entries.
- Coverage: `CSVM.Tests/ModPlaneCampaignTests.cs` (a stock profile writes no `modAirframe` and
  round-trips; a mod plane round-trips its id; an installed mod seats as its node and as the parent
  on a networked launch; a missing mod seats as the parent with one warning and the file stays
  byte-identical through the launch's save; a purchase records the id and a stock rebuild clears it)
  and the `campaign-mod-plane` suite: a `--campaign=` launch over a `--profiles=` store seats an
  installed mod and builds the seat from its `plane.glb`, seats the parent without it, and a load and
  save leave the profile byte-identical with the id. Probe, the local Avenger in a scratch
  `--profiles=` store at seq 0 (C3/M01): with `--mods=Z:\CSVM\Mods` the log reads `campaign:
  'D31Probe' seated in "D31 Probe Blue" as avenger_e1c` and the launch cutscene shows the Avenger in
  the zeppelin hangar (`Z:\CSVM\.scratchKeep\mod-planes\d31\campaign-avenger.png`); without it
  `campaign plane D31 Probe Blue: mod airframe avenger_e1c is not installed, it flies as
  player_brigand`, the Brigand in the same pose (`campaign-missing-mod.png`), and the profile file's
  hash unchanged.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Campaign saves and custom planes store a mod plane's id with its parent; without the mod
the plane loads as the parent with a notice, and the record stays untouched.

**Evidence (confidence: lead-only).** <TODO: where campaign profiles store the airframe id>

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** A load must never rewrite the stored id to the parent's.

## D32 ☑ Campaign unlock field

**Landed.** The stock unlock is `HangarEconomy.Airframes[id].Availability`, compared by
`CampaignWallet.IsAirframeAvailable` as `MissionsCompleted + 1 >= Availability` (`FUN_00410120`), so
availability is the 1-based ordinal of the mission from which the hangar offers the airframe. A mod
gets a number in the same units, `ModPlane.CampaignAvailability`, and the wallet compares it in
place of the parent's row: `IsAirframeAvailable(airframe, mod)` on `IHangarWallet`, which
`HangarFeature.Refusal` calls with `Scratch.ModIn(ModSet)`. The purchase refusal is the only gate
(the airframe list offers every row and refuses at the commit, as for stock airframes).

- **The rule.** `start` is 1, offered from the first mission. `with_parent` and an absent field are
  the parent's availability. A mission id is `<world folder>/<mission folder>` as `cm_sequence`
  stores it (`c1b/m02`), and a chapter id the world folder alone (`c1b`), matched without case; the
  mission's ordinal, or the chapter's first mission's, is the availability, so the hangar offers the
  mod once the missions before it are completed. An id the sequence does not hold warns `mod plane
  <id>: unlock <value> names no campaign mission or chapter, it unlocks with its parent <parent>` and
  uses the parent's. The unlock-everything cheat (`fAllowAll`) unlocks mods too.
- **When it is checked.** `ModPlane.BindCampaign(missions)`, which `Launcher.BindModPlanes` runs at
  each game data bind after the grafts, over `CampaignSequence.Load(_paths.Data.Zrdr)`. B11 still
  accepts any non-empty string at load. Before a bind (an off-engine test), a mission or chapter id
  reads as the parent's.
- Docs: `docs/formats/mod-planes.md` (`unlock` row, "The campaign" table, Evidence), the
  `CampaignWallet` and `ModPlanes` architecture entries.
- Coverage: `ModPlaneCampaignTests.TheUnlockFieldSetsWhenTheHangarOffersTheMod` (`start`,
  `with_parent`, `C1B/m02`, `c1b`, an unknown `c9/m01` with its one warning, over a four-mission
  list, and the wallet's answer either side of the mission) and the `campaign-mod-plane` suite (a
  mod unlocking at the mission id of `cm_sequence` seq 6 read from the install, offered from that
  seq and not before; an unknown id at the parent's availability).

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** A mod plane enters the campaign hangar at `start`, with its parent (default) or at the
named mission.

**Evidence (confidence: lead-only).** <TODO: where the campaign's stock unlock list lives>

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D33 ☑ Bot seats may fly mod planes

**Landed.** A bot seat of a field flown on one machine may fly a mod; a networked field keeps its
bots on stock airframes until D34 carries a mod id on the wire.

- **`--vs-bot` ordering.** `SessionSpec.ParseBots` no longer asks the registry, so the spec stays
  pure and keeps the plane as written. `BotSeats.Named(plane, airframes, mods)` checks it at
  seating, after `Launcher.BindModPlanes` registered the mods: an unknown node is Random (`bot:
  '<node>' is no airframe node, the bot flies a Random one`, the warning the parse used to give), a
  mod with `mods` false flies its parent (`bot: plane mod '<id>' flies as its parent <parent> in a
  networked field`). `--vs --vs-bot=avenger_e1c --mods=Z:\CSVM\Mods` now seats the Avenger.
- **The draw.** `BotSeats.Resolve(..., bool mods = false, AirframeRegistry? airframes = null)` and
  `DrawPair(..., AirframeRegistry? mods)`: with mods, each registered mod adds a copy of its parent's
  militia/airframe pairs with the mod's node. A named mod therefore draws a militia that flies its
  parent, which paints it (Medusa's swatches on the Avenger's tint mask in the probe) and gives it an
  accent. `AirframeAdmitted` judges a mod by its parent's stock id (`StockNodeOf`).
- **Random rule.** Random draws over stock and mod pairs alike when mods are loaded, at equal odds
  per pair, so a mod is drawn as often as its parent. Off the wire only.
- **Where mods are seated.** `SeatFields.LocalVersusField` (a local `--vs`/`--stunt` with bots, the
  join board's Dogfight and Instant Action race) passes `mods: true`. `CliHostField` (`--net-host`)
  and `VersusLaunchField` (the network lobby) pass none, so a named mod flies its parent and Random
  draws stock. A `--net-host` probe that no guest joined flew alone on the local field, mods
  included, which is the existing no-wire fallback.
- **Join board.** `DogfightBot.ModAirframe` holds a mod's id beside its parent's stock id in
  `Airframe` (C25's pair). `DogfightBots.SetAirframe(id, airframe, mod)`, Random and
  `ResetRefused` clear it, `LaunchEntries` emits the mod id. `OriginalBotPanel`'s plane list is
  Random, the eleven stock airframes, then each registered mod as `Stock <name>`; a mod row is
  admitted by its parent, and the editor shows the mod's name over the parent's icon and ratings.
  `OriginalLocalLobby`'s player list names a mod bot's plane by the mod's name. The network lobby's
  bot list (`OriginalLobbyScreen`, `DogfightLobby.SetBotAirframe`) stays stock.
- **How a mod bot flies.** Unchanged code: `HumanFlightAdapter` builds the seat by node (the mod's
  graft), takes `StatsFor` = `mod.Stats(zrdr)` (a bot seat's hull is a person's, as for a stock
  bot), and `MilitiaFits.DefFor(militia, modId)` is null, so `ArmsFor` picks the stock branch, which
  binds `mod.StockLoadout`. The parent's AI def (`mod.Stats(forAi: true)`) is what `AiStatsFor`
  returns for the mod, used by `--ai=<id>` and by a militia def a mod never has.
- **Unchanged by decision.** Scripted campaign enemies and Instant Action's waves stay as authored.
- Docs: `docs/formats/mod-planes.md` "Bot seats", `docs/cli.md` (`--vs-bots`, `--vs-bot`), the
  `BotSeats`, `SeatFields`, `SessionSpec`, `DogfightBots`, `OriginalBotPanel` and
  `OriginalLocalLobby` entries.
- Coverage: `BotSeatsTests.ALocalFieldSeatsAPlaneModOnItsParentsMilitias` (named on the parent's
  militias, Random within 0.8-1.25 of the parent's count over 7000 draws, none on the wire, the
  wire's named mod as the parent), `BotSeatsTests.ANamedPlaneIsCheckedAtSeating`,
  `VsBotFlagTests.APlaneIsKeptAsWrittenForTheSeating`,
  `JoinBoardBotTests.ARowOnAPlaneModLaunchesOnItsIdAndIsJudgedByItsParent`, and the `mod-plane-bot`
  suite (a Brigand export as a scratch mod with `fd_speed` 123.5 and `"turrets": []`: the seats
  above through a private `AirframeRegistry`, the build from the file, the AI flavour on the
  Brigand's AI def with the override and no turret mount, no militia def naming the mod, the fit's 4
  hardpoints and forward guns). Probe screenshots: `.scratchKeep/mod-planes/d33/bot-avenger.png`,
  `bot-avenger-1440p.png` and `bot-avenger-crop.png` (`--vs --chapter=C1 --plane=player_fury
  --vs-bot=avenger_e1c:skill=ace --mods=Z:\CSVM\Mods --view=cockpit --look=1,0 --frames=20`, the
  pane placed by `--pos`/`--lookat` so the bot spawns 60 m abreast): the Avenger in Medusa's colours
  beside the Fury, log `local: bot seat 1 'Black Swan' flies avenger_e1c for Medusa (accent 8) at ace`
  and `flight stats [pbrigand]: fd_speed=111.76`.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

**Goal.** Seats that pick or randomise a plane (vs-bot, races, multiplayer bot fill) may use mod
planes with the parent's AI def; scripted mission enemies are unchanged.

**Evidence (confidence: lead-only).** `BotSeats.cs:225`, `AiAirframePool.cs` and
`PlaneStats.LoadForAi` work from the stock defs; `SessionSpec.cs:3149` coerces non-stock planes
(per the exploration agent).

**Approach.** <TODO>

**Model recommendation.** <TODO: tier not settled>

**Verify.** <TODO>

**⚠ Traps.** <TODO>

## D34 ☑ Multiplayer: string airframe id and a matching mod set

**Landed.** A seat's airframe crosses as its node, and a host's plane mods cross as a set every guest
must hold. Rules and wire: `docs/org/multiplayer-messages.md` "Plane mods", `docs/formats/mod-planes.md`
"Multiplayer".

- **Seat roster `0x27`.** `NetSeatEntry.Plane` is a string. The byte after the flags, which carried the
  index into `StockAirframes.Nodes`, is reserved, and a 32-byte UTF-8 field after the callsign carries
  the node, a stock one or a mod id. The entry is 78 bytes (was 46), and a reader refuses any other
  width. `NetSession` no longer takes an airframe list (`NetAirframes`, the `airframes:` arguments are
  gone) and copies `PlaneNode` both ways. `ModPlanes` skips a mod whose id is longer than 31 bytes of
  UTF-8, the field's width.
- **Protocol version.** The peer protocol's version is the build's MAJOR.MINOR (`BuildVersionMessage`,
  frozen layout), which the lobby compares on connect. `project.godot` stays 0.8.0: the version is set
  by the release, and this layout must ship in a minor release (0.9.0), the rule the roster's width
  already carried. Inside one MAJOR.MINOR the width refuses on its own: a 46-byte build and this one
  each read the other's roster as malformed, so a `--net-join` across them fails its join (`no
  handshake or roster`) instead of flying different planes.
- **The mod set.** `Net/NetModSet.cs`: `NetMod(Id, Hash, Parent)`, `NetModSet` (the session's airframe
  rows, `NodeOf`, `StockOf`, `RowOf`, `MissingFrom`) and `ModSetMessage` `0x6E` (count, three reserved,
  then per mod its parent byte, its id in 32 bytes and its 32-byte hash; at most 64 mods).
  `ModPlanes.Wire` is a machine's own set. A host's `NetLobby` (`mods:`, from `NetPlayFeature.OpenHost`)
  sends it on connect after the version and the advert, and `NetSession.Host(..., mods)` sends it at
  each join before the handshake, for the command line's join that stands no lobby. Only a host with
  a mod sends it, so a stock session's wire and payload counts are unchanged. A guest's lobby keeps it
  as `Mods`; a host's lobby never takes a peer's.
- **The rule: refuse whenever the guest lacks a mod of the host's set or holds it under another hash.**
  Every host mod counts, not only those a seat flies, because a lobby's picks, bots and Random draws
  can put any host mod on a seat until the launch, and a per-seat check would have to run again at each
  change and could throw a seated guest out mid-lobby. The menu guest's door fails with
  `CoopDoorText.ModsMissing` ("Install the host's plane mods: <ids>"); a `--net-join` guest's
  `NetSession.RefusedMods` stops `SessionNet.AwaitJoin` at once with `net: the host flies plane mods
  this machine lacks or holds in another version, the join failed: <ids>`. A guest's own mods the host
  lacks refuse nothing and are no row of the session: the lobby's plane lists hold only the host's
  rows, its custom plane list leaves out a build on a mod the session lacks, and
  `CustomPlaneWire.Build` sends such a build as its parent's.
- **Lobby rows.** Every lobby airframe byte is a row of the host's set: 0-10 the stock ids, 11 + i the
  host's i-th mod in name order (the pick `0x51`, the Dogfight roster `0x54`, a bot), so no lobby
  message changed width. `DogfightLobby.Mods`; `Pick`, `PickSeat` and a guest pick's read bound to
  `Mods.Rows`; `PickCustom` takes the build's mod row; the plane rules judge `Mods.StockOf(row)`;
  `SetBotAirframe(row)` stores the parent and the mod id in `DogfightBot` (D33's pair) and `BotRow`
  lists it. `OriginalLobbyScreen`'s plane and bot dropdowns list every row, a mod row worded
  `Stock <name>` (`PlaneWord`, `PlaneText`, `ModName`); its icon, ratings and caption column stays the
  parent's, as C26 left the network lobby's art.
- **Custom builds.** `NetPlaneBuild.ModRow` is byte 23 of the 26 build bytes (two spare remain), 0 for a
  stock build, so a stock build's bytes are unchanged; `Airframe` stays the parent, so every reader of
  it (rules, stat table, engine rows) reads the parent. `CustomPlaneWire.Build(def, mods)` and
  `Def(build, mods)` map the row to the id and set `Airframe` from the set's parent.
- **The formerly int-only sites.** `NetFlight.cs:347` (`NetAirframes = StockAirframes.Nodes`) is gone:
  `LauncherContext.NetMods`, null reading `ModPlanes.Current.Wire`, feeds `SessionNet`'s host and guest.
  `SeatFields.cs:76` (a co-op guest's seat) resolves the hangar word's row, its build's `ModRow` when
  set, through the lobby's set; `SeatFields.cs:159` (a lobby pick) resolves the pick's row.
  `InspectionLabs.cs:396` (the lighting lab's plane list) lists `AirframeRegistry.Shared.All`, mods
  included. `CustomPlaneWire.Of`/`Build` names the mod row (`OriginalLobbyScreen`, `SeatFields.SeatBuildsFor`,
  `CampaignFeature.HangarBuildOf` with `ModSet.Wire`), and `SeatBuildFor` reads it back with the door's
  `Mods`. `DogfightLobby` and `NetPlaneMessages` as above. `OriginalLobbyScreen.StockDef` takes a mod
  row's own default through `ModPlanes.StockFitFor`, and its launch seats `Mods.NodeOf(row)`. D31's co-op
  host launch: `CampaignFeature.BuildExit` lost `networked`, so the host flies its mod id. D31's co-op
  hangar words: the word's airframe stays the parent (a guest's copy indexes art by it) and its build
  names the mod's row, so the host seats a guest on a hangar mod plane as the mod. D33's networked
  fields: `BotSeats.Resolve`, `BotSeats.Named` and `SeatFields.ResolveBots` lost `mods`, so
  `CliHostField` and `VersusLaunchField` seat named mods and Random draws them. D33's network lobby bot
  list lists the host's mod rows. `--net-host --plane=<mod>` needed no code beyond the roster: the host's
  seats, and each command-line guest's (it flies the host's plane), carry the mod id.
- **Still the parent.** The co-op wingman (`CoopWingmanMessage` keeps a parent byte, so the host's and
  every guest's wingman stay one airframe; the plan allowed it), the mission records and per-airframe
  kill tallies, the network lobby's art column, a co-op guest's own pages naming a hangar mod plane
  (its `CampaignFeature` holds no session set, so `GuestBuildOf` reads the build without one), and a
  host's mods past the 64th, which its guests are not offered.
- **Master server.** The game listing carries the version and the password mark but no content
  requirement, so it is unchanged; a mismatch shows at the join.
- Coverage: `NetMessagesTests` (`SeatRosterCarriesTheAirframeAsItsNode`: two stock nodes, a mod id and a
  31-byte id round trip, a 46-byte entry refused; `ModSetRoundTripsAndNamesTheSessionsAirframeRows`;
  `AGuestMissingAHostModOrHoldingAnotherHashIsNamedEachMissingMod`), `NetSessionTests.A_guest_joins_a_mod_host_only_holding_its_mods`
  (a matched guest joins on a mod seat, a stale one never joins and names the mod, a stock host still
  sends two payloads), `NetPlaneMessagesTests.ABuildOnAPlaneModCrossesAsItsParentAndTheModsRow`,
  `DogfightLobbyTests.AHostsPlaneModIsALobbyRowTheLaunchSeatsByItsId` (a guest's pick of row 11 and a
  bot on row 11 reach the host's launch roster as the mod id; a stock host offers 11 rows), the updated
  `BotSeatsTests.AFieldSeatsAPlaneModOnItsParentsMilitias` and `ModPlaneCampaignTests.AnInstalledModFliesAsItsOwnNode`,
  and the `net-mod-plane` suite (loopback host and two guests over a scratch mod: the matched guest
  joins, both ends seat the guest on the mod and build it from its `plane.glb`; a guest holding the mod
  with another `plane.json` never joins and names it; a guest's lobby reads the host's set on connect
  and the door's notice names the mod). The `mod-plane-bot` suite now checks a field draws the mod and a
  registry without it does not.
- Probe, two processes on the hidden desktop over the local Avenger: `--net-host=127.0.0.1:47811 --vs
  --chapter=C1 --plane=avenger_e1c --mods=Z:\CSVM\Mods` and `--net-join=127.0.0.1:47811 --vs
  --chapter=C1 --plane=player_fury --mods=Z:\CSVM\Mods`. The guest logs `net: joined as seat 1 of 2` and
  `HD models: 31 mesh instance(s) of avenger_e1c drawn from its mod's plane.glb` for both seats, and its
  chase view shows its own seat as the Avenger in Fortune Hunter's colours
  (`Z:\CSVM\.scratchKeep\mod-planes\d34\guest.png`, the host's `host.png`, logs beside them). The same
  guest with `--mods=` an empty folder logs `net: the host flies plane mods this machine lacks or holds
  in another version, the join failed: avenger_e1c` (`guest-refused.err.txt`). It then idles at the
  load screen until killed, as any failed command-line join already did (no handshake from the host);
  the menu's join returns to the games list instead.

**Verified.** <pending orchestrator run>

**Original approach (kept for reference).**

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

**Landed.** The Avenger is the local mod `<dataRoot>/Mods/Planes/avenger_e1c/` (gitignored by
`/Mods/`): `plane.glb` exported from Blender with the root named after the id, the tint mask and
detail map, and a `plane.json` of `kind_of` `player_brigand`, `cockpit` `player_warhawk`,
overrides `fd_speed` 111.76 (250 mph, Decision 31), `nose_armor` 37.5, `tail_armor` 22.5,
`leftwing_armor` and `rightwing_armor` 22.5 (Decision 32), `"turrets": []`, `hangar_capacity`
13500 (the default guns and all eight pylons weigh 12,565 lb, the 105 armour 420 lb more), the
loadout `[60, 30, 30, 30]` (2 × .60 and 6 × .30, Decision 30) and the `tint` block. It loads with
no skip and no override warning. Headless evidence per surface: free flight, cockpit and viewer
(`.scratchKeep/mod-planes/e41`), liveries (`c22`), the hangar builder and menu art (`c25`, `c26`),
damage and crash (`c23`), the campaign (`d31`), a bot seat (`d33`), a two-process network session
(`d34`). The A7 preview pack is no longer needed.

**Verified.** <pending: the user at the controls (surfaces deflecting, both props, handling, the
Warhawk cockpit, liveries, the hangar, a crash)>

**Original approach (kept for reference).**

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
