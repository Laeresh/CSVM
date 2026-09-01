# Enhanced graphics mode — opt-in lit world beside the faithful original

**ACTIVE PLAN** (written 2026-09-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan delivers an opt-in **enhanced graphics mode**: the world receives real Godot lighting
(sun, ambient, shadow maps, omni point lights) driven by the mission's authored SUNLIGHT values,
lighting-exempt surfaces become emissive under a glow pass, and the Environment gains a tonemap and
SSAO. The faithful original look stays the default and stays pixel-identical: every item in this
plan must leave original-mode shader text and all 18 golden hashes untouched, and that zero-mover
property is itself the per-item regression check. The mode is a **[Divergence]** in the BL-555
sense: an explicit opt-in deviation, documented as such, never presented as the original's
behaviour.

Out of scope: menu/UI exposure of the option (the active menu work owns options screens; this plan
ships a config key), the parked 4x texture upscaling (branch `upscaling`, deliberately unmerged),
SDFGI and volumetric fog (cost and look both overshoot the target), and any change to
original-mode rendering. No checklist item is drawn from `backlog.md`; the backlog items this plan
touches (BL-331, BL-332, BL-322, BL-613, BL-325) are cited as context, remain open, and each item
that leans on one carries a TODO to re-verify its state before building on it.

## Milestone goal

- A `graphics.mode` option exists (`original` default, `enhanced` opt-in), read at launch like
  `EffectsLevel`, logged at startup, and impossible for a stray user config to leak into golden
  runs.
- In enhanced mode the world is genuinely lit: decoded vertex normals shade under a
  DirectionalLight3D whose energy, ambient and orientation come from the mission's authored
  SUNLIGHT values; the sun casts shadow maps; LIGHT_STATE point lights are real OmniLight3D nodes
  that also light the aircraft.
- Lighting-exempt surfaces (lit windows, signs) read as emissive sources under Environment glow,
  with a Filmic/AgX tonemap and SSAO completing the stack; SSR on water is evaluated and either
  shipped or parked with a recorded verdict.
- Splitscreen panes and the cockpit interior pass render consistently in both modes.

**Original mode's pixels never change.** Every landing runs the full golden sweep expecting zero
movers; a mover in original mode is stop-the-line, not a re-pin. This is deliberate: the faithful
recreation is the project's spine, and the enhanced mode is a costume it can take off.

## Decisions (2026-09-01)

| # | Question | Decision |
|---|---|---|
| 1 | Should CSVM get an enhanced graphics mode at all? | **Yes, as an explicit opt-in [Divergence]** — the user wants improved plane lighting and a lit world available beside the faithful default. |
| 2 | Scope of the first version? | **Full stack** — core lighting (lit world, authored sun/ambient, shadow maps, omnis) plus emissives/glow/tonemap plus SSAO, with SSR-on-water as an evaluation item. |
| 3 | Where does the toggle live? | **Launch-time config key `graphics.mode`, modeled on `EffectsLevel`** — read once before scene build, so no runtime material rebuild; menu exposure deferred to the menu plan. |
| 4 | What drives the enhanced sun? | **The authored per-mission SUNLIGHT values** (diffuse, ambient, orientation), promoted from the scalar the original collapses them to — no invented numbers, per the decode-first principle. |
| 5 | How is the work run? | **This plan doc, item by item** via the usual checklist/commit-next loop. |

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, B15 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B12, B13, B14, C21, C22 | The *what* is settled; the *how much* (energies, shadow distances, glow threshold) is TUNE — judged at the controls, not invented as fact. |
| **Leads only — no mechanism yet** | C23, C24, D31, D32 | Budget for investigation; C24 in particular may end in a disproof, and that is a valid landing. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md` is
  frozen — never append) and is **deleted** from
  `backlog.md` (not marked FIXED there). New decodes land with their `docs/formats/` page.
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture at the
  location the report came from.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven. **Keep this in sync as items land.**

### Wave A — Option plumbing

1. ☑ `GraphicsMode` config key, launch-time resolution, golden-run isolation
2. ☑ Thread the mode into `SceneBuilder` shader keys with byte-identical original output

### Wave B — Core lighting

11. ☐ Lit world shader variant (drop `unshaded`, matte material, keep the gamma modulate)
12. ☐ Authored SUNLIGHT drives the sun and ambient in enhanced mode
13. ☐ Sun shadow maps
14. ☐ LIGHT_STATE point lights as real OmniLight3D nodes
15. ☐ Enhanced settings reach the cockpit interior pass and every splitscreen pane

### Wave C — Post stack

21. ☐ Lighting-exempt surfaces become emissive
22. ☐ Environment glow + tonemap
23. ☐ SSAO
24. ☐ SSR on water — evaluate, then ship or park

### Wave D — Hardening and record

31. ☐ Performance gate: enhanced mode under -Perf/-Hitch, worst chapter, 4-pane splitscreen
32. ☐ Divergence documentation, final golden sweep, optional enhanced goldens

## Dependency and parallelism notes

A1 → A2 block everything else. Wave B runs in order: B11 (lit world) before B12 (calibration is
judged against a lit world), B13 and B14 after B12, B15 last. Wave C after Wave B; C22 (glow) after
C21 (emissives are what glow is for); C23 and C24 are independent of each other. Wave D last. File
contention: `SceneBuilder.cs` is touched by A2, B11, B14 and C21; `Launcher.cs` by A1, B13, C22 and
C23; `WeatherRig.cs` by B12 and B15 — never run two items from the same group in parallel
worktrees.

---

# Wave A — Option plumbing

## A1 ☑ `GraphicsMode` config key, launch-time resolution, golden-run isolation

**Goal.** A `graphics.mode` config key (`original` | `enhanced`, default `original`) exists, is
resolved once at launch, is logged at startup, and cannot leak from a user config into a golden or
`--det` run.

**Evidence (confidence: traced).** The pattern to copy is `EffectsLevel`
(`CSVM/src/Utils/EffectsLevel.cs:15-85`): a `graphics.*` config key with a shipped default, warmed
once in `Config` (`CSVM/src/Utils/Config.cs:244-247`, "read once at launch"), resolved and logged
by the launcher (`CSVM/src/Session/Launcher.cs:447-453`), unknown words warned and defaulted.

**Approach.** New `CSVM/src/Utils/GraphicsMode.cs` static class mirroring `EffectsLevel`: `Key =
"graphics.mode"`, `Default = "original"`, a `TryParse` for the two words, a resolved boolean the
scene builders read. Warm it in `Config`, log it in `Launcher._Ready` beside the clutter-fade line.
`Config` layers a `res://config.json` override under every in-code default (`Config.GetString`
etc.), and `Launcher._Ready` already drops every loaded override outright when `--det` is in
effect (`Config.ClearOverrides`, `Launcher.cs`'s `--det` block), before `EffectsLevel` or
`GraphicsMode` ever resolves — a golden shot's stored args and `--run-tests` both carry `--det`
(`analysis/goldens/README.md`, `RunTests.ps1`'s engine/golden stages), so `graphics.mode` from a
user's `config.json` is isolated for free by resolving it after that block, the same way
`EffectsLevel` already is. `GraphicsMode.Resolve` additionally takes an explicit `--graphics=`
override (`SessionSpec.GraphicsMode`) that bypasses `Config` outright and so survives `--det`,
which is what lets a golden or a deterministic capture ask for the enhanced path on purpose (D32's
optional enhanced goldens will use it). The menu plan's process-wide options store
(`docs/PLAN-menu-presentations.md`, branch `menu-presentations`) is the intended future source of
this key's user-facing value; `GraphicsMode.Enhanced` is the one resolved value every reader
consults, so that layer can be slotted in later without touching them.

**Model recommendation.** medium, low effort — mechanical plumbing on a clear template.

**Verify.** Startup log names the mode; `.\RunTests.ps1` fully green with zero golden movers; a run
with `graphics.mode=enhanced` in the user config still produces zero golden movers.

**⚠ Traps.** The goldens compare md5 over raw pixels with no tolerance; if a user config can tilt a
golden run the tripwire becomes a coin flip on whatever machine runs it. Do not land A1 without the
isolation story.

**Verified.** The complete `.\RunTests.ps1` on the Wave A tree (A1 + A2): 2792 units, 200 engine
suites (errors clean), 18 goldens hash-identical, all passing in 169.1 s. During the item:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`dotnet test CSVM.Tests/CSVM.Tests.csproj --filter "FullyQualifiedName~GraphicsModeTests"` — 7
passed, 0 failed. `$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -Suite clutter-determinism
-SkipUnits -SkipGoldens` — engine stage PASS, 1 suite run (non-zero), engine errors clean. Three
scripted `RunGame.ps1` captures (freecam C1, `--det --mute --frames=5 --screenshot=...`), read back
from `.scratch/logs/`: with no config override, `--det` alone logs
`[world] graphics mode: graphics.mode=original`; with `--graphics=enhanced` added, the same `--det`
run logs `graphics.mode=enhanced` (the CLI override surviving `--det`); with a `res://config.json`
carrying `graphics.mode: enhanced`, a `--det` run logs `config=defaults dropped_overrides=1
via=--det` followed by `graphics.mode=original` (the override dropped), while the same config under
`--no-det` logs `graphics.mode=enhanced` (a normal flight honours it). So: a user with
`graphics.mode=enhanced` in their config sees the enhanced value on a normal flight, and `original`
on every golden shot, every `--det` run and the full `--run-tests` battery (all of which carry
`--det` in their stored/implied args), unless they also pass `--graphics=enhanced` explicitly. The
test config.json and capture PNGs used for this check were removed afterward; none are committed.

## A2 ☑ Thread the mode into `SceneBuilder` shader keys with byte-identical original output

**Goal.** `GetBiasShader` (and the billboard/facade builders that will later diverge) carry an
enhanced-mode bit in their memo keys, with the generated shader text in original mode provably
unchanged.

**Evidence (confidence: traced).** The generator is keyed by feature bits
(`CSVM/src/Mech3/SceneBuilder.cs:1327-1331`) and memoised process-wide
(`SceneBuilder.cs:232-234`, PERF-22). MeshLab and the game share the same builders, so the key must
carry the mode rather than a mutable global alone.

**Approach.** Add one new bit to the key sourced from `GraphicsMode`; emit no text difference yet
(that is B11), so this item is pure plumbing. Prove byte-identity by dumping generated shader text
for every key in original mode before/after (a scratch diff, not a committed artifact).

**Model recommendation.** medium — mechanical, but the key contract has bitten before (the
2026-07-17 instance-uniform ordering bug lives next door).

**Verify.** Shader-text dump diff empty in original mode; full `.\RunTests.ps1` green, zero golden
movers.

**⚠ Traps.** The instance-uniform block's declaration order is a contract
(`csky_instance_uniforms.gdshaderinc`); this item must not touch it. Do not reuse bit values
already taken (2048 clutterFade, 4096 DebugClutterFlag).

**Verified.** The complete `.\RunTests.ps1` on the Wave A tree (A1 + A2): 2792 units, 200 engine
suites (errors clean), 18 goldens hash-identical, all passing in 169.1 s. During the item:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors, both before and after the edit. `dotnet test CSVM.Tests/CSVM.Tests.csproj --filter
"FullyQualifiedName~GraphicsModeTests|FullyQualifiedName~UvClampTests"`: 13 passed, 0 failed.
`$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -Suite clutter-determinism -SkipUnits -SkipGoldens`:
engine stage PASS, 1 suite run (non-zero), engine errors clean.
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers. `GetBiasShader` builds its text in a pure `StringBuilder`, but the memoised return value is
a `Shader` (a Godot Resource), and constructing one outside the running engine crashes the
CSVM.Tests host (confirmed empirically: `new Shader { Code = "..." }` throws
`AccessViolationException` from `godotsharp_string_name_new_from_string` under `dotnet test`), so
the pure-unit route from the approach note was not available and the proof ran in-engine instead.
A temporary instrument (a static `SceneBuilder.DumpAllShaderKeysForVerification` enumerating every
boolean/enum combination into `GetBiasShader`/`GetBillboardShader`/`GetCylindricalShader`, wired
behind a `--scratch-dump-bias-shaders=<path>` flag in `Launcher.cs`) dumped every reachable key's
generated `Shader.Code` to `.scratch/`, then was deleted before landing. Baseline: the three key
computations with the new `GraphicsMode.Enhanced` bit removed, rebuilt, dumped via `--headless
--det` to `.scratch/bias_before.txt` (12,971,808 bytes). After: the same dump with the bit
restored, same flags, to `.scratch/bias_after.txt` (12,971,808 bytes). SHA-256 of the two files
matched exactly (`fcc20683...46ca7f`), confirming byte-identical original-mode shader text before
and after the key change. A third dump under `--graphics=enhanced` (`bias_enhanced.txt`) also
hashed identical to `bias_after.txt`, confirming the new bit only partitions the memo today and
emits no text difference in either mode, as the approach requires (B11 is where enhanced-mode text
starts to diverge). All three dump files and their probe logs are untracked, git-ignored
`.scratch/` output, and are not committed.

# Wave B — Core lighting

## B11 ☐ Lit world shader variant

**Goal.** In enhanced mode, world geometry shades under Godot's lights using the decoded vertex
normals: `unshaded` is dropped, a matte material responds to sun/ambient/omnis, and the baked
gamma-space vertex modulate and cylindrical fog are kept.

**Evidence (confidence: traced).** The fullbright path appends `unshaded` at
`SceneBuilder.cs:1340-1342`; the gamma-space vertex modulate is `SceneBuilder.cs:1383-1409` and
exists to reproduce the DX7 `D3DTOP_MODULATE` (docs/formats/gotchas.md); the per-mission dimming
multiply is `SceneBuilder.cs:1434-1435`; decoded normals are read from the data (`GameZ.cs:415`)
and already transformed per fragment for the point-light spill, including the cull_front sign flip
(`SceneBuilder.cs:1452-1455`); the aircraft's lit path shows the working shaded template
(`SceneBuilder.cs:1436-1441`, roughness 0.85 / metallic 0 / specular 0.5).

**Approach.** In enhanced mode the world path compiles without `unshaded`, applies the aircraft's
pre-negated NORMAL handling (`SceneBuilder.cs:1385-1388`), sets ROUGHNESS 1.0 / METALLIC 0.0 /
SPECULAR 0.0 (matte ground, no plastic sheen), keeps the vertex modulate and fog, drops the
`csky_world_light` multiply (B12 moves that energy onto the real sun), and keeps the shader light
spill off (B14 decides its replacement). Surfaces authored `lighting: false` keep the fullbright
arm; they are C21's emissives.

**Model recommendation.** high — generated-shader surgery with fidelity interactions on every side.

**Verify.** Enhanced `--freecam` sweep of all 8 chapters, zero errors, judged at the controls;
original-mode golden sweep zero movers. Take an original-mode shader dump baseline first (A2's
instrument) so "unchanged" has been seen able to fail.

**⚠ Traps.** Baked vertex colours already encode the original's static shading, so a real sun can
darken the same corner twice; if the lit world reads muddy, the enhanced path may flatten vertex
colour toward its luminance — that is a TUNE judged at the controls, not a fact. The `normalSign`
logic exists because cull_front makes every visible fragment back-facing; getting it wrong lights
the world from underneath. BL-613 decodes that the original exempts alpha-textured surfaces from
the sun term in the *faithful* path; do not let this item's changes leak into that question.
`<TODO: re-verify BL-613 and BL-322 still open before landing.>`

## B12 ☐ Authored SUNLIGHT drives the sun and ambient

**Goal.** In enhanced mode the DirectionalLight3D's energy and the Environment's ambient come from
the mission's authored SUNLIGHT values, so night missions are genuinely dark and the aircraft stops
being lit like noon at night.

**Evidence (confidence: direction-sound).** `WeatherRig.ApplyZone` already reads the authored
SUNLIGHT block: it collapses the dimming to `csky_world_light` in gamma space
(`CSVM/src/Session/WeatherRig.cs:618-622`) and applies `SUNLIGHT_ORIENTATION` to the real sun
(`WeatherRig.cs:626`). The launcher hardcodes LightEnergy 1.6 / ambient 0.9
(`CSVM/src/Session/Launcher.cs:905-924`); missions author diffuse 0.4-2.0 and ambient 0.15-0.6
(BL-332's record). The *what* is settled; the mapping from authored units to Godot energies is
TUNE. `<TODO: confirm the exact authored field names available inside ApplyZone and whether both
diffuse and ambient reach it today or need plumbing from the zone data.>`

**Approach.** Branch in `ApplyZone`: enhanced mode writes `sun.LightEnergy` from authored diffuse,
`env.AmbientLightEnergy` from authored ambient, and sets `csky_world_light` to 1.0; original mode
is untouched. The gamma-vs-linear conversion for each value is judged against the original's
overall scene brightness at the controls.

**Model recommendation.** high — small diff, but the calibration judgement spans every chapter.

**Verify.** Enhanced captures at the c1b-night-sea and c5-city-night golden camera args (as manual
shots, not goldens): night reads dark with a moon-strength key light. Day chapters read comparable
in overall level to original mode. Original-mode goldens zero movers.

**⚠ Traps.** BL-332 is the *faithful-mode* fix for the same hardcoded values and stays open; this
item must not be presented as closing it. Splitscreen wears rig 0's zone for the one sun
(`WeatherRig.cs:626` comment); enhanced inherits that limitation knowingly.
`<TODO: re-verify BL-332 still open.>`

## B13 ☐ Sun shadow maps

**Goal.** In enhanced mode the sun casts shadow maps: buildings and terrain self-shadow, and the
player's aircraft casts a real moving shadow on the ground.

**Evidence (confidence: direction-sound).** `ShadowEnabled = false` is deliberate in the faithful
path (`Launcher.cs:912-915`; BL-324's removal confirmed by the BL-331 decode). The world renders
with `cull_front` because the source's visible side is Godot's back face
(`SceneBuilder.cs:1335-1339`), and the depth-bias trick scales VERTEX toward the eye
(`SceneBuilder.cs:1399-1401`). All shadow *settings* (mode, splits, max distance, biases) are TUNE.

**Approach.** Enhanced mode: `sun.ShadowEnabled = true`, PSSM 4 splits, max distance TUNE (start
near the fog far so shadows do not pop inside clear air). Audit caster flags: the skydome and its
moon/stars must not cast (`WorldBuilder.BuildHorizon` already disables shadows there,
`CSVM/src/Mech3/WorldBuilder.cs:483-508`); check whether front-culled world meshes need
`ShadowCastingSetting.DoubleSided` to appear in the light pass at all.

**Model recommendation.** high — shadow acne vs peter-panning tuning across eight very different
chapters, plus two known geometric oddities.

**Verify.** Enhanced flyby over a city chapter: aircraft shadow tracks the plane; building shadows
match sun orientation; no acne shimmer at grazing angles. Original-mode goldens zero movers;
`-Perf` comparison against the pre-B13 enhanced baseline.

**⚠ Traps.** The depth-bias vertex scale runs in the light pass too, nudging casters toward the
*light's* camera; at 2e-4 per level it should be negligible, but confirm no peter-panning on biased
decals before blaming Godot's own bias knobs. BL-331 (the decoded 32x32 projected blob shadow)
remains the original-mode item and must not be closed or cannibalised by this one. `<TODO:
re-verify BL-331 still open.>`

## B14 ☐ LIGHT_STATE point lights as real OmniLight3D nodes

**Goal.** In enhanced mode the world's animated point lights are real OmniLight3D nodes, so
beacons and city lights illuminate the aircraft and the shadowed world, replacing the
fullbright-only shader spill.

**Evidence (confidence: direction-sound).** LIGHT_STATE lights currently render as a 2xN data
texture consumed by an additive spill on fullbright passes only
(`shaders/csky_lights.gdshaderinc`; `CSVM/src/Mech3/WorldLights.cs:60-65`), measured at 0.26 ms
whole-viewport with 16 lights. Forward+ clusters dozens of omnis trivially. `<TODO: confirm what
the data texture carries per light (position, colour, range?) and where WorldLights gets its
animated on/off state, so the omnis inherit the same animation.>`

**Approach.** Enhanced mode: WorldLights spawns an OmniLight3D per light (range and colour from
the decoded fields, energy TUNE), keeps driving on/off through the same state updates, and the
enhanced shader variant omits the spill term to avoid double-counting. Original mode keeps the
spill untouched.

**Model recommendation.** medium — bounded feature with a working data source.

**Verify.** Night city flyby: lit zones match the spill's footprint in original mode side by side;
the aircraft picks up light passing a beacon. Light count in the scene equals LIGHT_STATE count.

**⚠ Traps.** Do not delete the spill path: original mode is its only consumer but it is the
decoded behaviour. Omni shadows stay off (16 shadowed omnis is a frame-time cliff and the original
has no equivalent).

## B15 ☐ Enhanced settings reach the cockpit pass and every splitscreen pane

**Goal.** The cockpit interior SubViewport and all splitscreen panes render with the same
graphics mode and the same enhanced settings as the main world view.

**Evidence (confidence: traced).** `CockpitOverlay.NewOverlay` gives its own-World3D SubViewport a
`Duplicate()` of the Environment and its own clone of the sun
(`CSVM/src/Flight/CockpitOverlay.cs:114-146`), so Environment changes made after duplication do
not propagate. Splitscreen panes share the main `World3D` and therefore the one WorldEnvironment
(`CSVM/src/UI/SplitScreen.cs:210-239`); MSAA is mirrored explicitly because SubViewports do not
inherit it (`SplitScreen.cs:211-228`, `CockpitOverlay.cs:125-126`).

**Approach.** Order the enhanced Environment/sun configuration before every `Duplicate()`, and
route the per-zone updates (B12) into the overlay's copies the same way `ApplyZone` reaches the
main sun. Audit for any other Environment consumers (MeshLab toggles ambient for the viewer lab,
`GameSession.cs:1634-1636`).

**Model recommendation.** medium — plumbing with a clear checklist of consumers.

**Verify.** Enhanced 2-player splitscreen with cockpit view: interior lighting level matches the
world pass in the same pane; whiteout, lens flare and HUD overlays composite unchanged. Original
mode: goldens zero movers.

**⚠ Traps.** The cockpit pass composites PremultAlpha over the world pass
(`CockpitOverlay.cs:167-175`); a tonemap difference between the two viewports shows up as a seam
at the canopy edge, which is why C22 must re-run this item's verify.

# Wave C — Post stack

## C21 ☐ Lighting-exempt surfaces become emissive

**Goal.** Surfaces the original exempts from lighting (lit windows, signs, other fullbright
overlays) stay fullbright in enhanced mode and additionally write EMISSION, so they read as light
sources in the lit world and feed C22's glow.

**Evidence (confidence: direction-sound).** The per-model `lighting: false` exemption already
routes around the dimming multiply (`SceneBuilder.cs:1327-1328` `lit` bit,
`SceneBuilder.cs:1431-1435`), and the engine's per-texture lighting exemptions are decoded
(docs/org/vertexLighting.md). BL-322's open lead says lit windows and signs are fullbright
overlays whose faithful rendering is blocked on plumbing a texture storage byte. `<TODO: establish
which flag set exactly identifies the emissive population — the model-level `lit` bit, the decoded
per-texture exemption, or both — and whether C21 shares plumbing with BL-322 rather than
duplicating it. Re-verify BL-322 still open.>`

**Approach.** In the enhanced shader variant, the exempt arm sets `EMISSION = ALBEDO` (scale TUNE)
and keeps ALBEDO out of the diffuse-lit path, so a lit window neither darkens at night nor doubles
under the sun. Original mode text unchanged.

**Model recommendation.** high — decode-adjacent, and the flag-population question decides whether
the item is right or merely pretty.

**Verify.** Night city in enhanced mode: windows and signs hold their authored brightness while
the walls around them go dark. Original-mode goldens zero movers.

**⚠ Traps.** Do not conflate this exemption with BL-613's alpha-texture sun-term exemption; they
are different decoded rules. EMISSION above 1.0 is the glow trigger in C22; keep the scale a named
TUNE, not a magic number.

## C22 ☐ Environment glow + tonemap

**Goal.** Enhanced mode gets an Environment glow pass keyed to genuinely bright pixels (C21's
emissives, tracers, explosions) and a filmic-family tonemap so the now-HDR scene rolls off instead
of clipping.

**Evidence (confidence: direction-sound).** The Environment is deliberately bare in the faithful
path (`Launcher.cs:903-927`; docs/HISTORY.md item 6 records the austerity as a calibration
decision). Godot 4.7 capabilities (verified against the 4.7 docs during planning): glow with an
HDR threshold and multiple blend modes, tonemap modes including AgX with a contrast control, both
negligible-to-low cost. Calibration is entirely TUNE.

**Approach.** In `SetupLighting`, enhanced mode only: `TonemapMode` Filmic vs AgX chosen at the
controls, `GlowEnabled = true` with `GlowHdrThreshold` ≈ 1.0 and bloom 0 so only over-1.0 pixels
bloom. Re-run B15's cockpit-seam verify (both viewports must tonemap identically).

**Model recommendation.** medium — settings work; the judgement is the user's A/B at the controls.

**Verify.** Night city and a gun-fight capture in enhanced mode: emissives bloom, mid-tone world
does not; canopy edge shows no tonemap seam. Original-mode goldens zero movers.

**⚠ Traps.** The lens flare and whiteout are 2D CanvasLayer overlays and sit outside the 3D glow
chain; they must not be re-tuned to compensate for enhanced-mode bloom (they are measured against
original footage). WorldBuilder.cs:487-488 forbids a colour-grading stage for the *faithful* dome
colour; the tonemap lives strictly behind the enhanced branch.

## C23 ☐ SSAO

**Goal.** Enhanced mode gains screen-space ambient occlusion so building clusters and street
canyons get contact shading the baked vertex colours only hint at.

**Evidence (confidence: lead-only).** Godot 4.7 Forward+ SSAO affects ambient light only, costs
moderate GPU per viewport, and is not antialiased by MSAA (verified against the 4.7 docs during
planning). No CSVM-specific evidence yet; radius/intensity against this world's scale is unknown.

**Approach.** `SsaoEnabled = true` on the enhanced Environment; tune radius/intensity/power on a
city chapter. Because SSAO runs per viewport, measure the 4-pane splitscreen case before settling
quality (project-settings half-res/adaptive levels are the lever).

**Model recommendation.** medium, low effort — one property plus tuning captures.

**Verify.** A/B toggling SSAO in enhanced mode on C5: streets gain grounded contact shading
without halos on the aircraft against sky. `-Perf` delta recorded at 1 and 4 panes.

**⚠ Traps.** SSAO reads the resolved depth buffer, so its edges shimmer independently of MSAA;
judge it in motion, not in stills.

## C24 ☐ SSR on water — evaluate, then ship or park

**Goal.** A verdict, with captures: does screen-space reflection on sea/water surfaces in enhanced
mode read well enough to ship, or is it parked with the reasons recorded?

**Evidence (confidence: lead-only).** Godot 4.6 rewrote SSR (less temporal instability, explicit
half/full-res modes); SSR reflects opaque geometry only and cannot reflect off-screen content
(verified against the docs during planning). `<TODO: establish whether water surfaces are an
identifiable material/mesh population in the decoded worlds at all; without that handle the item
is dead on arrival.>`

**Approach.** Prototype on the C1 sea: water surfaces get low roughness in the enhanced variant so
SSR has something to work with; judge at the controls in motion (banking over water is the worst
case for screen-space misses). Ship behind the mode, or park with captures and the verdict in the
landing commit.

**Model recommendation.** high — an evaluation that may end in a disproof, which needs honest
judgement more than code volume.

**Verify.** Banked flight over the C1 sea in enhanced mode: reflection coherence during roll;
recorded verdict either way. Original-mode goldens zero movers.

**⚠ Traps.** A disproof here is a valid landing (ground rules); do not tune SSR past its
screen-space physics to force a ship.

# Wave D — Hardening and record

## D31 ☐ Performance gate

**Goal.** Enhanced mode's frame cost is measured and acceptable: the full stack holds frame rate
on the heaviest chapters and in 4-pane splitscreen, with the levers (shadow distance, SSAO
quality) set from numbers.

**Evidence (confidence: lead-only).** Perf and hitch are first-class stages (`RunTests.ps1
-Perf/-Hitch`; `analysis/verification-budgets.json`); the dev rig already runs physics-bound on
late chapters in heavy scenes, so GPU headroom is real but not the only axis. No enhanced-mode
numbers exist yet.

**Approach.** Baseline original vs enhanced on C4/C5 and 4-pane splitscreen via the perf
instrumentation; record the numbers in the landing commit; set shadow max distance and SSAO
quality from them.

**Model recommendation.** medium — measurement discipline, not invention.

**Verify.** `-Perf`/`-Hitch` runs pass in both modes; the enhanced-vs-original delta is recorded
per scenario in the landing commit message.

**⚠ Traps.** Read docs/verification.md before measuring; the sim clock lagging wall time on
physics-bound scenes will masquerade as a rendering regression if measured naively.

## D32 ☐ Divergence documentation, final golden sweep, optional enhanced goldens

**Goal.** The mode is recorded as a [Divergence] with its config key documented, the whole plan's
zero-mover property is confirmed one last time, and enhanced mode optionally gains its own pinned
goldens.

**Evidence (confidence: lead-only).** BL-555 is the documented-divergence template; goldens' GOLD
rules (README + docs/verification.md) define the re-pin ritual and say a scoped change should move
only the shots exercising it, which for this plan means zero.

**Approach.** Document the mode where the architecture docs cover rendering (per the BL-555
precedent for divergences), including the config key and what enhanced changes; run the full
golden sweep expecting zero movers; then decide whether to pin one day and one night enhanced
golden (recommended: two new shots whose stored args name the mode explicitly, so the enhanced
path gets its own tripwire without touching the original 18). Hand the options-menu exposure to
the menu plan as a note, not an item here.

**Model recommendation.** medium — documentation and ritual, with one product judgement (the new
goldens) for the user.

**Verify.** `.\RunTests.ps1` fully green; manifest diff shows only the two new shots if added, and
nothing else.

**⚠ Traps.** A stale `-RegenGoldens` silently re-baselines (GOLD-9): check `git diff` on the
manifest before believing any PASS in this item.
