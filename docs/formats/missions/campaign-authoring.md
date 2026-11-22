Part of: [Mission objectives and stunt zones](../missions.md).

# Campaign mission bundles

A campaign mission is not one file. It is assembled from shared, chapter, and mission reader
scopes, a chapter world, two compiled animation archives, and one per-mission engine setup script.
This page maps that bundle, its cross-file joins, and the whole-scope rules an external mod must
follow. Detailed field references remain on the linked family pages. The census covers the 24
story missions in the English retail data.

## Contents

- [At a glance](#at-a-glance)
- [Conceptual model](#conceptual-model)
- [The campaign mission reader census](#the-campaign-mission-reader-census)
- [Bundle reference](#bundle-reference)
- [Cross-file joins](#cross-file-joins)
- [Reader rules and edge cases](#reader-rules-and-edge-cases)
- [Evidence & limits](#evidence--limits)

## At a glance

| Question | Answer |
|---|---|
| What identifies a mission? | A zero-based campaign `seq` resolves through shared `cm_sequence.zrd` to a chapter and mission address such as `C3/M01`. |
| What is the mission reader scope? | The complete `ZBD/<chapter>/<mission>/zrdr.zbd`, containing 11 to 22 readers in the shipped campaign. |
| What else does a mission load? | The chapter `gamez.zbd`, textures, chapter readers, `cam_anim.zbd`, mission `mis_anim.zbd`, shared readers, messages, and the mission's `support\<chapter>\<mission>.gw` setup script. |
| How does a mod replace mission readers? | A local `<mod>\<chapter>\<mission>\zrdr.zip` or unpacked `zrdr\` replaces the complete scope; missing readers do not fall back to retail. |

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
| `location.zrd` | 24/24 | Named cockpit-map points with positions and rotations; eight missions (C4/M01-M05 and C5/M02-M04) ship `[null]`, a root containing one empty nested list | [Briefing](../briefing.md) |
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

Absence and an authored empty value are different. A file can be absent from the archive, load as
an empty root, or contain an ordinary list that includes empty nested lists. CSVM does not retain a
separate null-root representation: `ZrdrZbd.ReadReader` maps an empty binary root to an empty list,
and `Zrdr.ReadScope` maps a JSON `null` root to the same value before `ZrdrSet.Load` exposes it.
Raw mech3ax JSON can show that source distinction, but a CSVM mod round trip cannot preserve it.

Every one of the 24 addresses also resolves to a chapter `gamez.zbd`, chapter `cam_anim.zbd`,
mission `mis_anim.zbd`, and per-mission `.gw` setup script in the checked install. The chapter
companions are shared by several story missions.

## Bundle reference

### Shared inputs

| Source | Mission use | Scope and override rule |
|---|---|---|
| `ZBD/zrdr.zbd` | `cm_sequence.zrd`, vehicle and weapon definitions, sounds, shared animation definitions, and shared briefing data | Shared scope. Replacing it affects the whole game and requires a complete 221-reader output in this install. |
| `strings.dll` | Resolves the `MSG_*` keys used by objectives, targets, rosters, and briefings | Separate `messages.json` mod output, not part of a mission reader scope. |
| `ZBD/interp.zbd` | Supplies `support\<chapter>\<mission>.gw`, which activates, removes, translates, and rotates chapter-world entities | Separate whole-archive output. A modded `interp.json` replaces all scripts, not one mission script. |

### Chapter inputs

| Source | Mission use | Scope and override rule |
|---|---|---|
| `<chapter>/gamez.zbd` | Static world, named nodes, danger-zone geometry, scripted paths, target sites, and mission entity hosts | Chapter-wide source shared by every mission in the chapter. |
| `<chapter>/rtexture<N>.zbd` or `texture.zbd` | World textures selected by the chapter's highest available tier | Separate chapter texture output, selected by tier. |
| `<chapter>/zrdr.zbd` | AI net index and paths, chapter animation readers, target fallback, clutter templates, and weather-volume data | Chapter-wide scope. A replacement must retain the complete chapter reader set. |
| `<chapter>/cam_anim.zbd` | Compiled chapter animation definitions and SI scripts | Separate compiled archive; compiled definitions outrank reader definitions on collision. |

### Mission inputs

| Source | Mission use | Scope and override rule |
|---|---|---|
| `<chapter>/<mission>/zrdr.zbd` | The reader set in the census above | Complete override unit. A mod output retains the whole scope even when one reader changes. |
| `<chapter>/<mission>/mis_anim.zbd` | Compiled mission animation definitions and SI scripts | Separate compiled archive, distinct from the `mis_anim.zrd` reader inside the reader scope. |

## Cross-file joins

Many broken mission overrides are valid JSON with an unresolved name. These joins determine what a
reader value addresses:

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

This makes a single-file override unsafe. A folder containing only an edited
`objectives.zrd.json` also removes `aiv.zrd`, `weather.zrd`, `startanims.zrd`, and every other
mission reader from CSVM's view. A generated override must begin from a complete scope and write
every retained entry.

### Preserve the raw list tree

Any lossless reader or mod tool must retain the reader's ordered tree of strings, `float`s, nested
lists, and nulls:

- Do not convert the document to JSON objects. The format is arrays, and duplicate keys and bare
  flags are meaningful.
- Do not use `ZrdrDict` as the backing store. It is a convenient consumer view that collapses
  duplicate keys and does not preserve key order.
- Preserve list order, unknown directives, unsupported keys, stray values, nulls, and file names.
  Any typed view must point back to exact locations in the raw tree.
- Both integer and float binary tags enter CSVM's model as `float`. A JSON override can preserve
  the runtime value and the stored float bits, but it cannot reconstruct which binary number tag
  the retail reader used.
- An empty nested binary list becomes null. Replacing that nested null with an empty list changes
  the extracted representation even where a consumer gives both the same meaning.
- An empty reader root is different: both a count-1 binary root and a JSON `null` root load as an
  empty root list, so `ZrdrSet` exposes no distinction for a no-edit round trip to preserve.

The binary reader keeps archive table order. A generated ZIP can retain that order explicitly;
directory enumeration does not promise it. A generated `zrdr.zip` should therefore keep the source
entry order unless folder-mode gains an ordering manifest. The ZIP entries still use the unzbd
names, such as `objectives.zrd.json`. An entry named only `objectives.json` is ignored.

### Keep scope and fallback decisions explicit

Do not merge shared, chapter, and mission files into one dictionary. The same stem can have a
different role in each scope, and most consumers do not fall back. Target labels are an explicit
exception: mission `targets.zrd` wins as a whole, otherwise the chapter's file is used.

An existing mod output is also a complete scope. Silently filling absent readers from retail
changes its meaning; intentional absence must remain absent.

### Compiled animations mask reader edits

`AnimProgram` loads `cam_anim.zbd` and `mis_anim.zbd` first. A compiled definition wins when a
reader definition has the same identity, and the compiled mission manifest filters reader-only
definitions that are not part of that mission. Editing a mission's animation reader can therefore
produce valid output with no visible change.

`startanims.zrd` selects and orders existing animation names. JSON changes to `mis_anim.zrd`,
`zepstate.zrd`, or mission-specific animation definitions can still be masked by the compiled
archives, so a successful parse does not establish that the changed definition runs.

## Evidence & limits

The archive census comes from `CampaignSequence.Load` over the shared reader followed by
`ZrdrSet.Load` over all 24 resolved mission addresses. The same walk checks the companion
`gamez.zbd`, `cam_anim.zbd`, `mis_anim.zbd`, and `.gw` setup script. It records entry names and
presence only, not retail payloads.

`ZrdrZbd.cs` establishes the binary value model and table order. `Zrdr.cs` establishes the
accepted `*.zrd.json` mod names. `GameData.cs` and `ModdingFolderTests.cs` establish whole-archive
replacement; the test's one-file mission override has its edited `pickups.zrd` and no
`objectives.zrd`. `AnimProgram.cs` establishes compiled-first animation precedence.

The census covers one English retail data set.
