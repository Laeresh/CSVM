# Root

The three files at the root of `CSVM/src` (`SessionSpec.cs`, `SessionPaths.cs`, `Pads.cs`) plus the cross-module conventions and the enhanced graphics mode notes, all of which govern behavior spanning every namespace.

One `## src/...` entry per module, body at most 8 lines, 12 for the highest-traffic modules.

Traps do not live here; the rule is in `docs/architecture.md`.

## Cross-module conventions

⚠ **Labs are opt-in.** Every inspection lab (SelectionService, NodeLab, WorldDamageLab, LiveryLab,
  NodeLabels, MeshLab, …) builds no UI and changes no pixel until its key or `--debug-*` flag is
  used — the 11 golden screenshots stay byte-identical.
⚠ **Every formatted number uses `CultureInfo.InvariantCulture`.** A German-locale machine renders
  `0,5` and corrupts logs, reports and parsed round-trips (Probes/Log/StuntMission precedent).

## Rendering: the enhanced graphics mode (a documented divergence)

Enhanced graphics mode is an opt-in [Divergence] in the BL-555 sense: a deliberate, recorded
departure from the original, never presented as the original's own behaviour. It is gated by the
saved `graphicsMode` option both Options screens write, under it the `graphics.mode` config key
(`original`/`enhanced`, default `original`, `src/Utils/GraphicsMode.cs`), and over both an
explicit `--graphics=` flag (`docs/cli.md`) that survives `--det`, so a golden or a deterministic
capture can ask for the enhanced path on purpose while every ordinary `--det` run, including the
full golden sweep, stays on `original`.

