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

11. ☑ Lit world shader variant (drop `unshaded`, matte material, keep the gamma modulate)
12. ☑ Authored SUNLIGHT drives the sun and ambient in enhanced mode
13. ☑ Sun shadow maps
14. ☑ LIGHT_STATE point lights as real OmniLight3D nodes
15. ☑ Enhanced settings reach the cockpit interior pass and every splitscreen pane

### Wave C — Post stack

21. ☑ Lighting-exempt surfaces become emissive (light-source class only; the rest is a disproof)
22. ☑ Environment glow + tonemap
23. ☑ SSAO
24. ☑ SSR on water — evaluate, then ship or park

### Wave D — Hardening and record

31. ☑ Performance gate: enhanced mode under -Perf/-Hitch, worst chapter, 4-pane splitscreen
32. ☑ Divergence documentation, final golden sweep, optional enhanced goldens

### Wave E — At-the-controls findings from the Wave C montages

41. ☑ Shadows end before the fog ramp, not inside it
42. ☑ C5 reads too bright in enhanced mode, its water a light grey
43. ☑ The clutter building fade reaches as far as the pushed fog
44. ☑ The enhanced Environment's sky is the mission's dome, not the placeholder procedural sky
45. ☑ The lit world fogs after lighting, so fogged hills fade instead of keeping their shading
46. ❌ The water mirror strength, measured against the original's water, or the water bit parked
47. ☑ Day chapters read brighter than the original overall; the energy mapping re-anchored on frames

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

## B11 ☑ Lit world shader variant

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
BL-613 and BL-322 are both open in `backlog.md`, and this item touches neither: the enhanced arm
keys on the model-level `lighting` bit alone (`docs/org/vertexLighting.md`'s first test), leaving
the per-texture alpha exemption BL-613 is about, and BL-322's C5 facade brightness, exactly as they
are in the faithful path.

**Verified.** The complete `.\RunTests.ps1` on the merged B11 + B12 tree: 2796 units, 200 engine
suites (errors clean), 18 goldens hash-identical, all passing in 150.0 s.

`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -Suite plane-shader-reuse -SkipUnits -SkipGoldens`:
engine stage PASS, 1 suite run of 200 (non-zero), engine errors clean, 0 unexpected lines.
`$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18
shot(s) hash-identical, zero movers.

Original-mode byte identity was proved with A2's method rebuilt as a throwaway: a static enumerator
inside `SceneBuilder`'s constructor, armed by a `CSVM_SCRATCH_SHADER_DUMP` environment variable so
no file outside this item's ownership was touched, walking every reachable key of all three
generators (`GetBiasShader` over `DebugClutterFlag` x shaded x textured x blend x scissor x
doubleSided x scroll x clampUv x lit x fogged x 4 edgeClamp values x clutterFade, plus every
billboard and both cylindrical axes) and writing each key's generated `Shader.Code` to a file. The
baseline came from the committed tree BEFORE the edit; no stash was used. Runs were
`.\RunProbe.ps1 --freecam --chapter=C1 [--graphics=enhanced] --det --mute --frames=5
--screenshot=...`. `dump_orig_before.txt` and `dump_orig_after.txt` are both 12,672,544 bytes and
SHA-256 `42DCA26A4BEB01E7AE259199288B481A82A325C508E64B281D7A3DB82462A3D1`, identical, so original
mode's shader text did not move. `dump_enh_after.txt` is 12,255,776 bytes, SHA-256
`A5EC0AE3E490883E5CC46D0079F4A46683B65920CBB8B10BC9C879CDA62F7717`, which differs, so the
instrument was seen able to fail. The instrument was deleted before finishing and the dumps are
untracked `.scratch/` output.

The 8-chapter enhanced `--freecam` sweep (`--det --mute --frames=15 --screenshot=` per chapter, run
in both modes) reported zero engine error lines everywhere and identical gamez-node, mesh-instance,
uv-clamped and edge-clamped counts in both modes for all of C1, C1B, C1C, C2, C2B, C3, C4 and C5.
The first C1 enhanced run was killed at the 300 s probe timeout after writing its screenshot; a
re-run of the same command exited 0 in 7.1 s, which is the shared-hidden-desktop flake
`docs/verification.md` LOG-19 describes.

At the captures the world reads as genuinely lit and the normal sign is right: with C2's sun 65
degrees above the horizon, roofs and ground take the light while walls fall into shade, which is
the opposite of what an inverted normal produces. Two things are brighter than the original and are
left for the items that own them. Day chapters wash out, distant terrain most of all, because the
launcher's hardcoded LightEnergy 1.6 / ambient 0.9 now reaches geometry that used to ignore it and
nothing tonemaps the result; B12 and C22 own that. Shaded walls read dark enough to suggest the
baked vertex colour and the real sun are darkening the same surface twice, which is exactly the
flatten-vertex-colour-toward-luminance TUNE the traps above name. It is NOT implemented here: the
calibration it would be judged against does not exist until B12 lands.

## B12 ☑ Authored SUNLIGHT drives the sun and ambient

**Goal.** In enhanced mode the DirectionalLight3D's energy and the Environment's ambient come from
the mission's authored SUNLIGHT values, so night missions are genuinely dark and the aircraft stops
being lit like noon at night.

**Evidence (confidence: direction-sound).** `WeatherRig.ApplyZone` already reads the authored
SUNLIGHT block: it collapses the dimming to `csky_world_light` in gamma space
(`CSVM/src/Session/WeatherRig.cs:618-622`) and applies `SUNLIGHT_ORIENTATION` to the real sun
(`WeatherRig.cs:626`). The launcher hardcodes LightEnergy 1.6 / ambient 0.9
(`CSVM/src/Session/Launcher.cs:905-924`); missions author diffuse 0.4-2.0 and ambient 0.15-0.6
(BL-332's record). The *what* is settled; the mapping from authored units to Godot energies is
TUNE. Of the authored block, only two values reach `ApplyZone`: `ZoneWeather.WorldLight`, which is
already the collapse `clamp(SUNLIGHT_AMBIENT + SUNLIGHT_DIFFUSE * 0.46, 0.15, 1)`
(`Weather.WorldLightFactor`), and `ZoneWeather.SunOrientation`. `SUNLIGHT_DIFFUSE` and
`SUNLIGHT_AMBIENT` are read inside that collapse and discarded, and the two colour keys
`SUNLIGHT_COLOR_DIFFUSE`/`SUNLIGHT_COLOR_AMBIENT` are never read at all, so all four need plumbing
onto the `ZoneWeather` record before a real light can be driven from them. The colours are worth
carrying: 34 of the install's 106 `ZONE*` blocks author one away from white, C4 among them (warm sun
`[1.0, 0.8, 0.7]`, cold ambient `[0.7, 0.9, 1.0]`).

**Approach.** Branch in `ApplyZone`: enhanced mode writes `sun.LightEnergy` from authored diffuse,
`env.AmbientLightEnergy` from authored ambient, and sets `csky_world_light` to 1.0; original mode
is untouched. The gamma-vs-linear conversion for each value is judged against the original's
overall scene brightness at the controls.

**As landed.** `Weather.ZoneWeather` gains `SunDiffuse`, `SunAmbient`, `SunColorDiffuse` and
`SunColorAmbient`, parsed beside the untouched `WorldLight` collapse. `WeatherRig` takes the
session `Environment` as a fifth optional constructor argument (`GameSession` passes `_env`) and,
in enhanced mode only, `ApplyZone` calls `ApplyEnhancedLighting`: it rewrites `csky_world_light`
to 1.0, sets `sun.LightEnergy`/`sun.LightColor` and switches the Environment's ambient source from
`Sky` to `Color` so the authored ambient colour and energy are the ones that render. The mapping
is the pure `WeatherRig.EnhancedEnergies`, two TUNE factors anchored on the install's modal day
zone (`SUNLIGHT_DIFFUSE` 1.5, `SUNLIGHT_AMBIENT` 0.5: the modal pair, authored by 34 of the 106
`ZONE*` blocks, with the 1.5 diffuse alone in 54 of them)
landing on the 1.6 and 0.9 the launcher hardcodes: `SunEnergyPerDiffuse` 1.07 and
`AmbientEnergyPerAuthored` 1.8. A day mission therefore keeps the level it already had, and the
data alone carries C1B's night zone (0.6 / 0.15) to 0.64 / 0.27. The resolved pair prints on both
zone log lines. Original mode reaches none of it: `GraphicsMode.Enhanced` gates the whole call and
the faithful writes above it are unchanged.

**Model recommendation.** high — small diff, but the calibration judgement spans every chapter.

**Verify.** Enhanced captures at the c1b-night-sea and c5-city-night golden camera args (as manual
shots, not goldens): night reads dark with a moon-strength key light. Day chapters read comparable
in overall level to original mode. Original-mode goldens zero movers.

**⚠ Traps.** BL-332 is the *faithful-mode* fix for the same hardcoded values and stays open, and
this item does not close it: it is open in `backlog.md` under Damage & destruction's lighting
group, it asks for the same two constants in the *original* path, and its own note holds back the
`Sky`-versus-`Color` ambient-source question as a separate rendering-design decision, which the
enhanced arm answers only for itself. Splitscreen wears rig 0's zone for the one sun
(`ApplyZone`'s comment on the rotation write); enhanced inherits that limitation knowingly. The
Environment ambient source moves from `Sky` to `Color` in enhanced mode, because a sky-sourced
ambient reads the placeholder procedural sky and would ignore both authored values.

**Verified.** The complete `.\RunTests.ps1` on the merged B11 + B12 tree: 2796 units, 200 engine
suites (errors clean), 18 goldens hash-identical, all passing in 150.0 s. During the item, in the item's worktree with
`$env:CSVM_DATA_ROOT="Z:\CSVM"`: `dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build --filter
"FullyQualifiedName~SunlightEnergyTests"`: 4 passed, 0 failed.
`.\RunTests.ps1 -SkipEngine -SkipGoldens`: units PASS, 2796 passed of 2796.
`.\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`: engine PASS, 1 suite run, engine errors
clean; `.\RunTests.ps1 -Suite sun-orientation -SkipUnits -SkipGoldens`: engine PASS, 1 suite run,
engine errors clean. `.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s)
hash-identical, zero movers.

Sixteen captures through `.\RunProbe.ps1` (the repo's rule for a scripted `--screenshot` launch),
eight at the `c1b-night-sea` / `c5-city-night` / `c4-snow` / `c1-waterfall` golden camera args as
manual `--freecam` shots and eight as `--fly --plane=player_bhawk` shots from the same poses, each
in both modes, under `.scratch/b12/`. Mean frame luminance (0-255, every second pixel), and for the
flight shots the mean over the aircraft's red-livery pixels in the plane box (x 430-860, y
425-520), which isolates the only lit surface in the frame:

| Shot | frame, original | frame, enhanced | livery, original | livery, enhanced |
|---|---|---|---|---|
| c1b-night-sea, freecam | 29.51 | 52.04 | no aircraft | no aircraft |
| c5-city-night, freecam | 11.42 | 11.43 | no aircraft | no aircraft |
| c4-snow day, freecam | 128.88 | 128.88 | no aircraft | no aircraft |
| c1-waterfall day, freecam | 73.69 | 83.24 | no aircraft | no aircraft |
| c1b-night-sea, flight | 24.28 | 47.05 | 82.91 | 60.69 |
| c5-city-night, flight | 14.91 | 15.11 | 96.15 | 102.64 |
| c4-snow day, flight | 119.18 | 119.19 | 69.51 | 69.75 |
| c1-waterfall day, flight | 63.61 | 73.05 | 58.79 | 60.84 |

⚠ **The frame columns do not yet show the item working, and cannot in this tree.** Without B11 the
world is still `unshaded`, so a frame's level is the fullbright world's, and enhanced mode's
`csky_world_light = 1.0` removes the per-mission dimming without a sun replacing it: C1B's night
frame gets *brighter* (29.5 → 52.0), and C1's day frame likewise (73.7 → 83.2), by exactly the
dimming those zones authored (world light 0.43 and 0.80). C4 and C5 do not move because their
authored world light already clamps at 1.00. The livery columns are what this tree can show, and
they carry the item: the aircraft in C1B's night mission dims 27% (82.9 → 60.7) while the same
aircraft in C4's day mission is unchanged (69.5 → 69.8), which is the anchor holding. C5 is the
instructive non-mover: it is a night *scene* whose zone authors a day-level `SUNLIGHT` (1.5 / 0.5,
its darkness coming from `FOG_COLOR` and the art), so nothing in the data asks for a dimmer light
there and none is invented.

The log lines confirm the resolved energies reach the light. Enhanced C1B/IA1:
`weather [zone1]: … world light 0.43; sun -65° pitch / 90° yaw; … ; enhanced sun energy 0.64
(diffuse 0.6), ambient energy 0.27 (ambient 0.15)`; enhanced C4/IA1 zone2 and C5/IA1 zone1 both
print `enhanced sun energy 1.61 (diffuse 1.5), ambient energy 0.90 (ambient 0.5)`; the same runs in
original mode print the line without the suffix.

## B13 ☑ Sun shadow maps

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

**The fog push is the user's decision, taken after seeing the first captures**: shadowed ground
read badly through the authored haze, so enhanced mode scales each zone's authored fog near/far
outward by `WeatherRig.EnhancedFogRangeScale` (2.0) and the sun's `DirectionalShadowMaxDistance` is
then set from that scaled far, per zone. The faithful path keeps the authored ranges exactly, so
the prohibition in `ApplyZone` and `docs/org/weather.md`'s FOG_SCALE finding are untouched.

**Model recommendation.** high — shadow acne vs peter-panning tuning across eight very different
chapters, plus two known geometric oddities.

**Verify.** Enhanced flyby over a city chapter: aircraft shadow tracks the plane; building shadows
match sun orientation; no acne shimmer at grazing angles. Original-mode goldens zero movers;
`-Perf` comparison against the pre-B13 enhanced baseline.

**⚠ Traps.** The depth-bias vertex scale runs in the light pass too, nudging casters toward the
*light's* camera; at 2e-4 per level it should be negligible, but confirm no peter-panning on biased
decals before blaming Godot's own bias knobs. BL-331 (the decoded 32x32 projected blob shadow)
remains the original-mode item and must not be closed or cannibalised by this one. BL-331 is open
in `backlog.md` under Damage & destruction, fully decoded in `docs/org/shadows.md`, and asks for a
projected 32x32 silhouette quad along `SHADOW_ANGLES` in the FAITHFUL path; its own note already
records that Godot shadow mapping cannot be that mechanism. This item adds a shadow map to the
enhanced path only and leaves every word of BL-331 standing.

