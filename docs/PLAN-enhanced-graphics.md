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
22. ☐ Environment glow + tonemap
23. ☐ SSAO
24. ☑ SSR on water — evaluate, then ship or park

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

**Verified.** <pending orchestrator run>

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

**Verified.** <pending orchestrator run>

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

**Verdict: ship, mode-gated.** The census gives a decoded handle rather than an invented one, the
prototype is one shader bit and five Environment properties, original mode is byte-identical and all
18 goldens are unmoved, and at and near sea level the reflected shoreline is both readable and
temporally stable. The SSR values are Godot's own defaults in shape and were not pushed to
manufacture a reflection: the alternative to shipping is a glossy sea that reflects only the sky,
which is strictly less than what the marched rays deliver at no authoring cost.

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