Original mode's rendering does not move under this mode: enhanced mode replaces the fullbright
world with real Godot lighting. The world shades under decoded, pre-negated vertex normals as a
matte material (`SceneBuilder.cs`'s lit-world arm); a `DirectionalLight3D` and the Environment's
ambient are driven from the mission's authored `SUNLIGHT_DIFFUSE`/`SUNLIGHT_AMBIENT` values and
colours instead of the launcher's hardcoded numbers (`WeatherRig.cs`); the sun casts PSSM shadow
maps, with each zone's authored fog pushed out 2x and the shadow's max distance following that
pushed far so shadows never end in clear air; `LIGHT_STATE` point lights are mirrored onto real
`OmniLight3D` nodes that light the world and the aircraft, not only a fullbright spill texture
(`WorldLights.cs`); the light-source class of glow-arm sprites (flares, beacons, signal lamps)
scales its colour above 1.0 to feed an Environment glow pass, and an AgX tonemap rolls the
resulting HDR scene off instead of clipping it; SSAO adds contact shading in ambient light, and
SSR reflects the shoreline off water surfaces the engine already classifies as `"water"`
(`Launcher.cs`'s `SetupLighting`); the sky those water surfaces reflect where SSR finds nothing is
the flown zone's own `FOG_COLOR`, painted flat over Godot's procedural placeholder
(`Launcher.cs`'s `UseMissionSky`, `WeatherRig.WriteSkyColor`). ⚠ That needs a `Sky` resource: a
background COLOUR gives the specular no radiance at all, which reads as a plausible improvement
because it removes the placeholder rather than replacing it. The cockpit interior pass and every splitscreen pane pick up
the same settings and the same per-zone updates, since both duplicate or share the session's own
sun and Environment (`CockpitOverlay.cs`, `SplitScreen.cs`).

**Original mode's byte identity is proven, not assumed.** Every item that touched a shader-key
generator dumped every reachable key's generated `Shader.Code` before and after the change and
compared file size and SHA-256 (A2, B11, C21, C24); every item also ran the full 18-shot golden
sweep and reported zero movers. Neither instrument alone would catch everything: the shader dump
catches a text change the goldens' camera poses never frame, and the goldens catch a runtime
effect (a light, a tonemap curve) the shader text cannot show.

**Recorded disproofs.** Two hoped-for identifications did not hold up. C5's lit windows are not a
separable surface: they are bright texels inside `lighting: true` wall textures, so no per-surface
rule can hold them at their authored brightness without also relighting the wall around them
(C21). And the model-level `lighting: false` bit does not identify "the emissive population": its
general, non-glow-sprite population is about half non-luminous (a cloud deck, both skydome
textures, baked ground-shadow decals, tree and bush cards) mixed in with the genuinely self-lit
signs and lamps, so scaling that whole population would bloom a cloud deck and a skydome (C21,
census in `docs/org/vertexLighting.md`).

**Open judgements.** The energy mapping from authored SUNLIGHT units to Godot light energies, the
2x fog-range push and the shadow distance that follows it, the night key read off `FOG_COLOR`
luminance with its 0.25 separator and its 0.6 / 0.15 energy cap (a proxy the original never uses:
it lights from SUNLIGHT and darkens from FOG_COLOR independently), and SSR's hard mirror on
wave-less water planes are all TUNE: judged at the controls against captures, not derived from a
decoded rule. `PLAN-enhanced-graphics`'s Open judgements list is where the user's at-the-controls
pass tracks them.

Both Options screens expose the mode as a two-way row saved into the menu plan's options store
(`docs/menu-presentations.md`); every reader in this codebase consults the resolved
`GraphicsMode.Enhanced` boolean only, so neither the store nor the screens reach any of them. The
saved word changes on Apply and the world takes it on the next start, since the shader memos and
the Environment are built from the value resolved once at launch, which is why each screen's
description line says so.

## src/Pads.cs
Single source of truth for gamepads — every reader goes through it, never `Input.GetConnectedJoypads()`.
Owns the phantom policy (span every pad, never `pads[0]`), `Disabled` (`--no-pads`), the focus gate.
`Connected` (the roster) and `For` (the input gate) answer different questions; see the class
remarks. `AssignPads` is the launch-time roster split, its leftover-pool rule for P1 covered by
the pure, engine-free overload in `PadsTests.cs`.

`LogPads` records the roster and the per-seat result through the run log, because the binding is
settled once at build and cannot be reconstructed afterwards. The roster line carries POSITION and
id separately (`[1] pad 2 "..."`), and that is what it is for: `AssignPads` seats P2-P4 by roster
position, so a device occupying a position without producing input takes that seat and leaves the
real pad in P1's leftover pool, flying P1's plane beside P1's own. The per-seat lines cannot show
that device, since it is the one no seat reports; a position that does not match its id is the
tell. A menu-driven launch binds from the join flow instead, which claims by device id and so
cannot seat a phantom (`Launcher.BindMenuPads`, the path every launchscreen and campaign-cabin
launch takes).

## src/SessionPaths.cs
Static resolver for the extracted-data paths (`ChapterTextures`/`ChapterGamez`/`ChapterZrdr`/
`MissionZrdr`) under a data root, plus `PreferUnzipped` (an unpacked sibling dir beats its `.zip`).
The `rtextureN` tier decode is on `docs/tooling.md`; the `--gamez=`/`--textures=` override policy
stays in `GameSession`, not here.

## src/SessionSpec.cs
Everything the command line settles about a session, as one immutable record: `Parse(args)` parses
**and resolves**; the pure arg parsers (`ParseVec3`, `ParsePlanes`, `ParseHold`, …) are public so
they are testable. `SessionMode` is closed — Menu/Fly/Viewer/Freecam/AnimLab — with modifiers
(`Stunt`, `Versus`) and `SessionProbe`; per-rule coverage lives in `CSVM.Tests/SessionSpec*Tests`.
`Versus` (`--vs`, `--vs-kills=`, `--vs-time=`) beats `Stunt` by fixed precedence, not last-wins.
`Resolve`'s step order and the purity contract (DET-9) are on the class and method themselves;
`FromMenu`'s no-re-resolve/Dogfight-lock/`iaDef` rules are on `FromMenu` and `IaDef`.
`FromCampaign` is the cabin's counterpart, taking the joined player count and one aircraft node per
player, entry 0 the SEATED pilot's; `LaunchMenu.CampaignLaunch` is where that list is built, one
entry per joined human read off `CampaignFlightField.Plane`, so a guest flies its own C22 pick
rather than falling back to entry 0. A campaign session is co-op with no flag: `Resolve`
sets `Coop` for any `--campaign=` that is not also `--vs`, and `FromCampaign` sets it directly
because that factory deliberately does not re-resolve. `--coop` on a campaign command line is
therefore ignored in silence, which is the one ignored flag here that prints nothing, because it
asks for exactly what the mode already gives. A `--campaign= --players=N` command line needs no
new gate: a campaign resolves to `SessionMode.Fly`, so the existing "splitscreen is a flight mode"
clamp passes it through, and the multi-entry `--plane=` count inference still loses to an
explicit `--players=`.