**As landed.** `Launcher.SetupLighting` calls `EnableSunShadows` in enhanced mode only: PSSM 4
splits, blended, splits 0.06 / 0.17 / 0.42, `ShadowBias` 0.05, `ShadowNormalBias` 1.25, and
`DirectionalShadowMaxDistance` 6000 as the fallback a session with no weather.json gets. A flown
mission overwrites that distance per zone: `WeatherRig.ApplyEnhancedLighting` sets it from the
zone's authored fog far through `WeatherRig.FogRangeFor`, the same pure helper `ApplyZone` and
`ApplyFogState` write the fog range through, so shadows always end where that zone's haze does
(C2 4800 m, C5 4500 m, C1 and C2B 8000 m, C3 and C4 9000 m, C1B 9400 m). `FogRangeFor` is identity
in original mode. Of the fog range's consumers only the `csky_fog_range` global is scaled: the
whiteout and cloud band are driven by CLOUD_COVER altitudes, the zone gate is a `zone_id` cull-mask
gate with no distance in it, `WorldLights`' fade is its own 900/1500 m pair, the skydome is fitted
from `_camera.Far`, and `ZoneWeather.ClipFar` is parsed and logged but reaches nothing (the camera
far plane is `Launcher`'s fixed 40000 m, already past every pushed fog far), so nothing needed
extending. The caster audit found the tree already almost right: clutter, precipitation, fog-volume
clutter, the map-edge extender, projectiles, effect emitters, point-sprite lights and every debug
overlay were already `ShadowCastingSetting.Off`, and both billboard shader generators declare
`shadows_disabled`, so cloud sprites and glow flares cannot cast at all. `WorldBuilder` adds
`DisableShadows` on the cloud deck and the placed cloud clusters, beside the dome/moon/stars call
that was already there. The world meshes and the aircraft keep the default `On` and are the only
casters.

**Verified.** The complete `.\RunTests.ps1` on the merged B13 + B14 tree: 2796 units, 200 engine
suites (errors clean), 18 goldens hash-identical, all passing in 167.7 s. During the item, in the item's worktree with
`$env:CSVM_DATA_ROOT="Z:\CSVM"`: `dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`: engine PASS, 1 suite run of 200, engine
errors clean, 0 unexpected lines. `.\RunTests.ps1 -Suite sun-orientation -SkipUnits -SkipGoldens`:
engine PASS, 1 suite run of 200, engine errors clean. `.\RunTests.ps1 -SkipUnits -SkipEngine`
(goldens only): PASS, 18 shot(s) hash-identical, zero movers, on an RTX 5080.

The 8-chapter `--freecam` sweep (`--det --mute --frames=15 --screenshot=` per chapter, both modes)
reported zero engine error lines in all 16 runs and identical gamez-node, mesh-instance, uv-clamped
and edge-clamped counts between the modes for C1, C1B, C1C, C2, C2B, C3, C4 and C5.

`DoubleSided` casting is NOT needed, and the control proves it rather than assuming it: the same C1
airfield frame rendered with the world meshes' default `CastShadow` and with
`ShadowCastingSetting.DoubleSided` differs in 1 pixel of 921,600, by 1 unit. The `cull_front` world
already presents the sun the face it needs, because the source's visible side is Godot's back face.
The instrument was an environment-gated line in `SceneBuilder`, deleted before finishing.

At the captures the shadows read right. C1's 25 degree sun puts a hard cliff shadow across the
water at the `c1-waterfall` golden pose that original mode does not have, with no dithered acne
anywhere on the terrain at that grazing angle, which is what the 0.05 / 1.25 bias pair was chosen
for. Road markings and runway decals sit flat on the C1 apron and the C2 plaza in every capture, so
the depth-bias VERTEX scale running in the light pass costs nothing visible and Godot's own bias
knobs were left where they are. Buildings cast onto the ground: the C1 hangar's shadow lies on the
concrete apron in enhanced mode and not in original. The aircraft casts a real moving shadow,
caught at three sim frames of one C3 low pass. A shadows-on / shadows-off pair at the same C2 pose
isolates the whole shadow contribution to 3.7% of the frame with a peak darkening of 152 (summed
RGB), which is the honest size of the effect under a 65 degree sun.

The fog push at 2.0 was judged against the alternatives at the horizon in C1 and C4: it doubles the
clear air without changing the ramp's shape, and no chapter shows an unhazed cut or the map edge at
the new far (C2 4800 m is the shortest and its far hills still fade). Montages under
`.scratch/b13/`: `M1_c2_buildings`, `M2_c1_building_shadows`, `M3_doublesided_control`,
`M4_c1_acne`, `M5_aircraft_shadow`, `M6_c1_fog_push`, `M7_c1_fog_shadow_boundary`,
`M8_c4_fog_push`, `M9_c4_fog_shadow_boundary`.

Perf, C5 (heaviest) through `RunProbe` with `--freecam --perf --det --mute --frames=600`, three
runs per configuration, mean over the eight steady windows past sim frame 180. `frame_ms` is 8.33
in every run (the 120 fps cap), so the frame budget is untouched and `gpu_ms` is the number that
moves: original 1.504 / 1.536 / 1.505 (mean 1.515), enhanced without the shadow pass 1.559 / 1.559
/ 1.560 (mean 1.559), enhanced with it 1.631 / 1.631 / 1.630 (mean 1.631). The shadow pass costs
0.072 ms of GPU time, 4.6%; the whole enhanced mode costs 0.116 ms, 7.7%. `render_cpu_ms` goes 1.13
to 1.31.

B15 must copy onto the cockpit overlay's sun clone (`CockpitOverlay.cs:114-146`) everything
`EnableSunShadows` writes plus the per-zone distance: `ShadowEnabled`, `DirectionalShadowMode`,
the three splits, `DirectionalShadowBlendSplits`, `ShadowBias`, `ShadowNormalBias`, and
`DirectionalShadowMaxDistance` re-resolved from the zone the way `ApplyEnhancedLighting` does. The
overlay's own camera is `Far = 100`, so its useful distance is nothing like the world pass's.

## B14 ☑ LIGHT_STATE point lights as real OmniLight3D nodes

**Goal.** In enhanced mode the world's animated point lights are real OmniLight3D nodes, so
beacons and city lights illuminate the aircraft and the shadowed world, replacing the
fullbright-only shader spill.

**Evidence (confidence: direction-sound).** LIGHT_STATE lights currently render as a 2xN data
texture consumed by an additive spill on fullbright passes only
(`shaders/csky_lights.gdshaderinc`; `CSVM/src/Mech3/WorldLights.cs:60-65`), measured at 0.26 ms
whole-viewport with 16 lights. Forward+ clusters dozens of omnis trivially. The data texture packs
two texels per light: position (xyz) and range max (w) in the first, linear colour (rgb, already
distance-faded) and range min (a) in the second (`WorldLights.Commit`'s pack loop). The animated
on/off state, position and colour all come from `LightChannel.Tick` (`CSVM/src/Mech3/Anim/
LightChannel.cs`), which applies `LIGHT_STATE`/`LIGHT_ANIMATION` events onto a per-name `AnimLight`
record and, once a frame, calls `Begin`/`Add`/`Commit` on the same `WorldLights` instance the
texture reads from; an inactive or zero-range light is simply not submitted that frame.
`WorldLights.Commit` itself owns the 900-1500 m nearest-viewer fade and the `MaxActive` (16)
significance-rank budget, both already keyed to the same submitted set. Because the enhanced arm
of B11's lit-world shader is not the `fullbright` path, it never calls `csky_light_spill` at all,
so double-counting was already structurally impossible before this item; no shader edit was
needed.

**Approach.** Enhanced mode: `WorldLights` takes an optional `Node3D` parent (`WorldSession` passes
the world root it already builds), and gates spawning on `GraphicsMode.Enhanced` internally, so
original mode passes the same constructor and creates nothing. The same `Commit` that packs the
texture also mirrors `_pending[0..n)` onto a pool of `OmniLight3D` nodes bounded at `MaxActive`,
reusing hidden nodes rather than freeing and respawning every frame, driven off the identical
distance-faded `Entry` the texture reads (so an omni dims and vanishes exactly when its texel
does, never popping). `Dispose` frees every pooled omni.

**Model recommendation.** medium — bounded feature with a working data source.

**Verify.** Night city flyby: lit zones match the spill's footprint in original mode side by side;
the aircraft picks up light passing a beacon. Light count in the scene equals LIGHT_STATE count.

**⚠ Traps.** Do not delete the spill path: original mode is its only consumer but it is the
decoded behaviour. Omni shadows stay off (16 shadowed omnis is a frame-time cliff and the original
has no equivalent). Only C1 has `OnStartup` `LIGHT_STATE` definitions (docs/formats/
anim-definitions.md's "Point lights"); every other chapter's lights sit behind `ON_CALL`
combat/destruction effects a freecam bootstrap never reaches, so a night-city chapter like C5
commits zero lights at boot and is the wrong place to look for the omni footprint.

**Verified.** The complete `.\RunTests.ps1` on the merged B13 + B14 tree: 2796 units, 200 engine
suites (errors clean), 18 goldens hash-identical, all passing in 167.7 s. During the item:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build`: 2796 passed, 0 failed (WorldLights reaches
live `Node`s, so it has no headless unit coverage, per the repo's own rule for `src/Testing/`).
`$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -Suite world-lights-nearest-viewer -SkipUnits
-SkipGoldens`: engine PASS, 1 suite run, engine errors clean, in both an unmodified launch
(original mode) and a `--graphics=enhanced` `RunProbe.ps1` launch of the same suite (enhanced
mode); the suite was extended with a same-instance assertion that a parented `WorldLights` spawns
one `OmniLight3D` per committed light in enhanced mode and zero in original mode, and that
`Dispose` frees them, and it passed in both launches. `.\RunTests.ps1 -SkipUnits -SkipEngine`
(goldens only): PASS, 18 shot(s) hash-identical, zero movers.

An 8-chapter `--freecam --det --mute --frames=15` sweep, run in both modes, reported exit 0 and
identical gamez-node/mesh-instance counts for every chapter with no error or exception lines
beyond a pre-existing texture-fallback warning.

Light count: a C1/IA1 freecam and flight launch under `--debug-anim` prints `anim/debug: world
lights 15 rendered of 53 live` in original mode and `anim/debug: world lights 15 rendered of 53
live (enhanced: 15 omni)` in enhanced mode, the same 15 in both, confirming the committed count and
the spawned omni count match. C5's equivalent `--debug-anim` capture prints `world lights 0
rendered of 0 live` in both modes, consistent with the chapter carrying no `OnStartup` lights (see
the trap above); the c5-city-night golden's hash is unaffected either way.

Captures under `.scratch/b14/`, composed into side-by-side montages with a local copy of B11's
`montage.ps1`: `montage_freecam_c1.png` (C1/IA1 default freecam spawn, original vs enhanced, whole
world materially different under B11's lit shading, both reporting the same 15/15 lights);
`montage_dock.png` (a freecam close to `docklight1`/`docklight2` at `(-6324,10,-3299)`, spill
footprint beside the real-omni result); `montage_beacon.png` (the aircraft passing directly under
`docklight1`, cropped to the cockpit/spine region under the light: mean luma over that region rises
from 57.1 in original mode to 65.9 in enhanced, the fuselage visibly warmer and brighter in the
crop). All capture PNGs and the montage script are untracked `.scratch/` output, not committed.

Perf: `RunProbe.ps1 --freecam --chapter=C1 --no-det --mute --no-vsync --perf` for 60 s each side,
steady-state (post warm-up) `[perf] window` lines: original `gpu_ms` 0.22-0.27, `render_cpu_ms`
0.63-0.90; enhanced `gpu_ms` 0.28-0.32, `render_cpu_ms` 0.90-1.06 (15 real omnis over Forward+ cost
roughly 0.05 ms GPU time), in line with the item's evidence that the mechanism is cheap either way.

## B15 ☑ Enhanced settings reach the cockpit pass and every splitscreen pane

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

**As landed.** `CockpitOverlay.NewOverlay` copies its interior sun's `ShadowEnabled` off the live
world sun rather than hardcoding `false`; by the time it runs, `WeatherRig.Build` has already
applied the flown zone (`GameSession.BuildCockpitPasses` runs after `_weatherRig.Build`), so the
copy is already the right zone's settings, with no ordering change needed. When the live sun's
shadows are on, the clone also copies `DirectionalShadowMode`, all three splits,
`DirectionalShadowBlendSplits`, `ShadowBias` and `ShadowNormalBias` verbatim, and sets its own
`DirectionalShadowMaxDistance` to `Min(liveSun.DirectionalShadowMaxDistance, camera.Far)`: every
zone's fog-far distance (4500-9400 m) exceeds the overlay camera's 100 m far plane, so the clamp
always lands on 100 m, keeping the PSSM splits sized to the near-field panel geometry this pass
actually draws rather than to a distance the pass never renders. `WeatherRig.RegisterExtraLighting`
takes a second (sun, env) pair and `ApplyEnhancedLighting` writes the same `LightEnergy`,
`LightColor`, `AmbientLightSource/Color/Energy` onto every registered pair beside the session sun
and Environment (factored into the shared `ApplyEnhancedSunAndEnv`), deliberately excluding the
shadow max distance since a registered clone's distance is camera-relative and set once.
`GameSession.BuildCockpitPasses` registers each overlay's `Sun`/`Env` (two new accessors) with
`_weatherRig` in enhanced mode, so a zone crossing mid-flight reaches the interior pass too — seen
live in the splitscreen capture below, where camera state 1 fires mid-run and both panes' cockpit
log lines pick up the new zone's `enhanced sun energy`/`shadows to` values.
The consumer audit (`Grep` for `new DirectionalLight3D`/`new Godot.Environment`/`.Duplicate()`
across `CSVM/src`) found one other Environment/sun consumer: `MeshLab` takes `_sun`/`_env` by
reference from `GameSession` (no `Duplicate()`), so it already reads whatever the session's own
objects hold; its separate `_labLight` for the viewer's "Scoped" lighting demo is an inspection
tool outside this item's cockpit/splitscreen scope and untouched. Splitscreen needed no code
change: every pane's `SubViewport.World3D` is explicitly set to the main viewport's
(`SplitScreen.Init`), so the one session sun and WorldEnvironment are already shared, and each
pane computes its own directional shadow map off its own camera — `PositionalShadowAtlasSize`
(the omni/spot atlas) is moot since B14 keeps every omni's `ShadowEnabled` off.

**Verified.** The complete `.\RunTests.ps1` on the Wave B tree: 2796 units, 200 engine suites
(errors clean), 18 goldens hash-identical, all passing in 145.3 s.
In the item's own worktree, `$env:CSVM_DATA_ROOT="Z:\CSVM"` set first throughout.
`dotnet build CSVM/CSVM.sln`: clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -SkipEngine -SkipGoldens`: units PASS, 2796 passed of 2796, 0 failed.
`.\RunTests.ps1 -Suite cockpit-overlay-pass -SkipUnits -SkipGoldens`: engine PASS, 1 suite run,
engine errors clean. `.\RunTests.ps1 -Suite splitscreen-listeners -SkipUnits -SkipGoldens`: engine
PASS, 1 suite run, engine errors clean. `.\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`
and `.\RunTests.ps1 -Suite sun-orientation -SkipUnits -SkipGoldens`: both engine PASS, 1 suite run
each, engine errors clean (WeatherRig's registration list touches neither suite's assertions).
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers, on an RTX 5080. `.\CheckCommentCaps.ps1 -Summary` and `.\CheckEncoding.ps1`: both clean
over the whole tree.
Twelve captures through `.\RunProbe.ps1` (absolute `--screenshot=` paths; a relative one resolves
against the engine's own working directory and silently fails to save), each an original/enhanced
pair, under `.scratch/waveB/`: C2's default freecam spawn (buildings), C1's and C4's default
freecam spawns (the 2x fog push), C5's default freecam spawn (night city, unmoved), the aircraft
at `--pos=-6420,25,-3260 --direction=1,0,-0.3` near C1's `docklight1`/`docklight2` (CAP-10's night
version is not possible in day C1, so this is the closest live substitute — the omni's glare and
lens-flare-like starburst are visible on both the aircraft and the water in enhanced mode, absent
in original), and the splitscreen/cockpit pair
(`--fly --players=2 --plane=player_bhawk,player_bhawk --chapter=C1 --view=cockpit --det --mute
--frames=15`). All twelve runs report 0 `ERROR` lines in their `.err` stream. The splitscreen
capture's log shows both panes' cockpit passes built (`cockpit: interior drawn in its own pass at
the origin for 2 rig(s)`) and, mid-run, a camera-state zone change re-resolving the enhanced
energies and shadow distance (`weather: camera state 1 -> fog zone 'zone1' — … enhanced sun energy
1.28 (diffuse 1.2), ambient energy 0.45 (ambient 0.25), shadows to 3500 m`) with no unexpected
lines following it — the registered cockpit clones picked up that change alongside the session sun.
Visual check of both splitscreen images: pane 1 and pane 2 read at the same lighting level as each
other in both modes, and the world outside the canopy in enhanced mode shows real hillside shading
and building shadows that original mode does not, while the HUD (speed/altitude/throttle, the
weapon readout, the gunsight) and the interior dashboard composite identically in placement and
legibility between modes.
Seam measurement (mean luminance, 0-255, 15x20 px patches either side of the left canopy strut's
edge at x=280-295 world / x=310-325 strut, `System.Drawing`, `.scratch/waveB/seam.ps1`), both
panes of the splitscreen capture: original mode pane 1 world 136.47 / strut 142.36 (delta 5.88),
pane 2 world 176.00 / strut 163.20 (delta 12.80); enhanced mode pane 1 world 89.05 / strut 68.11
(delta 20.94), pane 2 world 74.00 / strut 70.86 (delta 3.14). The deltas vary between panes in
BOTH modes (different terrain sits behind the same screen-space patch at each pane's slightly
different altitude), which is the control showing the variation is content, not a mode-introduced
seam; a 4x-zoomed crop of the largest-delta edge (enhanced pane 1) shows a clean composite line
with no banding or artifact at the boundary.
Every montage and its exact command line is listed in the report; all are untracked `.scratch/`
output, not committed.

# Wave C — Post stack

## C21 ☑ Lighting-exempt surfaces become emissive

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

**Census result: neither flag identifies the emissive population, and one proper subset does.**
Counted over each chapter's placed models, joined to the textures they draw (`.scratch/c21/census.ps1`
over the extraction JSON; the table is now in `docs/org/vertexLighting.md`):

| Chapter | placed models | `lighting: false` | of which the light-source class | of which the general (bias-shader) arm |
|---|---|---|---|---|
| C5 (night city) | 2,851 | 232 models / 603 nodes | 99 models / 448 nodes | 120 models / 137 nodes |
| C1 (day) | 2,237 | 421 models / 1,106 nodes | 124 models / 760 nodes | 276 models / 285 nodes |

The light-source class is model type `Facade` + facade mode `Spherical` minus the cloud sprites,
which is `SceneBuilder.IsGlowSpriteMesh` and the original's own camera-facing flare classification.
Its members in both chapters are only flares, lamps, railway signals, muzzle tips, explosion sprites
and the moon: `poleflare` (246 C5 nodes), `flare_red`, `flare_green`, `light_flare`, `oil_liteflare`
(39 C1 nodes), `rr_litegreen`/`rr_litered`, `dock_liteflare`, `hangar_flare1`, `bigflare01/02`,
`beflare5`, `moon1`. The general arm's is heterogeneous and half non-luminous: C1's cloud deck (144
`cloudlayer` nodes), both skydome textures, the baked ground-shadow decals (`lkshad3`, `lkshad6`,
`sootstn`), the tree and bush cards, hangar interior skins, the zeppelin's passenger figures and the
destroyed-building skin, beside genuinely self-lit `bowlsign`, `hotel_*`, `rasign`, `lite_out`,
`flaglite01`, tracers and muzzle flashes. Scaling that whole arm would make a cloud deck, a skydome
and a baked shadow glow.

The per-texture exemption is gate 2 of `docs/org/vertexLighting.md`, the alpha bit, and it is the
same byte BL-322 is blocked on. It is not plumbed: `TextureArchive` classifies alpha from the
decoded PNG pixels and nothing reads the extractor's `alpha` field. That field IS present in the
extraction output (`extracted/<chapter>/texture/manifest.json` and the matching `.zip`), which
`vertexLighting.md` previously recorded as absent, so BL-322 needs a manifest reader rather than an
extractor change. C21 plumbs nothing: gate 2 governs the FAITHFUL path (BL-322, BL-613) and its own
exempted set mixes the lit-window and signage overlays with the baked shadow decals, the fog
gradients and the cloud sprites, so plumbing it would not identify the emissive population either.
Both backlog items were re-verified open and are untouched.

So the item lands on the light-source class alone, and the "lit windows and signs" half is a
recorded disproof: C5's lit windows are not separable surfaces where they matter most. They are
bright texels inside the `lighting: true` wall textures, so no per-surface rule, plumbed or not, can
hold them at their authored brightness.

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

**Verified.** The complete `.\RunTests.ps1` on the merged C21 + C23 + C24 tree: 2796 units, 200
engine suites (errors clean), 18 goldens hash-identical, all passing in 139.2 s.

The landed change is 15 lines in `CSVM/src/Mech3/SceneBuilder.cs`: a `private const float
EmissiveScale = 1.5f` TUNE with an invariant-culture `EmissiveLiteral` beside it, and one branch in
each of `GetBillboardShader` and `GetCylindricalShader` that, in enhanced mode and on the `glow` arm
only, emits `col.rgb * 1.5` where the arm previously emitted `col.rgb`. `GetBiasShader` is
untouched, for the census reason above.

**The route is a colour scale, not EMISSION, and that was measured rather than assumed.** Both arms
are `render_mode unshaded`, and Godot 4.7 discards EMISSION there. A throwaway variant emitting
`EMISSION = col.rgb * 3.0;` and no colour scale rendered `w_lightglow` at mean luminance 132.84 over
the 120x120 halo rect, pixel-for-pixel the same as emitting nothing (132.84); the colour scale over
the same rect reads 146.86. The glow pass reads the HDR colour buffer, so the scale is what reaches
it. The experiment switch was removed before finishing.

Commands and results, all from the worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -Suite plane-shader-reuse -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200
(non-zero), engine errors clean, 0 unexpected lines.
`.\RunTests.ps1 -SkipUnits -SkipEngine`: PASS, 18 shot(s) hash-identical, zero movers (50.5 s,
awareness-only over the 50.0 s budget).

Original-mode byte identity used B11's method rebuilt as a throwaway: a static enumerator in
`SceneBuilder`'s constructor, armed by `CSVM_SCRATCH_SHADER_DUMP`, walking every reachable key of all
three generators and writing each key's `Shader.Code`. The baseline came from the committed tree
before the edit. `dump_orig_before.txt` and `dump_orig_after.txt` are both 12,996,256 bytes and
SHA-256 `B96DA68E61BE449B179A7647AC0B23F46BBCA45938D1F84D10B5C6198403951F`, identical.
`dump_enh_before.txt` is 12,579,488 bytes / `50DC662C95FC9091FCCFCD62AD79D3CC11FD0BE8742E818A9BFDB1C3521236C7`
and `dump_enh_after.txt` 12,580,064 bytes / `653CE30B7990A2900A3999A248193CFC838BA4FCAADA8933BCCB67296C918B9D`,
so the instrument was seen able to fail. The instrument was deleted before finishing.

Captures are `.\RunProbe.ps1 ... --det --mute` runs under `.scratch/c21/`, with labelled montages
beside them. C5's freecam default is frame-identical between the pre-C21 and post-C21 ORIGINAL
builds (0 of 921,600 pixels) and moves 12 pixels between the two ENHANCED builds, all of them inside
the flare sprites, which is the scope of the change stated as a measurement.

| Sample | original | enhanced |
|---|---|---|
| C5 `w_lightglow` poleflare halo, 120x120 at 380,110 | mean 132.84, max 177.4 | mean 146.86, max 211.0 |
| C1 `gen_flare_yellow` refinery flare halo, 160x160 at 560,220 | mean 141.53, max 251.4 | mean 161.81, max 255.0 |
| C5 street-level lit window, 8x8 at 92,544 | mean 109.76 | mean 162.94 |
| C5 street-level brick beside it, 8x8 at 124,544 | mean 12.81 | mean 23.94 |
| C1 `des_on` DESERT sign panel, 180x60 at 470,150 | mean 138.43 | mean 138.43 |

The window and wall rows are the disproof as a measurement: enhanced mode scales both by about the
same factor (x1.48 and x1.87), so the window does not hold its authored brightness, because it is
texels in a lit wall texture rather than an exempt surface. The sign row is the general
`lighting: false` arm, unchanged in both modes by design.

**Contract for C22.** The pixels that exceed 1.0 in the HDR colour buffer are exactly the glow-arm
sprites: `GetGlowMaterial`'s camera-facing flares (C5 448 nodes, C1 760) and the flare/fire/flame
cylindrical facades, each writing `col.rgb * 1.5` before the fog mix, so a fully fogged sprite still
falls back to fog colour and cannot bloom. Nothing else in the world exceeds 1.0, so a
`GlowHdrThreshold` of 1.0 blooms the light sources and nothing else. Where a flare's core was already
saturated the scale shows only in its falloff (C1's max clips at 255), which is what the tonemap in
C22 is expected to recover.

## C22 ☑ Environment glow + tonemap

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

**Verified.** The complete `.\RunTests.ps1` on the Wave C tree: 2796 units, 200 engine suites
(errors clean), 18 goldens hash-identical, all passing in 144.8 s. During the item: `dotnet build CSVM/CSVM.sln`: clean, 0 warnings, 0 errors.
`.\CheckCommentCaps.ps1 -Summary` and `.\CheckEncoding.ps1`: both clean over the whole tree.

The landed change is `Launcher.EnableGlowAndTonemap`, called from `SetupLighting`'s existing
enhanced-mode branch alongside `EnableWaterReflections`. Every value is a named TUNE constant:
`GlowHdrThreshold` 1.0 and `GlowBloom` 0 so only pixels the HDR buffer already carries above 1.0
bloom, matching C21's contract that the glow-arm sprites (`col.rgb * 1.5`) are the only such
pixels; `GlowIntensity` 0.9, `GlowStrength` 1.1 and `GlowBlendMode` Screen for an additive halo that
does not blow out a sprite's own core; `GlowHdrScale` 2.0 and `GlowHdrLuminanceCap` 8.0 so a
saturated flare core still separates from its falloff. `TonemapMode` is AgX, chosen over Filmic at
the controls: AgX recovered the C1/C4 day-chapter far-ridge washout (Wave B's known defect) into
real terrain colour and detail, where Filmic left the same ridge closer to a flat wash; `TonemapWhite`
is left at its default (AgX ignores it) and `TonemapAgxWhite` 6.0 / `TonemapAgxContrast` 1.0 hold the
recommended photorealistic-lighting range from the 4.7 docs with no push either way, since C5's night
city and C1/C4's daylight all read correctly at these defaults, `TonemapExposure` stays neutral at 1.0.

**The over-1.0 claim.** No HDR pixel-readback instrument exists, so this was verified by A/B: a
throwaway `CSVM_GLOW_TONEMAP_OFF` env-var gate (removed before landing) let the same enhanced build
run with `EnableGlowAndTonemap` skipped. At every sampled region that is not a glow-arm sprite (a
lit-window/wall cluster, a mid-tone building facade, an empty dark-sky patch, the C1/C4 far-ridge
terrain) the glow-off capture is pixel-for-pixel close to or identical to original mode adjusted only
by the already-landed lighting/shadow/SSAO/SSR terms, while the ring immediately around C5's moon
(the one glow-arm sprite in that capture) jumps from mean 17.75 (glow off, exactly the original-mode
value) to 20.98 (glow on) over a 20x20 px sample, showing the bloom is confined to the source. The moon disc
itself drops from mean 201.54/max 251.41 (glow off) to mean 173.35/max 195.80 (glow on), which is the
tonemap rolling off what was clipping, not the source dimming for no reason.

**The cockpit seam.** `CockpitOverlay.NewOverlay` duplicates `_env` after `SetupLighting` has already
called `EnableGlowAndTonemap` on it (`GameSession.BuildCockpitPasses` runs after the world's
Environment is fully configured), so the interior SubViewport's own copy carries the same
`GlowEnabled`/`TonemapMode`/every glow and tonemap value verbatim, and each viewport is tonemapped
once, inside its own pass, before the PremultAlpha composite: the composite blends two already
tonemapped images rather than re-applying a curve. A `--view=cockpit` capture in enhanced mode shows
no banding or hard edge at the canopy strut under a 5x zoom crop (`.scratch/c22/cockpit_enhanced_zoom_leftstrut.png`);
the same world/strut luminance sample B15 used reads world 114.12 / strut 123.39 in enhanced mode
against world 91.61 / strut 91.90 in original, a delta that (as in B15's own splitscreen measurement)
tracks different geometry sitting behind each rect rather than a mode-introduced seam, since no
banding shows under zoom. No CockpitOverlay code change was needed.

**Captures**, all `RunProbe.ps1 ... --det --mute` under `.scratch/c22/`, `$env:CSVM_DATA_ROOT="Z:\CSVM"`
set first: C5's default freecam spawn (night city, moon + lamps), the aircraft at C1's
`--pos=-6420,25,-3260 --direction=1,0,-0.3` dock-light pose (also exercises C24's SSR on the water),
C1's and C4's default freecam spawns (the horizon washout), a firing sequence
(`--fly --chapter=C1 --plane=player_bhawk --fire --shots=15`, picking the frame the muzzle flash
sprite is visible), and `--view=cockpit`. Each pair (original/enhanced, and enhanced with the
throwaway glow/tonemap gate on) reports 0 `ERROR` lines in its `.err` stream (a few WARNING lines
about a late `snd_police` SOUND_NODE appear at the off-mission scripted `--pos`, unrelated to this
item and present in every mode). Labelled montages: `montage_c5_city.png`, `montage_c1_horizon_tonemap.png`,
`montage_c4_horizon_tonemap.png`, `montage_c1_dock.png`, `montage_gunfight.png`, `montage_cockpit.png`.
The muzzle-flash region (30x25 px at the wingtip gun) reads mean 60.33/max 98.89 in original against
mean 77.37/max 125.43 in enhanced, confirming tracers/muzzle flashes bloom under the same pass.

**Perf**, C5 (heaviest), `RunProbe.ps1 --freecam --chapter=C5 --no-vsync --perf --det --mute
--frames=600`, three runs per configuration, `gpu_ms`/`frame_ms` medians over the last five 60-frame
windows (each run time-boxed with `-TimeoutSec 35` since `--perf` alone has no auto-quit):

| scenario | run 1 | run 2 | run 3 |
|---|---|---|---|
| original `gpu_ms` | 1.54 | 1.51 | 1.54 |
| enhanced, glow+tonemap off `gpu_ms` | 1.85 | 1.85 | 1.85 |
| enhanced, glow+tonemap on `gpu_ms` | 1.90 | 1.90 | 1.90 |

`frame_ms` stayed at 8.33-8.55 in every run (the 120 fps uncapped ceiling on this rig, well under the
16.7 ms 60 fps budget). Glow and tonemap together cost about 0.05 ms of GPU time on top of the
already-enhanced (shadows + omnis + SSAO + SSR) baseline, in line with the item's evidence that both
are negligible-to-low cost.

Goldens-only (`.\RunTests.ps1 -SkipUnits -SkipEngine`): PASS, 18 shot(s) hash-identical, zero movers,
same hashes before and after the throwaway verification gate was removed. Engine suite
`cockpit-overlay-pass` (`.\RunTests.ps1 -Suite cockpit-overlay-pass -SkipUnits -SkipGoldens`): 1
passed, 0 failed, engine errors clean. An eight-chapter enhanced `--freecam` sweep
(C1/C1B/C1C/C2/C2B/C3/C4/C5, `--det --mute --frames=5`) reported 0 `ERROR` lines in every `.err`
stream.

## C23 ☑ SSAO

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

**Verified.** The complete `.\RunTests.ps1` on the merged C21 + C23 + C24 tree: 2796 units, 200
engine suites (errors clean), 18 goldens hash-identical, all passing in 139.2 s. During the item:
`dotnet build CSVM/CSVM.sln`: clean, 0 warnings, 0
errors. A/B captures via `RunProbe.ps1 --graphics=enhanced --det --mute` (a `CSVM_SSAO_OFF=1`
env-var gate in `SetupLighting`, added for the off capture and removed before landing): C2's
hangar/house cluster shows visible contact darkening at wall bases and roof/wall junctions with
SSAO on, absent with it off; C5 (night) shows no perceptible difference at this world's already
near-black night ambient, since SSAO modulates ambient light and there is little of it to modulate
after dark. The aircraft against sky (`--fly --stage=empty --view=8`) shows no halo at the
fuselage/wing silhouette in a tight crop, on vs off. A 5-frame `--shots=5` sequence flown low over
C2 (`--fly --chapter=C2 --frames=90`) measured frame-to-frame mean absolute channel difference over
the building region at 38.1-38.8 with SSAO on against 38.2-38.9 with it off: indistinguishable, so
SSAO does not add shimmer detectable above this flight's own parallax at this pixel sampling; a
slower or static pan would isolate the SSAO term more cleanly but was not needed to clear the trap.
Perf (`--fly --chapter=C5 --no-vsync --perf --frames=240`, `graphics.mode`=original /
enhanced-SSAO-off / enhanced-SSAO-on, three runs each, last 60-frame window, `gpu_ms`/`frame_ms`
medians):

| scenario | original | enhanced, SSAO off | enhanced, SSAO on |
|---|---|---|---|
| 1 pane `gpu_ms` | 1.54 | 1.62 | 1.82 |
| 1 pane `frame_ms` | 8.41 | 8.45 | 8.40 |
| 4 pane `gpu_ms` | 1.74 | 1.80 | 1.90 |
| 4 pane `frame_ms` | 12.46 | 13.39 | 15.00 |

SSAO's own GPU cost is small (roughly 0.1-0.3 ms per viewport) and draw counts are identical
between the SSAO-off and SSAO-on enhanced columns (SSAO is a post-process pass, not extra
geometry); the shadow-map cascades already in enhanced mode account for most of the gap against
original. `frame_ms` stayed under the 16.7 ms 60 fps budget in every 1-pane run and in most 4-pane
runs; one 4-pane run (both SSAO-off and SSAO-on) spiked to 17-23 ms, consistent with a stray
`--freecam` Godot process left over from an earlier session sharing the hidden desktop (LOG-19)
rather than with SSAO, since the spike appeared in the SSAO-off column too. Goldens-only
(`.\RunTests.ps1 -SkipUnits -SkipEngine`): 18/18 shot(s) hash-identical, zero movers. Engine suite
`cockpit-overlay-pass` (`.\RunTests.ps1 -Suite cockpit-overlay-pass -SkipUnits -SkipGoldens`):
1 passed, 0 failed, engine errors clean, confirming the cockpit's duplicated Environment still
builds correctly with SSAO carried on it. Cockpit-pass decision: SSAO stays ON there, since
`CockpitOverlay.NewOverlay` duplicates the enhanced `_env` verbatim (B15) and the interior's own
creases (dash, girders) are exactly the kind of small-scale contact shading SSAO's detail term is
tuned for; no separate off-switch was requested or built.

## C24 ☑ SSR on water — evaluate, then ship or park

**Goal.** A verdict, with captures: does screen-space reflection on sea/water surfaces in enhanced
mode read well enough to ship, or is it parked with the reasons recorded?

**Evidence (confidence: lead-only).** Godot 4.6 rewrote SSR (less temporal instability, explicit
half/full-res modes); SSR reflects opaque geometry only and cannot reflect off-screen content
(verified against the docs during planning).

**Census result: water IS an identifiable population, and the engine already classifies it.**
`SceneBuilder.ClassifySurface` names a surface `"water"` from its texture name, and the collision
buckets are built from that; the same call identifies the shading population with no new plumbing.
Counted over each chapter's placed models joined to the textures they draw
(`.scratch/c24/census.ps1`, the read-only streaming census C21's script uses):

| Chapter | placed models | water polygon instances | water textures | models drawing water |
|---|---|---|---|---|
| C1 (day, lake and river) | 2,237 | 652 of 25,962 | `water1`, `water1_trans1/2`, `wakefront1`, `watersquirt` | 51 (50 `lighting: true`) |
| C1B (night sea) | 1,305 | 1,085 of 14,060 | `wtr00000`, `srf0001`, `wakefront1`, `watersquirt` | 154 (151 `lighting: true`) |
| C3 (island, harbour) | 1,901 | 1,571 of 21,554 | `wtr00000`, `watersquirt` | 348 (all `lighting: true`) |
| C5 (night city waterfront) | 2,851 | 704 of 39,796 | `wtr00000`, `watersquirt` | 164 (all `lighting: true`) |

Two properties make the handle usable. It is at most five materials per chapter, so the shader key
splits at material granularity with no per-polygon work. And essentially every water model is
authored `lighting: true`, so the population sits inside B11's `worldLit` arm, which is the only arm
that can carry a roughness at all.

Two watery-named families are NOT in it and stay out: C1's `river1`/`river2` (149 polygon instances)
and C3's `cliff1_watertrans*` (258). They are the shoreline and cliff transition sheets, and
widening the decoded classifier to take them would be an invention, not a decode.

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

**Verified.** The complete `.\RunTests.ps1` on the merged C21 + C23 + C24 tree: 2796 units, 200
engine suites (errors clean), 18 goldens hash-identical, all passing in 139.2 s.

The landed change is a `water` bit on `GetBiasShader`'s key (16384, next free bit now 32768) fed
from `ClassifySurface(texName) == "water"` through `BiasMaterial`, plus two TUNE constants
`WaterRoughness = 0.1f` / `WaterSpecular = 0.5f` with invariant-culture literals beside them. The
arm is `waterLit = worldLit && water`, so it exists only inside enhanced mode's lit world arm and
original mode never sets the bit. It emits `ROUGHNESS = 0.1; METALLIC = 0.0; SPECULAR = 0.5;` where
the matte arm emits 1.0/0.0/0.0. `GetMaterial`'s cache key is unchanged, because the class is a
function of `materialIndex`, which the key already carries.

In `Launcher.cs` the addition is one mode-gated call, `if (GraphicsMode.Enhanced)
EnableWaterReflections(_env);` at the end of `SetupLighting`, and the method it calls, placed at the
end of the lighting region after `EnableSunShadows`. It sets `SsrEnabled = true`, `SsrMaxSteps = 64`,
`SsrFadeIn = 0.15f`, `SsrFadeOut = 2.0f`, `SsrDepthTolerance = 0.2f`, from four `EnhancedSsr*`
constants declared beside the `EnhancedShadow*` ones. Nothing else in `SetupLighting` was touched.

Commands and results, all from the worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -Suite plane-shader-reuse -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200
(non-zero), engine errors clean, 0 unexpected lines.
`.\RunTests.ps1 -SkipUnits -SkipEngine`: PASS, 18 shot(s) hash-identical, zero movers, 37.8 s.
The 8-chapter enhanced `--freecam --det --mute --frames=15 --screenshot=` sweep exited 0 on all of
C1, C1B, C1C, C2, C2B, C3, C4 and C5 with zero error lines; C1's single warning is the
`snd_police` late-sound line `--mute` produces, and an original-mode run of the same command
produces it too.

Original-mode byte identity used C21's method rebuilt as a throwaway: a static enumerator in
`SceneBuilder`'s constructor armed by `CSVM_SCRATCH_SHADER_DUMP`, walking every reachable key of all
three generators and writing each key's `Shader.Code`, with a second variable
`CSVM_SCRATCH_SHADER_WATER` selecting the water dimension so both halves of the new bit are dumped.
The baseline came from the committed tree before the water bit was added.
`dump_orig_before.txt`, `dump_orig_after_w0.txt` and `dump_orig_after_w1.txt` are all 13,100,096
bytes and SHA-256 `5C020A703AD5CBA1C23397C194F73940FBF23AA2411E47C14AAB95004D80ADF0`, identical, so
original mode's shader text does not move for either value of the bit.
`dump_enh_before.txt` and `dump_enh_after_w0.txt` are both 12,684,096 bytes and
`3AA59A633F203CC29253B6058B7D5EB5B75683B3B6894870013ABDB20B573047`, so the bit does not leak into
non-water enhanced surfaces, while `dump_enh_after_w1.txt` is
`3CEBAF044C1CB170D3E8DF3E246F4307DFD2A311BD29EC1FBB6B36DC51216D19`, which differs, so the instrument
was seen able to fail. The instrument was deleted before finishing.

Captures are `.\RunProbe.ps1 … --graphics=enhanced --det --mute` runs under `.scratch/c24/`, with
labelled montages beside them (`montage_c3_sealevel.png`, `montage_c3_bank.png`,
`montage_altitude.png`). Four arms were captured at each pose: original, enhanced with matte water
(the committed HEAD), enhanced with the water bit and SSR off, and enhanced with both. The two
extra arms came from throwaway environment switches (`CSVM_SCRATCH_NO_SSR`, `CSVM_SCRATCH_NO_WATER`),
both removed before finishing.

**The water bit is the larger half of the change, and SSR the smaller.** At the C3 island pose at
40 m the water bit alone moves 52.42 % of pixels against the matte arm (mean 15.97, max 83): the sea
stops being a flat teal card and takes a sky gradient. SSR on top of it moves a further 37.28 %
(mean 2.98, max 33), and that share is the mirrored island under the shoreline.

**Where the reflection exists it is stable; the limit is screen space, and it is severe.** Over the
level pass, measured in 400x40 bands walking down from the shoreline at x=560, the mean absolute SSR
contribution is 12.20 at y=380, 7.70 at 420, 3.04 at 460, 1.97 at 500, 1.07 at 540, 0.22 at 580,
0.004 at 620 and 0.000 at 660. The reflection dies over about 240 px, exactly as the reflected
shoreline walks off the top of the frame, and the near half of every frame gets nothing. Frame to
frame it does not flicker: over 8 consecutive `--shots` frames of a banked pass at 90 m, the
SSR-minus-no-SSR mean luminance of the 400x140 rect under the shoreline is 5.641 with a standard
deviation of 0.333 (5.9 %), and the trend across the eight is monotone with the roll rather than
noisy; the level pass reads 3.538 with sd 0.116 (3.3 %). The whole-frame contribution over the same
eight frames holds at 28.24 % to 26.55 % of pixels with max fixed at 29 every frame.

**The reflection fades out with altitude.** At the same C3 heading with SSR on against SSR off, the
maximum channel delta over the frame falls 33 (40 m), 29 (100 m), 26 (200 m), 23 (400 m), 21 (800 m),
16 (1600 m), and the mean falls 2.98 to about 1.0. A first banked pass flown at 400 m confirmed it
from the cockpit's side: 9.17 % to 13.43 % of pixels touched, mean 0.33 to 0.42, max 24 to 26, which
on the touched pixels is about 3 levels of 255.

**Verdict at the time: ship, mode-gated.** The census gives a decoded handle rather than an invented
one, the prototype is one shader bit and five Environment properties, original mode is byte-identical
and all 18 goldens are unmoved, and at and near sea level the reflected shoreline is both readable and
temporally stable. The SSR values are Godot's own defaults in shape and were not pushed to
manufacture a reflection: the alternative to shipping is a glossy sea that reflects only the sky,
which is strictly less than what the marched rays deliver at no authoring cost.

**Verdict, superseded.** E46 measured the shipped pair, and four alternatives, against the original's
water luminance at three poses once E44 had put the mission's own sky behind the reflection. Every
pair that kept a visible reflection read further above the original than this verdict's captures
showed, because the mission's authored sky is bright by day and the reflection contributes a floor
the roughness/specular pair cannot reach under regardless of setting by night. The water bit and its
SSR call are removed; see E46 for the measurement and the park.

**What the chosen poses cannot show.** They cannot show a reflection at cruise: everything readable
here is below roughly 200 m, and above 400 m the effect is under a level of 255 on average, so a
player who never descends will not see the feature at all. They cannot show the aircraft reflected
in its own wake, because it never is: the water rect directly under the aircraft during the banked
pass moves at most 4 of 255 with SSR on, since the reflected airframe is off the top of the frame.
They cannot say anything about frame cost, which D31 owns and which SSR is the first item in this
plan to add per viewport rather than per scene, so the 4-pane splitscreen case is genuinely
untested here. And they cannot show broken water: the surfaces are flat planes with no wave normals,
so where a reflection lands it is a hard mirror, which is a look the original never had.

# Wave D — Hardening and record

## D31 ☑ Performance gate

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

**Verified.** <pending orchestrator run> All commands from the worktree with
`$env:CSVM_DATA_ROOT="Z:\CSVM"`.

`dotnet build CSVM/CSVM.sln`: clean, 0 warnings, 0 errors (no TUNE constant moved, see the lever
decision below). `.\CheckEncoding.ps1`: no mojibake.

`.\RunTests.ps1 -Perf -SkipUnits -SkipEngine -SkipGoldens` (original) and the same with
`-Graphics enhanced`: both PASS, 6/6 scenarios, 300 sim frames x 3 launches per scenario (first
discarded), history appended. Medians of the kept windows, render_cpu_ms / gpu_ms / draws:

| Scenario | original render_cpu / gpu / draws | enhanced render_cpu / gpu / draws |
|---|---|---|
| empty-stage | 0.15 / 0.11 / 119 | 0.24 / 0.29 / 217 |
| c1-flight | 0.42 / 0.375 / 380.8 | 0.705 / 0.47 / 1201 |
| c2b-water | 0.285 / 0.10 / 115 | 0.39 / 0.37 / 160 |
| c4-terrain | 0.66 / 0.23 / 978 | 1.115 / 0.54 / 2550 |
| c2m02-hollywood | 0.635 / 0.30 / 675.6 | 0.925 / 0.55 / 1305.25 |
| c5-city | 1.055 / 1.51 / 1235 | 1.425 / 1.945 / 2055 |

Every one of these is a fraction of a millisecond to low single digits, nowhere near the 16.7 ms
line at one pane; the draw-count columns show the stack's real cost (roughly double the draws on
the heavier scenes) without any frame-time budget pressure yet.

`.\RunTests.ps1 -Hitch -SkipUnits -SkipEngine -SkipGoldens`, three runs each mode, and the same
with `-Graphics enhanced`: every run PASS, clean stays silent (0 hitch lines) and the injected run
trips exactly once, in both modes, every time:

| Run | original frame_ms / wall | enhanced frame_ms / wall |
|---|---|---|
| 1 | 64.82 ms / 18.2 s | 68.91 ms / 19.0 s |
| 2 | 62.84 ms / 18.1 s | 63.80 ms / 19.2 s |
| 3 | 63.81 ms / 18.1 s | 67.42 ms / 21.7 s |

All six runs land well inside the 30 s hitch budget (worst 21.7 s), so no budget entry moved.

**Targeted probes** (`RunProbe.ps1`, `--det --mute --perf --no-vsync --frames=600`, screenshot
ending the run at exactly sim_frame=600, three runs per cell, median of each run's per-window
median with the first perf window dropped for shader-compile warmup): `c4-terrain` and `c5-city`
are the two heaviest chapters already in the perf manifest; C3 stands in for the water/SSR arm
with the golden manifest's own `c3-island` pose. Each cell flew `--fly --plane=player_bhawk`
over the pose below, held level with `--hold=0,0,0,0.6`, at 1 pane and again with `--players=4`:

- C4: `--chapter=C4 --pos=-3330,958,-9181 --direction=-0.682,-0.2,0.731`
- C5: `--chapter=C5 --pos=-9256,178,-3155 --direction=-0.588,-0.1,-0.809`
- C3: `--chapter=C3 --pos=-4518,400,-2015 --direction=-0.35,-0.35,-1`

| Chapter | Panes | Mode | render_cpu_ms | gpu_ms | frame_ms |
|---|---|---|---|---|---|
| C4 | 1 | original | 0.80 | 0.74 | 8.33 |
| C4 | 1 | enhanced | 1.37 | 0.59 | 8.37 |
| C4 | 4 | original | 1.12 | 0.42 | 10.40 |
| C4 | 4 | enhanced | 1.58 | 0.66 | 15.04 |
| C5 | 1 | original | 1.10 | 1.54 | 8.35 |
| C5 | 1 | enhanced | 1.49 | 1.93 | 8.36 |
| C5 | 4 | original | 1.06 | 1.69 | 11.00 |
| C5 | 4 | enhanced | 1.44 | 2.03 | 13.60 |
| C3 | 1 | original | 1.07 | 0.59 | 8.37 |
| C3 | 1 | enhanced | 1.69 | 0.53 | 8.41 |
| C3 | 4 | original | 0.31 | 0.34 | 9.15 |
| C3 | 4 | enhanced | 0.50 | 0.60 | 12.51 |

C3's enhanced 4-pane cell carries one outlier run of the three (24.13 ms against 12.03 and
12.51 ms on the other two, with render_cpu_ms/gpu_ms unremarkable on that same run); the median
already absorbs it, and nothing in the render/GPU terms attributes it to the water or SSR path,
so it reads as ordinary machine noise (verification PERF-5, METHOD-3) rather than a regression.

**Reading the sim-clock trap.** Every 1-pane cell floors at 8.33-8.41 ms regardless of chapter or
mode, which is this machine's own ~120 Hz external pacing surviving `--no-vsync` (docs/cli.md's
`--no-vsync` entry measured the same floor on C4). Under `--det` the sim clock advances exactly
one step per rendered frame with no physics catch-up to fall behind on, so every frame_ms above
that floor is real per-frame cost, not a physics-bound scene lagging wall time. The four-pane
numbers clear the floor by 0.78-2.65 ms in original mode and 4.10-6.67 ms in enhanced mode, and in
every chapter the enhanced-vs-original increment at four panes (2.60-4.64 ms) is bigger than the
render_cpu_ms/gpu_ms readings account for on their own (those stay under 2.1 ms in every cell
measured), so the
GPU/render-server terms this build reports should be read as a per-viewport floor, not the whole
splitscreen GPU bill, and `frame_ms` (which already sums every pane's cost into one wall
measurement) is the number the 60 fps / 16.7 ms line is judged against.

**Lever decision: nothing moves.** The worst measured cell is C4 at 4 panes enhanced, 15.04 ms
against the 16.7 ms line, a 1.66 ms (9.9 %) margin; C5 clears it by 3.10 ms (18.6 %) and C3 by
4.19 ms (25.1 %, ignoring the one noise outlier above). None of the three heaviest scenes exceeds
the budget, so per the plan's own order (SSAO quality, then shadow max distance, then SSR steps)
nothing needs reducing. SSAO's project-level quality/half-res settings stay at Godot's defaults
(no `rendering/environment/ssao/*` override exists in `project.godot`), which is mode-neutral
already: `SsaoEnabled` is only set true inside `Launcher.SetupLighting`'s `GraphicsMode.Enhanced`
branch, and original mode never reaches that code path at all. `EnhancedShadowMaxDistance` (`Launcher.cs`),
`WeatherRig.EnhancedFogRangeScale` and `EnhancedSsrMaxSteps` (`Launcher.cs`) are unchanged from
their committed values. C4 is the tightest of the three and is worth re-measuring first if a
heavier world or a fifth splitscreen pane ever lands, but it is not a disproof today.

**Budgets: unchanged.** The perf stage carries no budget by design (records, does not judge,
`analysis/verification-budgets.json`); the hitch stage's existing 30 s budget covers the enhanced
runs measured above (worst 21.7 s) without a new lane or a loosened figure, so no entry moved in
`analysis/verification-budgets.json` or `analysis/engine-suite-weights.json`.

## D32 ☑ Divergence documentation, final golden sweep, optional enhanced goldens

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

**As landed.** The divergence is recorded in `docs/architecture.md` under "Rendering: the enhanced
graphics mode (a documented divergence)" (placed beside "Cross-module conventions", since
`architecture.md` has no single existing section for rendering as a whole): the config key and
flag, what enhanced mode changes item by item, the byte-identity proof (the per-item shader-key
dumps plus the 18 goldens, both named as the instruments), the two recorded disproofs (lit windows
are texels in lit walls; the `lighting: false` bit's general arm is half non-luminous), and the
open TUNE judgements. `SceneBuilder.cs`, `GraphicsMode.cs`, `WeatherRig.cs`, `WorldLights.cs`,
`CockpitOverlay.cs`, `SplitScreen.cs` and `Launcher.cs` each already carried their own
enhanced-mode paragraph from the items that landed them; each now also points back at the new
section, and `SceneBuilder.cs`'s entry gained the `docs/org/vertexLighting.md` cross-link C21's
census depends on that it had not carried before. `PROJECT_CONTEXT.md`'s flag-index and
`src/Utils/` counts were re-measured rather than trusted: `docs/cli.md`'s flag index carries 146
unique `--` flags today (the day-to-day table 30 of them), and `CSVM/src/Utils/` holds 16 `.cs`
files; both lines are updated to the measured counts.

**The menu-plan handoff.** `docs/PLAN-menu-presentations.md` is not in this worktree, so the note
for its authors lives here instead: the options menu exposes `graphics.mode` through the menu
plan's own options store; every reader in this codebase consults the resolved
`GraphicsMode.Enhanced` boolean only, so that layer slots in without touching them.

**Final golden sweep.** `$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -SkipUnits -SkipEngine` on
this tree: PASS, 18 shot(s) hash-identical, 44.4 s (budget 50.0 s), gpu NVIDIA GeForce RTX 5080 /
1.4.341. `git diff --stat -- analysis/goldens/manifest.json` and `git status --short --
analysis/goldens/manifest.json` both report nothing: the manifest is unmodified in the working
tree, so the PASS above is not GOLD-9's stale-regen false green.

**Enhanced goldens, prepared not pinned.** Two candidates, one day and one night, each the
existing golden's own camera args plus `--graphics=enhanced`, captured as manual
`.\RunProbe.ps1` shots (not through `-RegenGoldens`) under
`.claude/worktrees/enhanced-graphics/.scratch/d32/`: `c1-waterfall-original.png` /
`c1-waterfall-enhanced.png` and `c5-city-night-original.png` / `c5-city-night-enhanced.png`, with
labelled side-by-side montages `montage_c1_waterfall.png` and `montage_c5_city_night.png` (built
with a local copy of B11's `montage.ps1`). The original-mode twin of each reproduced its golden
hash exactly (`6410c3bdaf263124994aeaf0da892f0d`, `652069b267ba51789d5b5bf42d839651`), confirming
the pose is right before trusting the enhanced hash beside it. At the captures: the C1 waterfall
cliff face picks up real shadow and the lake darkens under it; the C5 skyline gains lit
skyscraper silhouettes against the shoreline that the original's flat night card does not draw.
Neither manifest entry below is written into `analysis/goldens/manifest.json`: the user declined
to pin them until the TUNE values settle at the controls, since every retune would move both
hashes. They stay here so the pin is one `-RegenGoldens` from this record once the values hold.

```json
{
  "name": "c1-waterfall-enhanced",
  "frame": 120,
  "hash": "11f49c0d974ccbc27fd4678133a62fe8",
  "exercises": "Enhanced mode's lit-world shading, shadow-mapped cliff and darkened water over the day waterfall pose, run with graphics=enhanced beside its original twin.",
  "args": ["--freecam", "--chapter=C1", "--pos=-7720,60,-3380", "--lookat=-7868,40,-3449", "--graphics=enhanced", "--det", "--mute"]
}
```

```json
{
  "name": "c5-city-night-enhanced",
  "frame": 120,
  "hash": "b58d2b796c10022ae57e731bd044b681",
  "exercises": "Enhanced mode's authored-sunlight sun, shadow maps and real omni lights over the night city skyline, run with graphics=enhanced beside its original twin.",
  "args": ["--freecam", "--chapter=C5", "--pos=-9256,178,-3155", "--direction=-0.588,-0.1,-0.809", "--graphics=enhanced", "--det", "--mute"]
}
```

**Verified.** <pending orchestrator run> — this item's own sweep and manifest-diff proof are
above; the plan's landing gate is the complete `.\RunTests.ps1` the orchestrator runs once D31
also lands.

# Wave E — At-the-controls findings from the Wave C montages

The user read the Wave B/C montages and named three things the instruments had not: shadows
mixed into the fog ramp, C5 too bright with light-grey water, and unique buildings standing out
past the clutter fade. Each is an enhanced-mode-only change; original mode and the 18 goldens stay
byte-identical, proved per item as before.

## E41 ☑ Shadows end before the fog ramp, not inside it

**Goal.** In enhanced mode the sun's shadows fade out before the fog ramp begins, so a shadow is
never seen dissolving into haze.

**Evidence (confidence: traced).** `WeatherRig.ApplyEnhancedLighting` sets
`DirectionalShadowMaxDistance` to the pushed fog FAR (B13), so shadows run through the whole ramp,
which is exactly the range the user saw them mixed with fog.

**Approach.** Set the max distance from the pushed fog NEAR instead (the point where the ramp
starts), with `DirectionalShadowFadeStart` (TUNE) so the last cascade fades before the ramp rather
than cutting; keep the Launcher fallback for a session with no weather.json in step. Confirm the
split fractions still put the near cascades where the aircraft's shadow lives.

**Model recommendation.** medium, low effort.

**Verify.** C1 and C4 horizon captures: no shadow visible inside the ramp; the hangar and
aircraft shadows unchanged near the camera. Goldens zero movers.

**Verified.** <pending orchestrator run>

`dotnet build CSVM/CSVM.sln`: clean, 0 warnings, 0 errors.
`$env:CSVM_DATA_ROOT="Z:\CSVM"; .\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`: engine
PASS, 1 suite run of 200, engine errors clean, 0 unexpected lines.
`.\RunTests.ps1 -Suite sun-orientation -SkipUnits -SkipGoldens`: engine PASS, 1 suite run of 200,
engine errors clean. `.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s)
hash-identical, zero movers.

An 8-chapter `--freecam` fog census (`--graphics=enhanced --det --mute --frames=5
--screenshot=...`, reading each session's own `weather [...]: ... shadows to N m` line) gives the
real per-zone pushed-near distances the rule now resolves to: C1/C1B/C1C/C2B/C3/C4 (authored near
1000 m) at 2000 m, C2 (authored near 2100 m) at 4200 m, C5 (authored near 1500 m) at 3000 m. The
smallest, 2000 m, is never close to the aircraft's own shadow range (a few metres to a few hundred),
so no floor clamp was added; the same log line read 8000 m for C1's zone2 before the change and
2000 m after, confirming the rule moved from the pushed FAR to the pushed NEAR.

Captures (before on the committed tree, after on this edit, both `--graphics=enhanced --det
--mute`), under `.scratch/e41/`: C1 and C4 default freecam horizons, the C2 hangar/house cluster,
the `c1-waterfall` golden pose (`--pos=-7720,60,-3380 --lookat=-7868,40,-3449`, ~164 m from the
cliff), and the same cliff viewed from ~3009 m out (`--pos=-5162,406,-2186`, same `--lookat`,
`--no-fog` added to separate the shadow term from atmospheric haze per SHOT-4). All runs reported 0
engine ERROR lines (one WARNING about a late `snd_police` SOUND_NODE at the off-mission scripted
pose, present identically before and after, is the same unrelated noise other items in this plan
recorded at the same pose).

The wide horizon and hangar-cluster captures are pixel-identical before/after (0 of 300+ sampled
pixels differ): at C1/C4/C2's default freecam distance and this fog opacity, the shadow contrast
the fix removes is already crushed by the haze in a plain screenshot, so SHOT-3 applies and the
census log line above is the state-log evidence for the change itself. The `c1-waterfall` pose is
also pixel-identical (mean luma 45.37 before and after over a 60x60 near-camera patch), confirming
the aircraft/hangar-scale shadow at close range is unchanged, matching this pose's golden hash
staying unmoved. The `--no-fog` far-cliff pose is NOT identical: with the haze term removed, the
shadow's edge visibly recedes at ~3009 m (past the new 2000 m cutoff for that zone, inside the old
8000 m one), and the strongest-diff 20x20 patch on the water brightens from mean luma 70.95 to
75.46 as the shadow lifts off it.

| pose | distance from feature | mean luma before | mean luma after |
|---|---|---|---|
| `c1-waterfall` (near-camera cliff shadow) | ~164 m | 45.37 | 45.37 |
| far-cliff, `--no-fog` (mid-ramp water patch) | ~3009 m | 70.95 | 75.46 |

Montages: `montage_c1_horizon.png`, `montage_c4_horizon.png`, `montage_c2_buildings.png`,
`montage_waterfall_near.png`, `montage_farcliff_nofog.png`, under
`.claude/worktrees/eg-e41/.scratch/e41/` and copied to
`.claude/worktrees/enhanced-graphics/.scratch/e41/`.

## E42 ☑ C5 reads too bright in enhanced mode, its water a light grey

**Goal.** C5's night city reads as night in enhanced mode, and its water reads dark, without
inventing a value the data does not carry.

**Evidence (confidence: direction-sound, mechanism to establish).** B12 recorded that C5's zone
authors a day-level SUNLIGHT pair (1.5 / 0.5), so the enhanced sun and ambient light the city like
noon while the original's darkness comes from FOG_COLOR and the art; C24's water bit then gives the
sea a sky-gradient specular (52 % of C3's sea pixels moved from the bit alone), which on a night sky
reads light grey. Which term dominates C5's brightness (sun, ambient, or the water specular) is not
yet measured.

**Approach.** Measure first: C5 captures with the sun energy, the ambient energy, and the water
bit each disabled in turn through a throwaway gate, sampling the water and a wall. Then find the
decoded handle for "this zone is night" (candidates: the zone's FOG_COLOR luminance, the skydome's
night texture selection, the CLOUD_COVER colours, the mission's own time-of-day field if one is
decoded); scale the enhanced energies by it as a named TUNE, or clamp the water specular on a dark
sky, whichever the measurement points at. A rule that is only "C5 is special" is not acceptable.

**Model recommendation.** high, an investigation with a fidelity judgement.

**Verify.** C5 night reads dark with its lamps and lit windows still reading as sources; C1B
night sea reads dark; day chapters unchanged. Goldens zero movers.

**As landed.** Two writes, both in `WeatherRig` and both gated on one new pure predicate.
`IsNightZone` reads the zone's authored `FOG_COLOR` as Rec.709 luminance against
`NightFogLuminance` (0.25, TUNE). `EnhancedEnergies` then caps a night zone's authored pair at
`NightDiffuseCap` 0.6 and `NightAmbientCap` 0.15, the install's own night pair, as a ceiling rather
than a replacement, so a zone already authored dimmer keeps its values. `ApplyEnhancedSunAndEnv`
takes a `night` flag and switches the Environment's `ReflectedLightSource` between `Disabled` and
`Bg`, because the glossy water's specular comes off the **placeholder** procedural sky rather than
the mission's dome and so ignores both energies. The zone log line gains
`; night zone, energies capped`. `SceneBuilder`'s water constants and `EnableWaterReflections` were
measured and left alone.

**The measurement, and which term it blamed.** Six arms per pose, three C5 poses, through a
throwaway `CSVM_SCRATCH_E42` gate that wrote the sun energy to 0, the ambient energy to 0, the water
roughness/specular to the matte 1.0 / 0.0, or skipped the SSR block. Mean Rec.709 luminance
(0-255) over fixed rects; the gate was removed before finishing.

| Pose / region | original | enhanced | sun 0 | ambient 0 | matte water | no SSR |
|---|---|---|---|---|---|---|
| waterfront, near water | 37.45 | 60.44 | 49.48 | 33.12 | 52.84 | 60.44 |
| waterfront, far water | 37.43 | 86.61 | 80.52 | 33.39 | 52.68 | 89.56 |
| waterfront, city blocks | 6.24 | 10.34 | 7.42 | 6.12 | 10.34 | 10.34 |
| waterfront, sky | 7.54 | 9.80 | 9.80 | 9.80 | 9.80 | 9.80 |
| golden pose, distant skyline | 3.19 | 22.44 | 21.07 | 6.33 | 22.44 | 22.44 |
| golden pose, near tower wall | 32.35 | 56.33 | 33.30 | 45.04 | 56.33 | 56.33 |
| golden pose, lit windows | 37.63 | 61.71 | 38.41 | 49.05 | 61.71 | 61.71 |
| golden pose, horizon water | 0.00 | 41.26 | 39.44 | 5.78 | 7.79 | 51.75 |
| spawn, rooftops | 19.09 | 26.86 | 19.48 | 17.51 | 26.86 | 26.86 |
| spawn, aircraft | 63.49 | 72.02 | 38.62 | 60.20 | 72.02 | 72.02 |

The ambient is the term that carries the grey: it is nearly all of the distant skyline (22.44 to
6.33) and of the water (41.26 to 5.78), while the sun carries the near lit wall (56.33 to 33.30).
SSR contributes nothing to the complaint and is slightly darkening. The water bit is a real second
term (the far water loses 34 without it), and the two together are why the energy cap alone left
the water at 76.99: the glossy specular is fed by the background sky, not by
`AmbientLightEnergy`, which is what the second write addresses.

**The handle, and what every chapter resolves to under it.** `FOG_COLOR` luminance over all 212
`ZONE*`/`SW_ZONE*` blocks (`docs/org/weather.md` carries the table). Night runs 0.0000 to 0.0942,
day 0.6900 to 0.8461, nothing in the gap.

| Chapter | zones | fog luminance | night? | authored diffuse / ambient | resolved enhanced sun / ambient |
|---|---|---|---|---|---|
| C1 | ZONE1, ZONE2 | 0.6900 | no | 1.2 / 0.25 (1.5 / 0.2 in two missions) | 1.28 / 0.45, unchanged |
| C1B | ZONE1, ZONE2 | 0.0942 | yes | 0.6 / 0.15 | 0.64 / 0.27, cap not binding |
| C1B M03 | ZONE1 | 0.0942 | yes | 0.65 / 0.35 | 0.64 / 0.27, capped |
| C1C | ZONE1, ZONE2 | 0.6900 | no | 0.4 / 0.6 and 2.0 / 0.6 | unchanged |
| C2 | ZONE1 | 0.8461 | no | 1.1 / 0.5 | 1.18 / 0.90, unchanged |
| C2 | ZONE2 | 0.6900 | no | 0.4 / 0.6 to 2.0 / 0.6 | unchanged |
| C2B | ZONE1, ZONE2 | 0.6900 | no | 0.4 / 0.6 and 2.0 / 0.2 | unchanged |
| C3 | ZONE1 | 0.7900 | no | 1.5 / 0.3 | 1.61 / 0.54, unchanged |
| C3 | ZONE2 (in-cloud, 9-10 km) | 0.0942 | yes | 1.5 / 0.3 | 0.64 / 0.27, capped |
| C4 | ZONE1, ZONE2 | 0.7529 | no | 1.5 / 0.5 | 1.61 / 0.90, unchanged |
| C5 | ZONE1, ZONE3 | 0.0000 / 0.0627 | yes | 1.5 / 0.5 | 0.64 / 0.27, capped |

⚠ **The skydome is not a usable handle and was rejected on the data**: C1 and C4 are day missions
that draw a moon and a star field (`docs/formats/weather.md`), so the dome's night art does not
separate the populations.

**Verified.** <pending orchestrator run>

Commands, all from the item's worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build --filter "FullyQualifiedName~SunlightEnergyTests"`:
7 passed, 0 failed (three new facts: the day-level pair under a black sky capping to the night
pair, the cap behaving as a ceiling, and the install's own C5/C4 fog colours falling on opposite
sides of the separator).
`.\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200 (non-zero),
engine errors clean, 0 unexpected lines.
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers, 42.1 s.

Captures are `.\RunProbe.ps1 … --det --mute` runs under `.scratch/e42/`, three arms per pose
(original, enhanced before the rule, enhanced after it), the "before" arm taken through a second
throwaway switch that suppressed the predicate. Labelled montages beside them:
`montage_c5_water.png`, `montage_c5_citynight.png`, `montage_c5_spawn.png`, `montage_c1b_night.png`,
`montage_c1_day.png`, `montage_c2_day.png`, `montage_c4_day.png`.

| Pose / region | original | enhanced before | enhanced after |
|---|---|---|---|
| C5 waterfront, near water | 37.45 | 60.44 | 29.11 |
| C5 waterfront, far water | 37.43 | 86.61 | 29.23 |
| C5 waterfront, city blocks | 6.24 | 10.34 | 5.31 |
| C5 golden pose, distant skyline | 3.19 | 22.44 | 12.52 |
| C5 golden pose, near tower wall | 32.35 | 56.33 | 33.34 |
| C5 golden pose, lit windows | 37.63 | 61.71 | 37.43 |
| C5 golden pose, horizon water | 0.00 | 41.26 | 4.91 |
| C5 spawn, rooftops | 19.09 | 26.86 | 15.09 |
| C5 spawn, aircraft | 63.49 | 72.02 | 45.71 |
| C1B/IA1 night sea, near | 17.74 | 64.70 | 56.60 |
| C1B/M03 night sea, near | n/a | 66.15 | 43.91 |

C5's lit windows land within 0.2 of the original's level and the tower wall within 1.0, so the
lamps and lit windows still read as sources while the world around them stops reading as noon. The
waterfront's water goes from a light grey above the original to a dark sea slightly below it.
C1, C2, C3 and C4's enhanced frames are **byte-identical** before and after (SHA-256 on the PNGs),
which is stronger than "within noise": the predicate never fires on a day fog.

**What this does not fix.** C1B's enhanced night sea is still far brighter than the original
(56.60 against 17.74). The cap cannot reach it, because C1B already authors the night pair the cap
is made of, so only the reflection-source write applies there. The residual belongs to terms this
item does not own: the 2x fog-range push (`E41`), the glow and AgX tonemap (`C22`), and the
placeholder procedural sky standing in for the mission dome behind the horizon. That last one is
the single largest remaining lever and has no item yet.

## E43 ☑ The clutter building fade reaches as far as the pushed fog

**Goal.** In enhanced mode the clutter populations (city blocks, trees) fade at the same pushed
distance as the fog, so unique buildings no longer stand alone past the clutter line.

**Evidence (confidence: traced).** The clutter far fade is the authored metres scaled by
`EffectsLevel.ResolveClutterFadeScaleSq` into the `csky_clutter_fade` global
(`Launcher.cs` ~518-522), independent of the fog range; B13's 2x fog push left it at the authored
distance, which is what the user saw.

**Approach.** In enhanced mode multiply the clutter fade scale by `EnhancedFogRangeScale`
(squared, since the global is a squared distance) at the one write site, logged on the same
`clutter fade:` line; audit any other distance-gated population that should follow the fog
(the map-edge extender, the far-field AI plant, the cloud clusters) and state which do and why.
Re-measure C5 at 4 panes with `--perf`, since more clutter instances draw.

**Model recommendation.** medium, low effort.

**Verify.** C5 and C2 horizon captures: clutter blocks reach the fog line; instance counts logged
before and after. Goldens zero movers.

**As landed.** `WeatherRig.EnhancedFogScale()` exposes `EnhancedFogRangeScale` (1 in original mode)
as a plain scalar; `EffectsLevel.ClutterFadeScaleSq`/`ResolveClutterFadeScaleSq` take it as a
`fogScale` parameter (default 1, so every existing caller is untouched) and DIVIDE by it squared:
the shader multiplies the scale into the squared camera distance, so a larger scale fades sooner,
and pushing the fade out by the fog factor means shrinking the scale by its square (the first
landing multiplied and made the towers vanish at half the distance; the orchestrator caught it on
the montage). `Launcher._Ready` resolves `GraphicsMode` before the
clutter-fade write (it used to run after) and passes `WeatherRig.EnhancedFogScale()` in, so the one
write site scales with the mode already resolved. `MapEdgeExtender`'s own clutter continuation
reads the same `csky_clutter_fade_scale_sq` global as `Clutter.cs`, so it follows with no code
change of its own.

The audit: `Clutter.cs` and `MapEdgeExtender`'s clutter both read the one global above, so both
follow. `Effects/FogVolumeClutter` (the `fvol` ambient cloud field) carries its own authored
`far_fade_range` per kind, explicitly exempt from distance fog by design (`fog: false`, "carry
`far_fade_range` instead") and unrelated to the "unique buildings stand out" complaint the user
raised, so it is left alone. `WorldBuilder.CloudClusters` (placed `cloudparent` subtrees) are gated
by camera altitude, not distance, so there is nothing to scale. `WorldLights`' 900-1500 m fade is
budgeted (`MaxActive` slot ranking against the nearest viewer), not fog-bounded, so it stays as is.
The zone gate carries no distance at all (B13). The far-field AI plant's 1 km branch is a gameplay
physics simplification, not a visual population, so it stays untouched.

**Verified.** <pending orchestrator run>

C2 (`--freecam --chapter=C2 --pos=-5722,186,-3457 --direction=-0.438,-0.15,-0.899 --graphics=enhanced
--det --mute`): `clutter fade:` `scale_sq` 1 before, 0.25 after; `clutter uv lattice: placed=46752`
unchanged. C5 (`--pos=-9256,178,-3155 --direction=-0.588,-0.1,-0.809`, same flags): `scale_sq` 1
before, 0.25 after; `placed=177291` unchanged. The C5 horizon montage shows the clutter towers
reaching the fog line beside the unique bridge towers. A same-pose original-mode
capture is md5-identical before and after; the two enhanced-mode captures differ (C2 84,656/921,600
px, C5 60,651/921,600 px), confirming the fade moved and nothing else did.

C5 `--perf` (`--freecam --det --mute`, three runs per configuration, mean gpu_ms over the first
eight steady windows past sim frame 180; frame_ms holds at the 120 fps cap of 8.3-8.6 in every run):
1 pane 2.085 ms before, 2.184 ms after; 4 panes (`--players=4`) 2.185 ms before, 2.187 ms after.
Both deltas sit inside the run-to-run spread (1.89-2.24 ms) seen across all twelve runs, so the
uniform-only change costs nothing measurable; flagged provisional under the shared desktop's
contention.

`.\RunTests.ps1 -Suite clutter-determinism -SkipUnits -SkipGoldens`: PASS, 1/200, engine errors
clean. `.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical,
zero movers, on an RTX 5080. The complete `.\RunTests.ps1`: PASS, 2802 units, 200 engine suites
(errors clean), 18 goldens hash-identical, 169.7 s total. `dotnet build CSVM/CSVM.sln`: clean, 0
warnings, 0 errors.

## E44 ☑ The enhanced Environment's sky is the mission's dome, not the placeholder procedural sky

**Goal.** In enhanced mode the Environment's background and reflection source are the mission's
own horizon dome (or a colour derived from it), so specular and any sky-sourced term read the
authored sky rather than Godot's placeholder procedural gradient.

**Evidence (confidence: traced).** E42 measured that the glossy water's specular comes off the
Environment's background sky, which is a `ProceduralSkyMaterial` placeholder rather than the
gamez dome `WorldBuilder.BuildHorizon` draws as geometry; under a night sky it had to be disabled
outright, and it is the largest remaining reason C1B's night sea reads 56.6 against the
original's 17.7. Day zones still reflect the placeholder's daylit gradient.

**Approach.** Establish what the Environment's background is today in both modes and what the
faithful path relies on (WorldBuilder forbids a colour-grading stage on the dome colour; the dome
is geometry, so the background is likely never seen). In enhanced mode only, either (a) set the
background to a colour sampled from the zone (the FOG_COLOR is the horizon colour the dome fades
into; a sky-top colour may exist in the dome data) with `ReflectedLightSource` following it, or
(b) render the dome into a `PanoramaSkyMaterial` at build time if a decoded dome texture exists
per zone. Prefer (a) unless (b) is cheap and clearly better at the controls. Re-enable the night
reflection source if the new sky makes it read right, keeping E42's cap.

**Model recommendation.** high, a fidelity judgement over a decode.

**Verify.** C1B and C5 night seas darker toward the original; C3 and C1 day water reflects a sky
that matches the dome's colour; original mode untouched, goldens zero movers.

**What the background was.** In both modes the Environment was built with `BGMode.Sky` over a
`Sky { SkyMaterial = new ProceduralSkyMaterial() }`, Godot's default day gradient, with
`AmbientSource.Sky` and energy 0.9 (`Launcher.SetupLighting`). Enhanced mode then took the ambient
off the sky per zone (B12) and, on a night zone, took the reflection away outright (E42). The
faithful path never SHOWS that sky: the dome is gamez geometry drawn camera-centred over the
background at every altitude. What the sky was is the reflection and, before B12, the ambient.

**What it is now.** Option (a), and no new fidelity TUNE. Enhanced mode's Environment carries a
flat `PanoramaSkyMaterial` (8x4 texels, `Rgbaf`) whose one colour is the flown zone's authored
`FOG_COLOR`, written in linear (`WeatherRig.WriteSkyColor`, called from `ApplyEnhancedSunAndEnv`,
so it reaches the cockpit overlay's registered copy on every zone change).
`Launcher.UseMissionSky` builds the same material with the zone default 0.69 grey, for a world
that applies no zone at all, and moves the ambient to `AmbientSource.Color` there rather than
leaving a flat sky to override the authored `SUNLIGHT_AMBIENT`. E42's night reflection disable is
gone: `ReflectedLightSource` is `Bg` at every zone, because the night sky reflected is now the
zone's own near-black rather than a daylit gradient. Option (b) was not attempted: no zone block
carries a sky colour, so a per-zone panorama would have to be RENDERED off the dome geometry, and
the census below shows a flat `FOG_COLOR` already lands within 2 to 15 units of that dome.

**The colour census.** The dome as drawn in original mode, sampled off `--freecam --det` captures
(mean sRGB over a fixed rect, 0-255), against the zone's authored `FOG_COLOR`. The table is
`docs/org/weather.md`'s, repeated here for the judgement it settles.

| Chapter / zone | dome top | dome at the horizon | authored `FOG_COLOR` |
|---|---|---|---|
| C1 zone2 | 174, 174, 174 | 176, 176, 176 | 176, 176, 176 |
| C1B zone1 | 35 median luminance under the puff field | 29, 37, 58 | 16, 24, 48 |
| C3 zone1 | 176, 209, 242 | 186, 193, 205 | 201, 201, 201 |
| C5 zone1 | 17, 18, 26 | 2, 2, 3 | 0, 0, 0 |

Only C3 disagrees in hue, and there the blue top carries almost exactly the fog's luminance (204
against 201). Nothing else per zone is decoded to blend with, so `FOG_COLOR` alone is the answer
and no blend TUNE exists.

**⚠ A background colour is not a sky, and the wrong mechanism looked like a win.** The first arm
set `BGMode.Color` with the zone colour and `ReflectedLightSource = Bg`. C1's lake darkened from 82
to 59 and C3's sea from 98 to 95, both toward the original, and the night chapters came out
byte-identical. The control refutes it: a pure-RED background rendered byte-identically to the
fog-grey one, so the colour reached nothing. Godot builds a radiance map from a `Sky` resource
only, and `BGMode.Color` had simply removed the specular. Under the flat panorama the same red
control tints the water red, which is what licenses the numbers below (`docs/verification.md`
WORLD-29).

**Verified.** <pending orchestrator run>

Commands, all from the item's worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200 (non-zero),
engine errors clean.
`.\RunTests.ps1 -Suite cockpit-overlay-pass -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200,
engine errors clean.
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers, 47.1 s.

Captures are `.\RunProbe.ps1 … --det --mute --frames=120` runs under `.scratch/e44/`, three arms per
pose (original, enhanced from the committed tree, enhanced after), plus a sky-up pose per chapter
for the census. Labelled montages beside them and copied to the plan worktree's `.scratch/e44/`:
`montage_c1b.png`, `montage_c5.png`, `montage_c3.png`, `montage_c1.png`, and the four `*-sky` ones.

| Pose / region | original | enhanced before | enhanced after |
|---|---|---|---|
| C1B night sea, water | 17.73 | 56.58 | 57.02 |
| C1B night sea, island | 26.93 | 61.85 | 62.10 |
| C5 waterfront, near water | 37.46 | 29.11 | 29.11 |
| C5 waterfront, far water | 37.43 | 29.34 | 29.34 |
| C5 waterfront, city blocks | 6.05 | 6.72 | 6.72 |
| C3 island sea, water | 82.17 | 98.43 | 105.22 |
| C3 island sea, mountain | 123.72 | 47.15 | 47.29 |
| C1 waterfall lake, water | 35.86 | 82.01 | 96.17 |
| C1 waterfall lake, cliff | 61.68 | 93.30 | 93.30 |

Rec.709 luminance, 0-255, over fixed rects. C5's frames are byte-identical before and after (its
`FOG_COLOR` is exactly black, so the restored reflection carries nothing), and C1B's move by 0.44:
restoring the night reflection costs what a near-black sky is worth, which is the measurement E42
could not make with a placeholder sky in the way. The day chapters take the hue and the level of
their own sky: C1's overcast 176 grey and C3's 201 grey are brighter than the placeholder gradient
those surfaces mirrored before, so day water reads lighter and greyer, and C1's enclosed lake most
of all.

**What this does not fix.** Day water is now further from the original's flat unreflective surface
than it was (C1's lake 96 against 36), because the authored overcast IS bright. The lever left is
the mirror itself, not the sky it mirrors: C24's SSR/gloss on wave-less water planes is already an
open judgement, and this item makes what it reflects correct rather than deciding how much of it to
reflect. C1B's night sea is still far above the original for the reasons E42 recorded.

## E45 ☑ The lit world fogs after lighting, so fogged hills fade instead of keeping their shading

**Goal.** In enhanced mode a surface inside the fog ramp fades into the fog colour the way the
original's does, only farther away; the lit and shaded sides of a fogged hill no longer read as
structure through the haze.

**Evidence (confidence: traced).** The user saw hill structure inside the fogged band on the C1
hills capture after E41. The `worldLit` arm keeps the original's fog as
`ALBEDO = mix(col, csky_fog_color, fog_amt)` inside `fragment()`, and Godot then applies the sun,
ambient, shadow and specular terms to that fogged albedo, so a fully fogged fragment still varies
with its normal. The fullbright arm is `unshaded`, so its fogged colour is final, which is why the
original fades cleanly.

**Approach.** In the `worldLit` (and `waterLit`) arms only, leave `ALBEDO = col.rgb` and write
the same ramp through the spatial shader's post-lighting `FOG` output
(`FOG = vec4(csky_fog_color, fog_amt)`), which blends the final lit pixel toward the fog colour
after every light term; the cylindrical and altitude logic in `csky_fog_amount` is unchanged and
the fullbright arm's text is untouched. Confirm the fog colour is in the space Godot expects on
that output (the atmosphere include already linearises it) and that the tonemap sees the fogged
result the same way it sees the dome. Check the same for shadows: a shadow inside the ramp must
fade with the fog, which this gives for free.

**Model recommendation.** high, generated-shader surgery.

**Verify.** C1 hills and C4 horizon captures: the fogged band shows no normal-dependent
structure (sample the sun-side and shade-side of one fogged hill, the two means converge to the
fog colour); the near world unchanged. Original-mode every-key dump identical; goldens zero
movers.

**Verified.** <pending orchestrator run>

The landed change is 7 lines in `CSVM/src/Mech3/SceneBuilder.cs`, inside `GetBiasShader`'s `fogged`
block. `fog_amt` and the whole of `csky_fog_amount` are untouched; the emitted line is now selected
by the arm. The `worldLit` arm (which `waterLit` is a subset of) emits
`FOG = vec4(csky_fog_color, csky_fog_on * fog_amt);` and leaves `ALBEDO = col.rgb` as the vertex
modulate wrote it, so Godot lights the surface colour and then blends the finished pixel toward the
haze after every light term. Every other arm keeps
`ALBEDO = mix(ALBEDO, csky_fog_color, csky_fog_on * fog_amt);` byte for byte. `fog_world` is still
computed for both, since its guard is `fogged || fullbright` and the lit arm is fogged.

**The colour space is settled by measurement, not by reading.** `csky_fog_color` is written linear
(`WeatherRig.ApplyZone` converts the authored sRGB `FOG_COLOR`), and `FOG.rgb` is resolved in that
same linear space ahead of the same tonemap the fullbright path's mix goes through. At C1's freecam
default the camera-anchored dome, which fogs through `ALBEDO` on the fullbright arm, reads exactly
`(159.00, 159.00, 159.00)` in enhanced mode; after the change the fully fogged ridge below it reads
exactly `(159.00, 159.00, 159.00)` on both its sun and its shade face. A `FOG` value resolved in
gamma space, or after the tonemap, could not land on the fullbright arm's number to the last
hundredth. Custom `FOG` also applies with the Environment's own fog switched off, which the same
measurement proves, so nothing had to be enabled on the Environment.

**A consequence worth naming: the haze is no longer multiplied by the enhanced sun.** The old form
was `light x mix(albedo, fog, f)`, so the fog colour itself took the sun and ambient terms; the new
form is `mix(light x albedo, fog, f)`, which is the model the faithful path already uses, where
`csky_world_light` scales `ALBEDO` and the mix then pulls toward an undimmed `csky_fog_color`. Where
a zone's enhanced sun energy exceeds 1 the mid-ramp veil therefore reads thinner than before: C4
(sun energy 1.61) shows its fogged dunes darker and slightly more separated mid-ramp, while its
deepest visible terrain converges on the dome. That is the sun no longer lighting the atmosphere,
and the energies it interacts with are already TUNE under "Open judgements".

Commands and results, all from the worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -Suite plane-shader-reuse -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200
(non-zero), engine errors clean, 0 unexpected lines.
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers, 45.9 s.

Original-mode byte identity used B11's method rebuilt as a throwaway: a static enumerator called
from `SceneBuilder`'s constructor, armed by `CSVM_SCRATCH_SHADER_DUMP`, walking every reachable key
of all three generators (`GetBiasShader` over DebugClutterFlag x shaded x textured x blend x scissor
x doubleSided x scroll x clampUv x lit x fogged x clutterFade x water x 4 edgeClamp values, plus
every billboard key and all three cylindrical axes) and writing each key's `Shader.Code`. The
baseline came from the committed tree before the edit. `dump_orig_before.txt` and
`dump_orig_after.txt` are both 25,953,088 bytes and SHA-256
`BBCC1E87A7F868C47F878BA37C3F3393885CB19039C1E3A440CB08F695580FF9`, identical, so original mode's
shader text did not move. `dump_enh_before.txt` is 25,120,320 bytes /
`2B799A2DC9949F8F39BB011B3F314C389E2E035DBD499CD6AE04D5765E252981` and `dump_enh_after.txt`
25,099,840 bytes / `F0CC2A12BEC9FE2FFF917DE007D6F094D27365D2B734C3DEE72E913BCF9F1B18`, so the
instrument was seen able to fail. Attributing every differing line to the key header above it, 2,048
of the 16,640 dumped keys moved and all 2,048 are `bias sh=False li=True fo=True`, which is the lit,
fogged world arm and nothing else. The instrument was deleted before finishing.

Captures are `.\RunProbe.ps1 --freecam --chapter=<X> --graphics=<mode> --det --mute --frames=15
--screenshot=<abs>` runs under `.scratch/e45/`, three arms per pose (original, enhanced on the
committed tree taken first, enhanced after the edit), all nine exiting 0 with zero engine ERROR
lines. Rects are 24x12, the two hill rects of a pose sharing a row so they share a depth.

| pose | rect (x,y) | original | enhanced before | enhanced after |
|---|---|---|---|---|
| C1 hills | fogged ridge, sun face (1072,372) | 176.00 | 138.47 | 159.00 |
| C1 hills | fogged ridge, shade face (1008,372) | 176.00 | 121.10 | 159.00 |
| C1 hills | dome above the ridge (1200,352) | 176.00 | 159.00 | 159.00 |
| C1 hills | near hillside (200,640) | 77.87 | 98.92 | 98.92 |
| C4 horizon | fogged dune, sun face (944,408) | 151.75 | 143.10 | 136.96 |
| C4 horizon | fogged dune, shade face (1072,408) | 147.38 | 121.72 | 108.91 |
| C4 horizon | deepest visible terrain (1184,376) | 191.84 | 168.49 | 167.11 |
| C4 horizon | dome beside it (1200,340) | 192.00 | 168.00 | 168.00 |
| C4 horizon | near ridge (300,550) | 73.43 | 91.66 | 91.66 |
| C2 buildings | hillside, sun face (112,352) | 215.76 | 145.58 | 145.58 |
| C2 buildings | hillside, shade face (240,352) | 215.76 | 107.84 | 107.84 |
| C2 buildings | near wall (700,425) | 75.67 | 72.11 | 72.04 |
| C2 buildings | near hangar roof (350,500) | 119.40 | 124.45 | 124.45 |

C1 is the item's claim measured: the ridge sits past that zone's pushed fog far, its two faces
differed by 17.37 before and by 0.00 after, and the value they land on is the dome's own fogged
value. C4's chosen pair is mid-ramp rather than fully fogged, so it separates by 6.7 more than
before for the sun-energy reason above, while the deepest terrain in frame moves from 0.49 above the
dome to 0.89 below it. C2's hillsides sit inside that zone's 4200 m fog near, so they are unfogged
and unmoved to the last hundredth, which is the near-world control the item asked for; the three
near-camera rects across the three poses move by 0.00, 0.00 and 0.07.

Montages: `montage_c1_hills.png`, `montage_c4_horizon.png`, `montage_c2_buildings.png` and the two
3x close-ups `montage_zoom_c1_hills.png`, `montage_zoom_c4_horizon.png`, under
`.claude/worktrees/eg-e45/.scratch/e45/` and copied to
`.claude/worktrees/enhanced-graphics/.scratch/e45/`.

## E46 ❌ The water mirror strength, measured against the original's water, or the water bit parked

**Goal.** Enhanced-mode water reads close to the original's water luminance by day and by night,
with whatever reflection survives that constraint; if no roughness/specular pair gets there, the
water bit is parked and C24's verdict re-recorded.

**Evidence (confidence: traced).** With E44 the reflection reads the authored fog colour, and the
day water is brighter than before (C1 lake 96 against the original's 36; C3 sea 105 against 82)
because the authored overcast is bright and the water arm's roughness 0.1 / specular 0.5 mirrors
it in full. The user's complaint is the grey water; C24's open judgement is the hard mirror.

**Approach.** Sweep the water arm's `WaterRoughness` and `WaterSpecular` (and SSR on/off) on the
C1 lake, C3 sea and C1B night sea poses, tabulating water luminance against the original at each
setting; pick the pair whose water lands nearest the original in all three while keeping a visible
shoreline reflection at sea level, or park the bit if none does. The pair stays a named TUNE.

**Model recommendation.** medium, a measurement sweep with one judgement.

**Verify.** The three poses' water within a stated distance of the original; goldens zero movers.

**As landed: the water bit is parked.** The sweep is six roughness/specular pairs, each with SSR
on and off, at three poses, sampling a fixed water rect and a fixed non-water reference rect per
pose through a throwaway environment-variable override on the two constants
(`CSVM_SCRATCH_WATER_ROUGHNESS`/`_SPECULAR`) and a throwaway gate on the SSR call
(`CSVM_SCRATCH_NO_SSR`), both removed before finishing. Rects (`x,y,w,h`, pixels): C1 waterfall lake
water `(50,560,250,90)`, reference (left cliff) `(50,60,200,100)`; C3 island sea water
`(550,470,200,80)`, reference (mountain) `(550,150,200,100)`, shoreline crop
`(0,260,350,110)`; C1B night sea water `(550,550,200,100)`, reference (island) `(880,200,200,80)`.
Rec.709 luminance, 0-255, mean over the rect.

| Pose | original | 0.1/0.5 (shipped) | 0.3/0.5 | 0.5/0.5 | 0.5/0.25 | 0.7/0.25 | 1.0/0.0 (matte) |
|---|---|---|---|---|---|---|---|
| C1 lake, water | 33.47 | 83.64 | 79.57 | 71.82 | 59.45 | 56.60 | 49.60 |
| C3 sea, water | 87.47 | 111.94 | 111.66 | 111.05 | 104.09 | 103.80 | 100.45 |
| C1B night sea, water | 17.73 | 56.96 | 58.19 | 63.06 | 58.25 | 59.19 | 56.57 |

SSR on and SSR off read the same water mean to within 0.1 at every pair and pose (the reflection's
own contribution at these poses is a fraction of a level once averaged over the whole rect; C24
already measured it dies within about 240 px of the shoreline and falls off further with altitude,
and two of the three poses here are shot from above rather than at grazing incidence). The
reference rect holds flat across every setting at a given pose (C1 154.97, C3 64.99, C1B 52.1-52.6,
the small C1B spread being SSR bleed a few tenths of a level wide at the rect's lower edge, nearest
the water line below the sampled island), which is the control showing only the water rect moved.

**The pattern is monotone and the closest pair is the matte one.** Lowering roughness and specular
from the shipped 0.1/0.5 toward 1.0/0.0 tracks water luminance down toward the original at both day
poses, and 1.0/0.0 (ROUGHNESS 1.0, METALLIC 0.0, SPECULAR 0.0) is exactly the ordinary matte world
arm's own values, so that setting is the water bit already switched off in substance, not merely in
number. Distance from the original at that pair: C1 16.13, C3 12.98. The nearest pair that still
carries any specular, 0.7/0.25, sits at 23.13 and 16.33, worse in both, and every pair between it
and the shipped 0.1/0.5 is worse again. At C1B every pair, matte included, clusters within about 7
of the original's furthest point (56.57 to 63.06 against 17.73, distance 38.84 to 45.33): the
residual there is dominated by the enhanced sun and ambient landing on the water's albedo, a term
this item does not own and no roughness/specular pair moves.

A crop at the C3 shoreline (`montage_c3_shoreline_crop.png`, the rect above) shows what the numbers
say: the shipped pair's water reads as a pale mirror of the overcast dome, and the matte pair reads
as the original's darker, more saturated teal, with no mirrored shoreline at all. No pair in between
reads meaningfully different from the shipped pair in that crop; the grey cast comes from any
non-zero specular meeting a bright authored sky, not from the specific magnitude chosen.

**Judgement.** No roughness/specular pair lands near the original while keeping a visible
reflection. The day poses' water luminance moves smoothly with the pair chosen, but even the
least reflective non-matte pair (0.7/0.25) stays 16-23 levels above the original, most of the gap
the shipped pair opened; the night pose barely moves at all across the whole sweep, because its
residual is lighting, not reflection. The only pair that gets near the original removes the
reflection outright. The water bit is parked: the `waterLit` shader arm and
`Launcher.EnableWaterReflections`'s SSR call are removed, water surfaces render through the
ordinary matte `worldLit` arm like every other lit-but-glossless surface, and C24's verdict is
re-recorded above as superseded.

**Verified.** <pending orchestrator run>

Commands, all from the item's worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors.
`.\RunTests.ps1 -Suite plane-shader-reuse -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200
(non-zero), engine errors clean.
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers.

Original-mode byte identity used the established every-key dump method as a throwaway: a static
enumerator in `SceneBuilder`'s constructor, armed by `CSVM_SCRATCH_SHADER_DUMP`, walking every
reachable key of `GetBiasShader` and writing each key's generated `Shader.Code`. The baseline came
from the committed tree (with the `waterLit` arm still present, `water` fixed to `false`, which is
original mode's value at every key regardless of the argument); the after dump came from this
item's tree with the arm removed. `dump_before.txt` and `dump_after.txt` are both 6,323,200 bytes
and SHA-256 identical, so original mode's shader text did not move. The instrument was removed
before finishing.

Thirty-nine captures through `.\RunProbe.ps1` (`--det --mute --frames=120`, the three poses' own
golden-camera args where a golden pose exists), all exiting 0, under `.scratch/e46/` and copied to
`.claude/worktrees/enhanced-graphics/.scratch/e46/`: one original-mode shot per pose, twelve
enhanced-mode shots per pose (the six pairs above, each with SSR on and off). The three-pose
comparison montages (`montage_c1.png`, `montage_c3.png`, `montage_c1b.png`) show original / the
shipped pair / the parked result side by side with the measured water level in the caption;
`montage_c3_shoreline_crop.png` shows the shoreline crop the judgement above cites. A byte-for-byte
check confirms the parked code path: a fresh capture per pose with no environment override and
`--graphics=enhanced` hashes identical to that pose's `1.0/0.0` sweep capture (both SSR on and off),
confirming the matte arm produced by parking the water bit is the same pixels the sweep measured
under that pair's name.

## E47 ☑ Day chapters read brighter than the original overall; the energy mapping re-anchored on frames

**Goal.** An enhanced day frame's overall level matches the original's within a stated margin,
so the lit world adds shading rather than brightness.

**Evidence (confidence: direction-sound).** B12 anchored `SunEnergyPerDiffuse` and
`AmbientEnergyPerAuthored` on the launcher's hardcoded 1.6 / 0.9, which lit only the aircraft;
now that the world takes the same energies on top of its baked vertex colours, the day frames
read brighter than the original (E44's C1 cliff 93 against 62; C4's far ridges washed before the
tonemap). The night side is handled by E42's cap.

**Approach.** Measure the mean luminance of matched original and enhanced frames across the eight
chapters' default freecam poses (sky excluded), fit the single factor on the two mapping
constants that brings the day chapters' median ratio to 1.0, keep the ratio between the two
constants, and re-anchor the tests. State the residual per chapter. The aircraft's own level is
allowed to move with the world.

**Model recommendation.** medium, measurement discipline.

**Verify.** Per-chapter ratio table before and after; `SunlightEnergyTests` re-anchored; goldens
zero movers.

**As landed.** No constant moves. The measurement below found the day chapters' median ground
ratio already at 0.97 under B12's pair (1.07 / 1.8), and the one round the agent tried (1.10 /
1.85, a 3 % bump) closed a residual of 1.75 points, smaller than the per-rect noise floor, so the
orchestrator rejected the bump as unmeasurable and kept the pair and its tests. The premise
("day chapters read brighter overall") is disproved as a level claim: the per-chapter residuals
(C1 +35 %, C3 -36 %) are shading landing on baked vertex colours, sunlit rects up and shaded rects
down, not a uniform offset a single factor could remove. What the user saw as too bright is the
water (E46) and C5's day-level authoring (E42), not the day level. `IsNightZone`,
`NightDiffuseCap` and `NightAmbientCap` are untouched. The tables below are the evidence; the
"after" columns show what the rejected 3 % bump did.

**The measurement.** Eight chapters' default `--freecam --det --mute --frames=15` poses, original
and enhanced, before and after the factor change. Two rects per capture: the frame's lower 60%
(y ≥ 40% of 720, full width — ground and structures, no sky) and one hand-picked ground rect per
chapter (terrain or a building wall, chosen sunlit where the pose has a sunlit side, since a
shaded pick answers a shadow question, not a brightness one). Mean Rec.709 luminance, 0-255, every
second pixel. `WeatherRig.IsNightZone` puts C1B and C5 on the night side of the separator at their
default poses (C1B's zone1 fog luminance 0.06, C5's zone1 0.00); C3's default pose sits in its
zone1 (day, fog luminance 0.79), not the in-cloud night zone2. The remaining six chapters are day.

Two of those six have no qualifying ground rect at their default freecam pose at all: C1C's spawn
looks down on solid cloud cover with no terrain in frame, and C2B's looks out over open sea in a
storm with no non-water surface in frame. Both are reported with a labelled surrogate rect (a
cloud plateau, a rain-streaked sky band) rather than invented terrain, and both are EXCLUDED from
the fit target below, since a cloud billboard and the background sky are not part of the lit-world
surface the two constants shade and barely move with them (confirmed: both surrogate ratios sit
within 3% of 1.0 in every round, unlike the four real ground rects).

| Chapter | night? | lower-band original | lower-band before | lower-band ratio | ground rect | rect original | rect before | rect ratio |
|---|---|---|---|---|---|---|---|---|
| C1 | no | 107.21 | 100.01 | 0.933 | sunlit grass field, right of frame | 66.27 | 88.40 | 1.334 |
| C1B | yes | 21.98 | 61.05 | 2.778 | island terrain, centre island | 26.85 | 63.63 | 2.370 |
| C1C | no | 179.20 | 177.40 | 0.990 | SURROGATE: cloud plateau (no terrain in frame) | 186.74 | 182.01 | 0.975 |
| C2 | no | 118.38 | 105.89 | 0.894 | hangar wall, left of frame | 158.46 | 138.84 | 0.876 |
| C2B | no | 99.48 | 130.05 | 1.307 | SURROGATE: rain-streaked sky band (no non-water surface in frame) | 164.15 | 174.78 | 1.065 |
| C3 | no | 167.16 | 135.21 | 0.809 | sunlit ridge top, centre of frame | 187.35 | 118.91 | 0.635 |
| C4 | no | 112.86 | 117.24 | 1.039 | snow mountain slope, left of frame | 117.32 | 125.36 | 1.069 |
| C5 | yes | 13.11 | 14.76 | 1.126 | rooftop plaza, centre, no lit windows | 19.28 | 14.18 | 0.735 |

Fit target: the median ground-rect ratio over the four day chapters with a real ground rect (C1,
C2, C3, C4). Before any change: 0.876 and 1.069 bracket the median at 0.9725, already close to 1.0
despite the wide per-chapter spread (0.635 to 1.334) a real sun and real shadows now put into a
single hand-picked patch that the original's flatter, fog-hazed lighting did not. A day chapter's
median sitting near 1.0 while individual chapters swing either side of it is what "shading, not
brightness" looks like when the shading is real: C3's chosen ridge sits in a cast shadow the
faithful path never draws, and C1's chosen field sits in the sun the same way.

**Round 1.** `SunEnergyPerDiffuse`/`AmbientEnergyPerAuthored` 1.07/1.8 → 1.10/1.85 (a 3% common
factor). Re-captured the four real-ground day chapters (C1, C2, C3, C4) plus the two surrogate and
two night chapters for completeness:

| Chapter | ground rect ratio, before | ground rect ratio, after |
|---|---|---|
| C1 | 1.334 | 1.348 |
| C1B (night) | 2.370 | 2.399 |
| C1C (surrogate) | 0.975 | 0.975 |
| C2 | 0.876 | 0.886 |
| C2B (surrogate) | 1.065 | 1.065 |
| C3 | 0.635 | 0.641 |
| C4 | 1.069 | 1.079 |
| C5 (night) | 0.735 | 0.748 |

Median over C1/C2/C3/C4: 0.9825. The night chapters move with the same common factor (the cap
still applies to the raw authored diffuse/ambient before the factor multiplies it, per E42, so a
capped zone's *input* is unchanged but its *output* scales exactly like a day zone's), which is
expected and does not reopen E42: C1B and C5 stay far below their day counterparts either way.

A 3% factor moved the median by one point (0.9725 → 0.9825), a tonemap-compressed response
consistent with the approach's warning that the mapping is not linear. The residual (1.75
percentage points) is smaller than the per-chapter noise a single hand-picked rect carries (the
four real rects' after-values span 0.641 to 1.348), so a second or third round would be tuning
inside the noise floor rather than converging on a better answer. Two rounds close it: **round 1's
1.10 / 1.85 is the final pair.**

**The residual per chapter**, ground-rect ratio at the final factor: C1 +34.8% (sunlit, real sun
now lighting a field the original's haze flattened), C2 -11.4% (a wall now reads its own cast
shadow), C3 -35.9% (a ridge now sits in a cast shadow the faithful path never draws), C4 +7.9%
(open snow slope, direct sun). None of these four is "the mapping is wrong at this level"; each is
the shading the item exists to add, measured on a patch small enough that one shadow edge crossing
it dominates the number. The lower-band ratio, which averages across the whole ground/structure
band rather than one patch, is closer to 1.0 for three of the four (C1 0.941, C2 0.902, C4 1.048)
and still shows C3 low (0.813) for the same reason (its lower band is mostly the same shaded
slope). The night chapters (C1B, C5) are unaffected in kind by this item: their residual against
the original is E42's and E44's, not this item's, and this item's factor change moves them by the
same 1% a day zone sees.

The aircraft's own level at C1's flight spawn (`--fly --chapter=C1 --plane=player_bhawk`, the
plane-box rect `SceneBuilder`'s livery occupies) moves with the world, which the goal explicitly
allows: mean luminance over that box reads 71.08 (original), 85.79 (enhanced, before), 86.73
(enhanced, after) — the aircraft brightened when the world first took a real sun (B12/B11) and
moves negligibly further under this item's 3% nudge, as expected for a factor this small.

**Verified.** <pending orchestrator run>

The complete `.\RunTests.ps1` on this item's tree: 2805 units, 200 engine suites (errors clean),
18 goldens hash-identical, all passing in 142.6 s.

Commands, all from the item's worktree with `$env:CSVM_DATA_ROOT="Z:\CSVM"`:
`dotnet build CSVM/CSVM.sln` clean, 0 warnings, 0 errors, at both the baseline and the final
factor.
`dotnet test CSVM.Tests/CSVM.Tests.csproj --no-build --filter "FullyQualifiedName~SunlightEnergyTests"`:
7 passed, 0 failed, re-anchored to the new factor's outputs (the modal day pair now resolves
1.65 / 0.93; the night pair, raw-capped then scaled by the same factor, now resolves 0.66 / 0.28).
`.\RunTests.ps1 -Suite fog-state -SkipUnits -SkipGoldens`: PASS, 1 suite run of 200, engine errors
clean.
`.\RunTests.ps1 -SkipUnits -SkipEngine` (goldens only): PASS, 18 shot(s) hash-identical, zero
movers.

Sixteen captures per round through `.\RunProbe.ps1` (eight chapters, original and enhanced, `--det
--mute --frames=15 --screenshot=` per chapter) under `.scratch/e47/round0` (baseline) and
`.scratch/e47/round1` (final factor), plus the C1 flight-spawn aircraft triple
(`--fly --chapter=C1 --plane=player_bhawk`) under `.scratch/e47/final`. Labelled three-way
(original / before / after) montages, copied to the plan worktree's `.scratch/e47/`:
`montage_c1_freecam.png`, `montage_c3_freecam.png`, `montage_c2_buildings.png`,
`montage_c4_horizon.png`, `montage_c1_flight_spawn.png`.

**What this does not fix.** The per-chapter residual (C1 and C4 brighter than the original, C2 and
C3 darker) is real shading from a real sun and real shadows landing on baked vertex colours that
already encoded the original's own static shading, the same double-shading the lit-world item's
traps named; this item's mandate is the day chapters' overall level, not per-chapter or
per-surface shading, and it does not touch the vertex-colour-flattening question that trap raised.

## Open judgements

The following stay TUNE: correct in shape, but judged at the controls rather than derived from a
decoded rule.

- The SUNLIGHT-to-Godot energy mapping (`WeatherRig.EnhancedEnergies`'s `SunEnergyPerDiffuse` and
  `AmbientEnergyPerAuthored`), anchored on the install's modal day zone.
- The 2x fog-range push (`WeatherRig.EnhancedFogRangeScale`) and the shadow max distance that
  follows it.
- C5's night zone authoring a day-level SUNLIGHT, so its skyline reads daylit under the pushed
  fog with nothing in the data asking for a dimmer light there.
- SSR's hard mirror on wave-less water planes, since the surfaces carry no wave normals to break
  the reflection up, and with it how much of the zone's own sky that mirror should return: a day
  chapter's authored overcast is bright, so C1's lake reads light grey under it.
- When to pin the two enhanced goldens proposed in D32 (declined until the values above settle),
  and whether the day/night pair chosen there is the right pair to stand in for the whole mode.
