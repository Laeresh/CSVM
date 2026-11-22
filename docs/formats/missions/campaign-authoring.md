Part of: [Mission objectives and stunt zones](../missions.md).

# Campaign mission bundles and Godot authoring

A campaign mission is not one file. It is assembled from shared, chapter, and mission reader
scopes, a chapter world, two compiled animation archives, and one per-mission engine setup script.
This page maps that bundle and defines a safe authoring boundary for a Godot editor. Detailed field
references remain on the linked family pages.

The format facts cover the 24 story missions in the English retail data. The Godot workflow is a
design for a future editor, not a feature present in the project.

## Contents

- [At a glance](#at-a-glance)
- [Conceptual model](#conceptual-model)
- [The campaign mission reader census](#the-campaign-mission-reader-census)
- [Bundle reference](#bundle-reference)
- [Cross-file joins](#cross-file-joins)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Godot editor workflow](#godot-editor-workflow)
- [Validation](#validation)
- [Evidence & limits](#evidence--limits)

## At a glance

| Question | Answer |
|---|---|
| What identifies a mission? | A zero-based campaign `seq` resolves through shared `cm_sequence.zrd` to a chapter and mission address such as `C3/M01`. |
| What is the editable unit? | The complete mission reader scope, `ZBD/<chapter>/<mission>/zrdr.zbd`. It contains 11 to 22 readers in the shipped campaign. |
| What does a preview also need? | The chapter `gamez.zbd`, textures, chapter readers, `cam_anim.zbd`, mission `mis_anim.zbd`, shared readers, messages, and the mission's `support\<chapter>\<mission>.gw` setup script. |
| Where should an editor save? | A local external modding folder, normally `<mod>\<chapter>\<mission>\zrdr.zip`, with every retained reader stored as `*.zrd.json`. The generated full scope is not a distributable artifact. |
| What should remain read-only first? | The chapter world, shared and chapter reader scopes, setup scripts, and compiled animation archives. They have wider effects or no current writer. |
| Can the retail archive be overwritten? | No. CSVM reads extracted JSON from a modding folder; it has no `zrdr.zbd` writer, and the retail game does not read this override format. |
| Does the project include a Godot editor plugin? | No. The project has no `EditorPlugin`, `[Tool]` script, registered addon, or mission serializer. |

## Conceptual model

The campaign sequence supplies a storage address, not a self-contained mission:

```text
shared zrdr.zbd
  cm_sequence.zrd -> seq -> C3/M01
  vehicle, sound, message-key, briefing, and shared animation definitions

chapter C3
  gamez.zbd + textures              static world and node names
  zrdr.zbd                          AI nets and chapter definitions
  cam_anim.zbd                      compiled chapter animations

mission C3/M01
  zrdr.zbd                          objectives, roster, weather, entities, animation readers
  mis_anim.zbd                      compiled mission animations

interp.zbd
  support\c3\m01.gw                 world activation and placement
```

These inputs keep their scopes. A mission reader does not become a chapter reader because both
are visible during a flight, and a generic "nearest file wins" rule is incorrect. Each consumer
chooses its own scope. For example, objectives come from the mission scope, AI nets come from the
chapter scope, and target labels use the mission's `targets.zrd` first with an explicit chapter
fallback.

The chapter world is a superset. It contains scenery and entities for several modes and missions.
The `.gw` setup script switches and moves the subset used by the selected mission before startup
animations run. Reader records then refer to those world nodes by name. A preview that loads only
the mission readers therefore cannot show the mission accurately.

## The campaign mission reader census

Every story-mission archive contains nine common readers. Optional mission systems and
mission-specific animation definitions raise the archive total to between 11 and 22 readers.

| Reader | Story missions | Role | Detailed reference |
|---|---:|---|---|
| `aiv.zrd` | 24/24 | AI vehicle roster, spawn poses, teams, groups, skills, and generator parameter templates | [AI rosters](../ai-rosters.md) |
| `egen.zrd` | 24/24 | Enemy-generator hosts and launch cycles; an empty mission still carries the reader | [Enemy generators](../mission-entities/enemy-generators.md) |
| `location.zrd` | 24/24 | Named cockpit-map points with positions and rotations; one shipped root is null | [Briefing](../briefing.md) |
| `map.zrd` | 24/24 | In-flight cockpit-map transform, animations, and player marker | [Briefing](../briefing.md) |
| `mis_anim.zrd` | 24/24 | Index of animation-definition reader files visible to the mission; not the compiled archive of the same stem | [Animation definitions](../anim-definitions.md) |
| `net.zrd` | 24/24 | Multiplayer spawn table carried by the story folders; it does not place a campaign player | [Network spawns](../net-spawns.md) |
| `objectives.zrd` | 24/24 | Player start and the ordered objective state machines | [Objectives](../objectives.md), [spawns](../spawns.md) |
| `startanims.zrd` | 24/24 | Ordered `NEW_GAME_START` animation names | [Animation definitions](../anim-definitions.md) |
| `weather.zrd` | 24/24 | Fog, sunlight, cloud, wind, and precipitation parameters | [Weather](../weather.md) |
| `targets.zrd` | 23/24 | Initial target flags and display-string keys. `C1C/M01` takes the chapter reader instead. | [Target display keys](../missions.md#target-display-keys) |
| `dzones.zrd` | 22/24 | Per-mission danger-zone disables, objective numbers, and snapshot policy | [Zone overrides](../missions.md#zone-overrides) |
| `zeppelins.zrd` | 22/24 | Mission zeppelin instances, poses, damage zones, teams, and weapons | [Mission entities](../mission-entities.md) |
| `zepstate.zrd` | 8/24 | Zeppelin animation definitions and state wiring | [Animation definitions](../anim-definitions.md) |
| `pickups.zrd` | 4/24 | Proximity sensors used by mission cutscenes | [Cutscenes](../anim-definitions/cutscenes.md) |
| Other named readers | varies | Mission-local animation definitions such as `ladder.zrd`, `objcomplete.zrd`, and cutscene files named by `mis_anim.zrd` | [Animation definitions](../anim-definitions.md) |

Absence and an authored empty value are different. A file can be absent from the archive, present
with a null root, present with an empty root, or contain an ordinary list. The editor must retain
that distinction instead of replacing every empty-looking case with one representation.

Every one of the 24 addresses also resolves to a chapter `gamez.zbd`, chapter `cam_anim.zbd`,
mission `mis_anim.zbd`, and per-mission `.gw` setup script in the checked install. The chapter
companions are shared by several story missions.

## Bundle reference

### Shared inputs

| Source | Mission use | Authoring boundary |
|---|---|---|
| `ZBD/zrdr.zbd` | `cm_sequence.zrd`, vehicle and weapon definitions, sounds, shared animation definitions, and shared briefing data | Read-only for a mission editor. Replacing it affects the whole game and requires a complete 221-reader scope in this install. |
| `strings.dll` | Resolves the `MSG_*` keys used by objectives, targets, rosters, and briefings | Read-only lookup. A text edit belongs to the separate `messages.json` mod output. |
| `ZBD/interp.zbd` | Supplies `support\<chapter>\<mission>.gw`, which activates, removes, translates, and rotates chapter-world entities | Read-only first. A modded `interp.json` replaces all scripts, not one mission script. |

### Chapter inputs

| Source | Mission use | Authoring boundary |
|---|---|---|
| `<chapter>/gamez.zbd` | Static world, named nodes, danger-zone geometry, scripted paths, target sites, and mission entity hosts | Preview and validation source. Editing it changes every mission on the chapter and has no current writer. |
| `<chapter>/rtexture<N>.zbd` or `texture.zbd` | World textures selected by the chapter's highest available tier | Preview source. |
| `<chapter>/zrdr.zbd` | AI net index and paths, chapter animation readers, target fallback, clutter templates, and weather-volume data | Read-only first. It is a separate archive-level override shared by the chapter. |
| `<chapter>/cam_anim.zbd` | Compiled chapter animation definitions and SI scripts | Read-only. Compiled definitions outrank reader definitions on collision. |

### Mission inputs

| Source | Mission use | Authoring boundary |
|---|---|---|
| `<chapter>/<mission>/zrdr.zbd` | The reader set in the census above | The first writable unit. Export the whole scope, even when one reader changes. |
| `<chapter>/<mission>/mis_anim.zbd` | Compiled mission animation definitions and SI scripts | Read-only first. It is distinct from the `mis_anim.zrd` reader inside the reader scope. |

## Cross-file joins

Most authoring errors are valid JSON with an unresolved name. An editor should validate these joins
as the value changes:

| From | Resolves against | Meaning |
|---|---|---|
| `cm_sequence.zrd` `campaign` and `mission` | Fixed chapter-folder mapping and `M01` to `M05` folders | Selects the mission bundle. `seq`, the one-based `CM` ordinal, and the storage address are different identifiers. |
| `objectives.zrd` target directives | `targets.zrd`, roster names, and gamez node paths | Adds, removes, labels, wakes, or tests the named mission object. Objective blocks must remain contiguous from `OBJECTIVE1`. |
| `objectives.zrd` animation and sound directives | The assembled animation program and shared sound groups | Starts animations and mission audio by name. Unknown directive names stay inert. |
| `aiv.zrd` vehicle names and `netids` | Shared vehicle definitions and the chapter `neindex.zrd`/`ne0NNNNN.zrd` graph | Builds and routes each roster member. Position and yaw are authored directly in the roster. |
| `egen.zrd` `vehicle.params` | Designer labels in the same mission's `aiv.zrd` header | Selects the generated aircraft template. |
| `egen.zrd` host, origin, nets, and animations | Gamez nodes, chapter AI nets, and the animation program | Places and drives the generator. A generator record does not carry a free-standing world position. |
| `zeppelins.zrd` node, net, targets, and part names | Gamez nodes, chapter AI nets, and targetable world or roster objects | Places and drives one zeppelin and binds its damage model. |
| `targets.zrd` nodes and message keys | Gamez or roster names and `strings.dll` | Supplies the starting target sets and HUD text. |
| `dzones.zrd` path names | Gamez `dzpathN` geometry and `objectives.zrd` danger-zone conditions | Narrows and numbers the chapter's authored stunt routes. The geometry is not stored in `dzones.zrd`. |
| `startanims.zrd` names | The assembled animation program | Starts existing definitions in authored order. Order matters when definitions write the same state. |
| `mis_anim.zrd` paths | Shared, chapter, and mission animation-definition readers | Selects reader files visible to the mission. The compiled mission archive still decides which compiled definitions win. |
| `weather.zrd` zones | The chapter horizon and fog-volume geometry | Selects the environmental state applied to the world. |

## Reader rules and edge cases

### A mod replaces an archive, not one reader

The modding folder is selected archive by archive. If
`<mod>\<chapter>\<mission>\zrdr.zip` or its unpacked `zrdr\` sibling exists and reads, that output
is the entire mission reader scope. Missing entries do not fall back to the retail
`zrdr.zbd`.

This makes a single-file export unsafe. A folder containing only an edited
`objectives.zrd.json` also removes `aiv.zrd`, `weather.zrd`, `startanims.zrd`, and every other
mission reader from CSVM's view. A mission editor must begin from a complete scope and write every
retained entry.

### Preserve the raw list tree

The canonical editable model should remain the reader's ordered tree of strings, `float`s, nested
lists, and nulls:

- Do not convert the document to JSON objects. The format is arrays, and duplicate keys and bare
  flags are meaningful.
- Do not use `ZrdrDict` as the backing store. It is a convenient consumer view that collapses
  duplicate keys and does not preserve key order.
- Preserve list order, unknown directives, unsupported keys, stray values, nulls, and file names.
  Typed editor panels should point back to exact locations in the raw tree.
- Both integer and float binary tags enter CSVM's model as `float`. A JSON override can preserve
  the runtime value and the stored float bits, but it cannot reconstruct which binary number tag
  the retail reader used.
- An empty nested binary list becomes null. Treating null as an empty `Godot.Collections.Array`
  changes the extracted representation even where a consumer gives both the same meaning.

The binary reader keeps archive table order. A generated ZIP can retain that order explicitly;
directory enumeration does not promise it. A tool should therefore write `zrdr.zip` in the source
entry order unless folder-mode gains an ordering manifest. The ZIP entries still use the unzbd
names, such as `objectives.zrd.json`. An entry named only `objectives.json` is ignored.

### Keep scope and fallback decisions explicit

Do not merge shared, chapter, and mission files into one dictionary. The same stem can have a
different role in each scope, and most consumers do not fall back. Target labels are an explicit
exception: mission `targets.zrd` wins as a whole, otherwise the chapter's file is used.

An existing mod output is also a complete scope. Opening it and silently filling absent readers
from retail would change its meaning. A separate "rebase from retail" action may create a new
complete document, but ordinary open and save must preserve intentional absence.

### Compiled animations mask reader edits

`AnimProgram` loads `cam_anim.zbd` and `mis_anim.zbd` first. A compiled definition wins when a
reader definition has the same identity, and the compiled mission manifest filters reader-only
definitions that are not part of that mission. Editing a mission's animation reader can therefore
produce valid output with no visible change.

The first editor should let `startanims.zrd` select and order existing animation names, but keep
`mis_anim.zrd`, `zepstate.zrd`, and the mission-specific animation-definition readers read-only.
Writable animation authoring needs an explicit compiled-archive strategy rather than a JSON panel
alone.

## Godot editor workflow

### Document model

The Godot scene should be a view of a plain C# mission document, not the source of truth:

| Layer | Responsibility |
|---|---|
| `MissionDocument` | Holds the campaign address, every mission `ZrdrFile` in source order, and read-only references to the shared and chapter inputs. |
| Raw reader model | Keeps each `List<object?>` tree exactly, including unknown and duplicate entries. |
| Typed adapters | Expose objectives, roster blocks, weather, targets, entities, and spawn records while retaining the raw index for every editable field. |
| Preview nodes | Derived `Node3D` handles and overlays rebuilt from typed values. They are not saved as `.tscn` or `.tres` mission data. |
| Diagnostics | Resolves world nodes, messages, AI nets, animation names, and cross-reader references continuously. |

Godot resources are useful for editor UI state, but they should not replace the raw reader model.
A heterogeneous reader tree contains representations that an exported inspector property does not
retain reliably, including duplicate keys, null nested lists, and unknown list shapes.

Undo and redo should apply a typed edit to one raw-tree location, then rebuild the affected preview
and diagnostics. Reconstructing the whole reader from typed fields would discard data the panel
does not understand.

### Import

1. Let the user choose their own install or disc image and an external modding folder. Do not copy
   retail data into the repository.
2. Load shared `cm_sequence.zrd` and select a `CM` ordinal or storage address.
3. Decode the complete retail mission reader scope with `ZrdrSet.Load`. If an existing mod scope is
   opened, treat it as complete because that is how the runtime reads it.
4. Load the chapter world, textures, chapter readers, animation archives, messages, and setup
   script as read-only dependencies.
5. Build typed adapters over deep copies of the raw mission readers. Keep source names and entry
   order beside them.
6. Build the preview only after the `.gw` setup has selected the mission's world state. Startup
   animation playback is optional, but its names and references can still be validated.

Imported or generated retail-derived data remains in memory or under the selected external
modding folder. It does not belong under `CSVM/`, `docs/`, or a committed Godot resource.

A shareable authoring project should contain only the author's changes, not unchanged readers
copied from the retail archive. The editor can apply those changes to a recipient's own install
and materialize the complete runtime scope locally. The full generated `zrdr.zip` is a local build
product and must not be committed or published.

### Editor surfaces

| Surface | Godot view | First-version behavior |
|---|---|---|
| `PLAYER_INIT` | Player camera or aircraft handle | Move and rotate the campaign start; edit throttle and speed in an inspector. Only yaw currently affects the CSVM spawn. |
| `aiv.zrd` | Aircraft handles with team/group labels and optional chapter-net paths | Move roster starts, edit yaw and typed roster fields, and validate vehicle definitions and net ids. |
| `zeppelins.zrd` | Zeppelin handles attached to existing world nodes | Edit position, yaw, pitch, motion limits, teams, targets, and damage records. |
| `egen.zrd` | Host and origin overlays on existing nodes | Edit generator timing and references. The host remains a gamez node rather than a freely placed generator object. |
| `location.zrd` | Named point handles | Preserve and edit the cockpit-map points even while the current flight UI has no authoring panel for them. |
| `dzones.zrd` | Read-only gamez gate ribbons with enable, objective, and snapshot controls | Edit mission overrides, not gate geometry. |
| `targets.zrd` | Node-selection and message-key table | Edit starting flags and labels. Offer an explicit mission-local copy when a mission inherits the chapter table. |
| `objectives.zrd` | Ordered objective list or graph plus directive inspector | Preserve contiguous numbering, raw directive order, unknown directives, and target paths. |
| `weather.zrd` | Environment controls and zone preview | Edit typed atmospheric values while retaining unknown blocks. |
| `startanims.zrd` | Ordered name list | Select and reorder existing animation names. |
| `map.zrd`, `net.zrd`, animation readers | Raw tree or read-only typed summary | Preserve without promising a visible campaign effect. |
| `.gw`, gamez, chapter readers, compiled anims | Read-only dependency browser | Show what controls a value and where a wider-scope edit would belong. |

### Export

1. Serialize the complete mission reader scope as nested JSON arrays. Retain every unedited file
   and every unedited raw subtree.
2. Write a temporary `<mod>\<chapter>\<mission>\zrdr.zip` with entries in source order, then
   replace the destination atomically. Refuse to overwrite a preferred unpacked `zrdr\` sibling
   unless the user explicitly chooses which output owns the scope.
3. Reopen the generated archive with `ZrdrSet.FromExtracted` and compare names, tree shapes,
   values, nulls, order, and float bits against the in-memory document.
4. Run each typed reader used by the edited surfaces and report every failure before offering a
   test launch.
5. Launch CSVM with `--mod-folder=<folder>` and the selected campaign position. Never write into
   the retail install or repack its `zrdr.zbd`.
6. Keep the generated complete archive local. Save or distribute only an author-owned delta that
   another user can apply to their own retail data.

## Validation

A mission editor is ready to save only when all of these checks pass:

- The exported scope reopens and contains every retained reader under a `*.zrd.json` name.
- A no-edit import/export changes no reader tree, file name, file order, null, or float bit.
- `ObjectiveScript`, `AiSkills`, `WeatherState`, `MissionTargets`, `Zeppelins`,
  `EnemyGenerators`, `CampaignDangerZones`, and `AnimProgram` can read the relevant output.
- Every edited node, target, generator host, entity part, AI net, animation, sound group, and
  message key resolves in the same bundle.
- Objective numbering remains contiguous and every displayed objective has a valid identity.
- The external mod scope survives a real campaign load, not only an isolated parser test.

The no-edit round trip is the primary safety test. A typed panel that looks correct but rewrites
unknown data is not a lossless editor.

## Evidence & limits

The archive census comes from `CampaignSequence.Load` over the shared reader followed by
`ZrdrSet.Load` over all 24 resolved mission addresses. The same walk checks the companion
`gamez.zbd`, `cam_anim.zbd`, `mis_anim.zbd`, and `.gw` setup script. It records entry names and
presence only, not retail payloads.

`ZrdrZbd.cs` establishes the binary value model and table order. `Zrdr.cs` establishes the
accepted `*.zrd.json` mod names. `GameData.cs` and `ModdingFolderTests.cs` establish whole-archive
replacement; the test's one-file mission override has its edited `pickups.zrd` and no
`objectives.zrd`. `AnimProgram.cs` establishes compiled-first animation precedence.

The current tree contains readers and typed consumers but no ZRDR serializer or Godot editor
plugin. The proposed workflow targets CSVM's external modding path only. It does not define a
binary writer for the original game, compiled animation authoring, chapter-world editing, or a
shared-scope campaign rewrite. The census covers one English retail data set.
