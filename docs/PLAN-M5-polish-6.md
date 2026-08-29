# Milestone 5 polish, run 6: cross-theme traced polish

**ACTIVE PLAN** (written 2026-08-28). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This run takes eleven open items from the themes the campaign walk of `PLAN-M5-polish-5.md` does
not touch: world lighting and the clutter fade band, the effects and animation runtime, the
allocation and tick budget, and the damage share and target class. The selection criterion is
"cross-theme traced polish" (Decision 1): open `[Bug]`, `[Fidelity]` and `[Perf]` items that are
unblocked, are not `[Owed-playtest]`, and carry either a cited routine, a cited data file or a
measurement, chosen so no item contends with run 5's remaining work. Items needing an
original-game A/B before any code can move are out (`BL-070`, `BL-326`, `BL-331`, `BL-332`,
`BL-325`), as are the plan-sized or user-deferred items (`BL-150`, `BL-446`, `BL-463`, `BL-256`),
the AI mode machine (`BL-523`, `BL-550`, `BL-558`, `BL-565`, `BL-566`), everything
`[Blocked: ...]`, and every `[Owed-playtest]` item, whose code side is done and which needs the
user at the controls rather than a plan.

**Every item was re-verified still-open against the record in this session**, both against
`git log --grep` on each id (every hit is a filing, a minting or a cross-reference, never a
landing) and against the `backlog.md` of the `worktree-m5-polish-5` branch, which is ahead of main
and deletes on landing. **None was re-verified against the code**; each item carries a
`<TODO: re-verify still-open against the code>` for that half.

**⚠ This plan is written against the run-5 branch's backlog, not main's.** Three of its items
(`BL-586`, `BL-587`, `BL-598`) were filed inside `worktree-m5-polish-5` and do not exist on main
yet, and two of them build directly on code that lands with run 5. Branch run 6 from
`worktree-m5-polish-5`, or from main once run 5 merges; a branch taken from today's main will not
find them.

**Decode stays open per item (Decision 3).** Every item's Approach names the `crimson.exe` routine
or the data file that settles it where one is known, and an implementer may take that lane instead
of the code-side lead whenever the lead runs out. A decoded rule beats a plausible fix here, as in
every prior run.

## Milestone goal

- The world lights as the original does at the two places `CAP-11` measured and one place the
  controls reported: water renders unmodulated, C5's lit facades reach the original's brightness,
  and the large buildings carry no dark band at the range the templates clutter fades out.
- The effects and animation runtime does what its data authors: a nitro engage swaps the prop
  discs and trails smoke, a chapter-scope animation file no mission lists does not run, and a
  repeat sonic burst costs no more than its first.
- The frame budget holds: collections stop being gen1 with a 24 to 29 ms pause, the late-CM11
  physics tick comes under the 16.7 ms budget so the sim clock tracks the wall clock, and the
  allocation assertion that guards all of it is stable inside the parallel unit stage.
- A burst deals a world destructible one splash share rather than one per collider body, and a
  surface vehicle can be targeted and aim-assisted like the other target classes.

**No item here judges a constant at the controls.** Every `[Tuning]` and `[Owed-playtest]` item is
deliberately out: those need the user flying, not an implementer, and they are consolidated in
[`playtest.md`](../playtest.md). Where an item's verification does need the user's eyes (A1, A2,
A3, B12), it says so and the code side lands first.

## Decisions (2026-08-28)

| # | Question | Decision |
|---|---|---|
| 1 | Which selection criterion | **Cross-theme traced polish** over the camera and view scheme, the owed-playtest tuning sweep, and the presentation gaps: run 5 spent itself on one campaign walk, and the themes it left alone hold traced items with named mechanisms. |
| 2 | How this relates to the active `PLAN-M5-polish-5.md` | **Run in parallel.** Run 5 has 21 of its 22 items landed on `worktree-m5-polish-5` and only its closing sortie (`D31`, a flight of CM04 to CM09) remains, which contends with no code here. |
| 3 | Weight | **Routine, with the decode lane kept open on every item.** No up-front data survey; each item names what in `crimson.exe`, the mission data or the measured record settles it. |
| 4 | Size | **Eleven items in four waves.** One wave per theme, so a wave can be taken whole by one agent without file contention with the others. |

## ⚠ Read this before implementing anything

The evidence quality is uneven across this plan, which is the price of picking across themes rather
than down one. Budget by the grade, not by the item's apparent size.

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code or data** | B11, B12, B13, C21, C22, D31, D32 | Confirm the trace, then implement. |
| **Direction sound, magnitude or mechanism a judgement call** | A1, A2 | A measurement fixes the *what*; the mechanism is a candidate, not a finding. Ruling the candidate out is a result. |
| **Leads only, no mechanism yet** | A3, C23 | Budget for investigation; either may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees, so never use it in a
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

### Wave A — World lighting and the fade band

1. ❌ `BL-304`: water takes the WorldLight dim the original renders it without
2. ☑ `BL-322`: C5's lit facades render at 0.58 to 0.66 of the original with WorldLight already at clamp
3. ❌ `BL-538`: the band is the `cblock*` mip chain's non-monotone level 1, not the clutter fade

### Wave B — Effects and the animation scope

11. ◐ `BL-546`: the exhaust smoke lands, the prop swap does not and the anchor question stays open
12. ☑ `BL-587`: chapter-scope animation files no mission lists still run
13. ◐ `BL-535`: a repeat sonic burst pays a 10 to 14 ms slot re-reset from the third burst on

### Wave C — The allocation and tick budget

21. ☑ `BL-536`: the gen1/24-29 ms reading is the session build settling; the settled pause is set by finalizable-object count
22. ❌ `BL-562`: the 39 ms is Godot's 1 Hz worst-tick monitor; the step is 1.8 ms and the sim keeps up
23. ❌ `BL-584`: `PerfSampleTests.AScopeAllocatesNothing` is not same-build stable in the parallel unit stage

### Wave D — Damage share and target class

31. ☑ `BL-586`: one burst deals a world destructible one splash share per collider body it carries
32. ☑ `BL-598`: CM08's patrol boats take hits but cannot be targeted or aim-assisted

## Dependency and parallelism notes

The four waves own disjoint files and may each run in their own worktree in parallel, which is why
the plan is cut this way. Inside a wave, the notes below apply.

Wave A's three items all end in a shader or material decision about the world pass, and A1 and A2
share the `csky_world_light` question directly (A1 proposes exempting a material from it, A2 asks
whether these facades should be modulated at all), so run them in sequence in one worktree rather
than in parallel; A1 first, because its measurement is the tighter of the two. A3 is separable
(the clutter fade cutout and the gamez buildings' own fade range) and may run alongside, but not in
a parallel worktree if it touches `csky_clutter_fade` and A1/A2 touch the shared world shader
include. The two live in separate includes (`csky_world_light` is declared in
`csky_atmosphere.gdshaderinc` and the fade in `csky_clutter_fade.gdshaderinc`), so the contention is
only over `SceneBuilder.GetBiasShader`, which assembles both into one generated shader.

Wave B: B11 and B13 are both in `AnimRuntime` and its emitter path, and B13 changes what
`ResetCheckedOutCopies` walks while B11 may need `PlayWithin` and the activation path, so sequence
them; B11 first, since it is the player-visible one. B12 is in the definition-file gate rather than
the runtime and contends with neither, but its verification needs the C1 goldens and the user's
eyes, so it should not land in the same commit as either.

Wave C: C21 and C22 are the same budget seen from two instruments and share suspects (the ray-query
wrappers per sim step are both a C21 allocator and a C22 physics-step cost), so run them in one
worktree, C21 first: removing the per-cast allocations may move C22's number and must be measured
before C22 attributes what is left. C23 guards the assertion both of them lean on, is small, and
should land first of the three so the later measurements are made against a stable gate.

Wave D: D31 is in `ProjectilePool.ApplyDamage` and the `DamageSink` key, D32 is in
`TargetSelection` and `AimAssist`; they do not meet, and both build on code that lands with run 5
(`BL-573`'s turret fire path and `SurfaceVehicleRuntime` respectively), so neither may start on a
branch taken from today's main.

Against `PLAN-M5-polish-5.md`: its only remaining item is `D31`, a flight of CM04 to CM09 by the
user, which lands no code. Nothing here contends with it. `BL-598` is a CM08 report and the run-5
sortie flies CM08, so a fix landed here before that sortie will be seen on it; that is a benefit,
not a conflict, and the run-5 sortie does not judge `BL-598`.

---

# Wave A — World lighting and the fade band

## A1 ❌ `BL-304`: water takes the WorldLight dim the original renders it without

**Closed without code.** Closed on the user's decision that footage-derived and screenshot-derived
measurements — this project's repeated failure mode — do not drive a fidelity change by themselves,
and `BL-304` rests entirely on one such measurement. Everything found while investigating strengthens
that call rather than the fix: the shipped gamez data authors `lighting: true` on every water tile
exactly like ordinary terrain, so nothing in the original's own data marks water exempt; a bounded
Ghidra search of `crimson.exe` (below) found no routine that exempts a surface from SUNLIGHT
modulation by soil type, material flag or class; the raw C2B A/B needed a rain-streak correction
before it recovered anything close to CAP-11's 0.78 ratio; a real 3–6% overshoot remains unexplained
after fog was tested out directly; and the `SoilId == Water` key silently covers non-ocean water
CAP-11 never measured, including a node literally named `pool` in C3. A `SceneBuilder` override with
no authored field and no decoded routine behind it is exactly the invented-rule failure mode this
project names as its most common one, fitted to two screenshots. The code is reverted;
`CSVM/src/Mech3/SceneBuilder.cs` is byte-identical to its committed state.

**The decode search (bounded, then stopped on instruction).** Working outward from `BL-332`'s
decoded sunlight pair: `FUN_00472ea0` (the zone-apply, called once per fog-zone/mission change) calls
`FUN_004dbdb0` (diffuse) and `FUN_004dbce0` (ambient) on the `sunlight` gamez node's own `Light`
class instance (`D:\zipper\gamez\zclass\Light.c`) — these are the light object's own property
setters, not a per-surface test. The renderer's per-material draw setup (`FUN_005a4210`, called from
the polygon-submission code `FUN_005c0ff0`/`FUN_005d1940` in `zvid_ddd3d.c`) toggles
`ALPHABLENDENABLE`/`SHADEMODE`/`ZWRITEENABLE`/`SRCBLEND`/`DESTBLEND` keyed on the texture's own blend
field (`tex+0x10`, matching `BL-508`'s existing decode of that same field), never on soil id, and
never touches a lighting-enable state. Vertex colours reaching that polygon-submission code are
already fully computed (packed with alpha as a finished ARGB dword), which means the actual
bake-in-SUNLIGHT-or-don't decision, if a per-surface one exists at all, happens earlier — in a
model/mesh load-time colour bake this search did not locate among the codebase's ~8,400 functions.
The one confirmed soil-id-keyed special case found anywhere in the binary is unrelated to rendering:
`FUN_005ad330` tests `material+0x20 == 1` (water) for weapon-impact effects (splash vs explosion),
not lighting. The `BL-070` "same should-be-exempt family" analogy this item leaned on is itself
undecoded — `BL-070`'s own poleflare entry says outright that it "needs an original-game A/B" — so
that comparison was two unconfirmed hunches supporting each other, not one decoded case backing
another. **The absence of any such routine is itself the result:** nothing found argues FOR a
soil-based exemption, and the data (every water tile authoring `lighting: true`) argues against one.

**The measurement (kept — this is the useful residue of the item).**

*Exact commands (both frames 1280×720, `CSVM_DATA_ROOT=Z:\CSVM`):*
```
.\RunProbe.ps1 --fly --chapter=C2B --mission=IA1 "--pos=-3843,200,-1101" --det --frames=30 "--screenshot=<out>\baseline.png"   # built WITHOUT the fix
.\RunProbe.ps1 --fly --chapter=C2B --mission=IA1 "--pos=-3843,200,-1101" --det --frames=30 "--screenshot=<out>\postfix2.png"  # built WITH the fix
```
against `playtest/CAP-11/t0.5-c2b-spawn-ocean.png` (the original reference, already 1280×720 in the
repo). *Exact boxes* (pixel `x0,y0,x1,y1` in that 1280×720 frame, greyscale mean):
`right=(750,660,950,700)`, `left=(60,660,260,700)`. These are NOT CAP-11's own boxes —
`playtest/CAP-11/README.md` line 94 says those coordinates live only in "the CAP-11 analysis
conversation" and are not in the repo, so they cannot be reproduced; this is a real, reportable
instrumentation gap, not a failure to look.

*Naive box means do not reproduce CAP-11's ratio.* Baseline 43.8 (right) / 44.89 (left); original
51.77 / 52.03 in the same boxes → ratio **0.846 / 0.863**, not CAP-11's 0.784. Post-change 57.44 /
56.01 → post/orig **1.109 / 1.077**, an 8–11% overshoot past the original.

*Root cause of the box mismatch: our render carries visible rain streaks CAP-11's own footage
does not.* C2B IA1 rains below the cloud cover; ours draws one-pixel-wide streaks at full
resolution, while CAP-11's 2560-wide, compressed video capture swallows the same streaks
(`playtest/CAP-11/README.md`'s own caveat: "our low-shot boxes still carry slight streak
contamination"). Per-column means inside each box confirm it: several columns run 45–83 against a
flat ~40–41 median everywhere else — rain, not water texture. Filtering out every column whose mean
exceeds the box's median by more than 3 (same column set applied to both frames, since `--det`
pins the precip seed identically) gives a streak-corrected reading: baseline 40.6 (right, 159/200
columns kept) / 39.97 (left, 139/200 kept); ratio against the SAME unfiltered original (which needs
no filtering) is **0.784 (right) / 0.768 (left)** — the right box lands on CAP-11's 0.784 almost
exactly, the left is close. Filtered post-change: 54.66 / 53.55, i.e. post/orig **1.056 / 1.029** —
a real but much smaller overshoot than the naive 8–11%. This is a derived, contamination-corrected
approximation of CAP-11's instrument, not a reproduction of its own lost box, and the corrected
baseline/original absolute values (40.6/51.77, 39.97/52.03) still sit a few units under CAP-11's
stated 42.0–42.5/53.9 — consistent with box placement differing enough to shift both sides together
without breaking the ratio, but not decisive proof of hitting the same patch of water.

*The plan's own overshoot explanation does not hold up.* Traps says an overshoot "means the water
was carrying a second term as well as the dim" and the first landing text pointed at
`csky_light_spill` and the fog mix. Testing that: a `--no-fog` capture at the same pose gives an
IDENTICAL filtered water mean to the fogged one (54.66 vs 54.66, 53.55 vs 53.55, to two decimals) —
fog contributes nothing measurable to this near-range, low-altitude sample, so **the fog half of
that explanation is wrong and is retracted**. `csky_light_spill` (`CSVM/shaders/csky_lights.gdshaderinc`)
only adds anything within a `LIGHT_STATE` point light's range, and the C2B low pose is open ocean far
from any lit structure, so a nonzero spill contribution here is architecturally implausible, though
no on/off flag exists to isolate it directly — this stays unconfirmed rather than asserted. The
residual ~3–6% overshoot (corrected measure) is real and consistent across both boxes but its cause
is not established.

*Scope is wider than CAP-11 tested.* `SoilId == SurfaceRegistry.Water` is a material property, not
an "is this the ocean" flag, so the exemption applies uniformly wherever the original authors that
soil id. Census across all 8 chapters' gamez data (mesh count carrying the water material): C1 48,
C1B 144, C1C 144, C2 82, C2B 144, C3 348, C4 12, C5 163. C3's water meshes include one node literally
named `pool` — a non-ocean water body. CAP-11 measured open ocean only (C1B, C2B); the pool and any
other non-ocean water surface are exempted on the same soil-id reasoning but have no original-game
measurement behind them.

An 8-chapter `--freecam` sanity sweep (C1/C1B/C1C/C2/C2B/C3/C4/C5) built clean at exit 0 with no new
errors and unchanged mesh/node counts; `dotnet build`/`dotnet format --verify-no-changes`/
`CheckCommentCaps.ps1` are clean. The full golden suite was not run here (orchestrator's job); the
manifest shots carrying open water are named below. `WorldLight` itself was not retuned. This
verification ran against a build carrying the since-reverted `SceneBuilder` exemption; it is kept
here as the record of what was checked, not as evidence for a change now closed.

**Original approach (kept for reference).**

**Goal.** Water renders at the original's brightness in a matched pose: the C2B ocean foreground
reads about 53.9 where it now reads 42.0 to 42.5, with the rest of the world's `WorldLight`
calibration untouched.

**Evidence (confidence: direction-sound, mechanism a candidate).** The `CAP-11` A/B (evidence in
`playtest/CAP-11/README.md`, surfaced closing `BL-110`) measured the C2B ocean foreground in the
same world at a matched spawn pose: original 53.9 against ours 42.0 to 42.5, a ratio of 0.78 that
is our `world_light` of 0.784 to within the measurement. Dividing our value by the dim reproduces
the original within 7%. C1B's night ocean points the same way (original 37 to 42 against ours 9 to
23) but is noisy, because moon glitter and wave texture vary with screen position, so the night
number is support rather than proof. The candidate is that the water material should be exempt from
`csky_world_light`, the same should-be-exempt family as `BL-070`'s poleflare glows.

**Approach.** Find where the water material is built and whether it goes through the shared
`csky_world_light` modulation, then exempt it. The mechanism claim is a candidate and not a
finding, so the first deliverable is confirming that the water pass is in fact modulated and that
removing the modulation lands on 53.9 rather than past it; a ratio that comes out at 0.78 by
coincidence of two other terms is the reading to rule out.

**Model recommendation.** Medium. The change is small and the measurement is already taken; the
judgement is whether the candidate mechanism is the real one, which is a confirm-or-disprove task
rather than a design one.

**Verify.** The C2B low pose, `--pos=-3843,200,-1101`, against
`playtest/CAP-11/t0.5-c2b-spawn-ocean.png`, reading the foreground water the A/B measured. Take the
baseline first: the pre-change number must be seen to reproduce 42.0 to 42.5 before the post-change
number means anything. Then the full 8-chapter `--freecam` regression, plus whichever goldens carry
water: `c1b-night-sea`, `c2b-rain`, `c3-island` by name (open water is explicit in their `exercises`
text); `c1-waterfall` is a maybe (its own water-soil pool may or may not sit in that shot's frame —
its falls/mist geometry is a separate, non-water-soil material either way).

**⚠ Traps.** The `WorldLight` scalar itself is `CAP-11`-calibrated on terrain and must not move;
this item exempts one material from it and does not retune it. C1B's night ocean is support and not
proof, so do not fit anything to the 37 to 42 range. A brightness that overshoots 53.9 means the
water was carrying a second term as well as the dim, which is a different item, not a reason to
scale the exemption.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

## A2 ☑ `BL-322`: C5's lit facades render at 0.58 to 0.66 of the original with WorldLight already at clamp

**Re-scoped to DECODE ONLY, and the decode landed. No rendering code was written, by instruction.**
The user's ruling is that footage- and screenshot-derived measurements are not admissible evidence
for a fidelity change here, so this item was reduced to one question: what in `crimson.exe` decides
whether a scene node's material is modulated by `SUNLIGHT`, and what does it key on?

**The answer, written up in [`docs/org/vertexLighting.md`](org/vertexLighting.md).** A real
per-surface exemption exists, it is two gates, and neither keys on a `soil` id:

1. **Per model** (`FUN_00551d90`): bit 0 of the model record's flag word at `+0x08`, the flag the
   extractor spells `lighting`. Clear, and no light in the scene's array is even considered.
2. **Per texture** (`FUN_005524d0`, textured branch): bit `0x02` of the texture object's storage
   flags byte at `+0x09`, set for exactly the textures carrying an alpha channel. Set, and the whole
   per-vertex lighting evaluation is skipped and the polygon goes through the unlit submission.

`SUNLIGHT` itself is not a separate path: the `sunlight` node is one entry in the same 128-slot
light array as every `LIGHT_STATE` point light (`FUN_00566be0`, `zmodel\gmod_light.c`), marked
directional by `FUN_004dbf70` from the zone apply, and evaluated per vertex at draw time by
`FUN_00567150`. That closes the loop A1's agent left open: the finished ARGB it saw at polygon
submission is written by this evaluation, not by a load-time bake, and there is no load-time bake.

**What it settles, for all three items that were leaning on the same hunch.**

| Item | Verdict |
|---|---|
| `BL-322` (these facades) | **Premise refuted.** `cblock1`–`7`, `bldg1`–`4`, `bldgtrim1` all ship storage flags `0xa5` (no alpha bit) and are lit in the original exactly as in ours. No exemption is available to close the measured ratio. |
| `BL-304` (water) | **Independently refuted.** `wtr00000`–`wtr00015` are `0xa5` too, so water is not exempt. A1's close stands, and by a mechanism rather than by absence of evidence. |
| `BL-070` (poleflare glows) | **Confirmed.** `poleflare` is `0xab`; so are `lightpole`, `lite_out`, `bliteon`/`bliteoff` and C5's signage set. The "should be exempt" half needs no A/B; the billboard-axis half still does. |

**What a decoded fix would look like, for a later run.** Gate 2 is not reproduced anywhere in the
remake: `SceneBuilder` applies its per-model `lit` decision to every surface of the model, overlay
passes included, and nothing reads the texture storage byte. Honouring it means carrying the
extractor's `alpha` field through to the material build and treating `alpha != None` on a textured
surface as "does not take `csky_world_light`", independent of the model flag. The deployed texture
tree is PNGs without the extractor's `manifest.json`, so that is a plumbing job first; and a PNG
alpha-channel test is not a substitute, since it loses the 1 to 10 `Simple` textures per chapter
that carry the bit too. For this item specifically the effect is narrow and its size is not
predicted: it makes the overlays drawn **on top of** the measured facades fullbright
(`buildingspotlighted`'s lit windows, `nypd`, `clock`, `fadedsign01`–`03`, `traffic_sign1`), not the
facades themselves.

**Original goal (kept for reference).** C5's night facades reach the original's measured brightness:
tower faces about 15.5 where they now read 10.2, low-rise about 37.6 where it now reads 21.7.

**Evidence (confidence: direction-sound, mechanism lead-only).** Split out of `BL-303` at its close
on 2026-08-08 and measured under `CAP-11`: tower faces 10.2 against 15.5, low-rise 21.7 against
37.6, with `WorldLight` already at its clamp of 1.0, so the deficit is not the world-light scalar
running short. It is explicitly not fog: that is `BL-303`'s own adjunct note, and the Wave B fog
work of that plan moved none of it. The candidate direction is the lit-signage and self-lit family,
where `lighting: false` models draw fullbright (`docs/formats/weather.md`), so the question is
whether these facades author a flag or vertex data that we modulate and the original does not.
That candidate is now checked against the code and ruled out for the facades themselves: they carry
neither an unlit model flag nor the texture alpha bit the original exempts on.

**Approach.** Start from the facades' own authored material and vertex data in C5's gamez rather
than from the shader: establish what distinguishes the measured faces from the ones that read
correctly, and whether that discriminator is a `lighting` flag, a vertex colour, or a material the
self-lit path should already have caught. Only then decide what stops modulating them. Decode lane:
<TODO: name the `crimson.exe` routine that decides whether a scene node's material is modulated by
SUNLIGHT, and what it keys on.>

**Model recommendation.** High. The mechanism is unknown, the measurement is the only firm ground,
and the change risks becoming a second global brightness knob if the discriminator is guessed
rather than found. The discriminator is now found rather than guessed, and no code was written
against it here.

**Verify.** The C5 night poses listed in `playtest/CAP-11/README.md`, measuring the same tower
faces and low-rise the split recorded, with the pre-change numbers reproduced first. Then the full
8-chapter `--freecam` regression and the C5 goldens.
<TODO: name the C5 goldens in `analysis/goldens/manifest.json`.>
Nothing of that applies to what landed here, which is documentation only. The decode's own checks
are reproducible without a build: the routines above re-read at their addresses in Ghidra, and the
texture-flag claims re-read straight off the shipped `ZBD/<chapter>/texture.zbd` directory (24-byte
file header, then 40-byte records of `name[32]`, data offset, length; the storage byte is the first
byte of the 16-byte image header at that offset). Cross-checked against the extractor's own `alpha`
field in `extracted/<chapter>/texture.zip`'s `manifest.json`: the bit-`0x02` count matches
`Full + Simple` exactly in all eight chapters, and the header widths and heights match too.

**⚠ Traps.** Fog is ruled out and must not be re-chased. `WorldLight` is at clamp here, so raising
it is not available and would break the terrain calibration `CAP-11` pinned. A fix that brightens
every C5 surface rather than the measured family is a regression the goldens should catch, so read
them rather than only the two measured faces. This item interacts with A1: both ask whether a
family should be exempt from the same modulation, and a shared exemption that catches both by
accident is not a result, it is a coincidence to rule out. The decode answers that trap directly:
the exemption A1 proposed and the one this item proposed are the same real mechanism, and it
excludes both families.

**Verified.** <pending orchestrator run>

## A3 ❌ `BL-538`: a dark band crosses the large buildings at the range the templates clutter fades out

**Goal.** In C5, with the authored `far_fade_range` applied, the large gamez buildings read at the
same brightness through the range where the downtown clutter vanishes as they do nearer and
farther, and the fade reaches the other buildings as well as downtown, as it does in the original.

**Evidence (confidence: lead-only).** Reported at the controls in C5 with the authored
`far_fade_range` applied, filed at the close of `PLAN-M5-polish-3` (`git log --grep=BL-538`): the
larger gamez buildings show a dark area at the range where the downtown clutter vanishes, with
buildings nearer and farther reading brighter. The dither itself is not the fault; it reads as the
original's fade in motion. Three candidate mechanisms are named and none is confirmed: the
collapsed clutter cards may still write depth or a dark fragment behind the band (the
`csky_clutter_fade` cutout keeps a card in the pass until `step(d, far)` culls it, and a card
collapsed to zero size should contribute nothing); the fog-volume clutter's own `far_fade` may
overlap the templates fade at that range; or the gamez buildings may carry a `far_fade_range` of
their own that the remake ignores, since `FUN_004d5de0` applies the scaled test to every type-5
scene node rather than only to clutter.

**Outcome: a disproof, no code.** All three candidates are ruled out and the darkening is not in the
clutter pass at all. It is the **shipped hand-authored mip chain of C5's `cblock*` facade textures,
whose level 1 is a non-monotone dip**: `--dump-mips` reads `cblock1` at mean luminance 15.62 (L0) →
**4.41** (L1) → 6.77 (L2), and `cblock2` at 9.60 → **0.83** → 1.84. A facade near enough to sample
L0 reads bright, one in the L1 band reads three to eleven times darker, and one far enough for L2
reads brighter again, which is the reported shape exactly. `--mips=generated` (the box filter)
removes the band completely; turning `graphics.clutterFarFade` off does not touch it. The chain is
installed correctly (every `installed` line in the dump reads `== authored`), so this is the
original's own art reaching the screen, not a loader fault.

How each candidate falls:

- **Collapsed cards writing depth or a dark fragment, disproven by measurement.** At the pose
  below, the fade-on frame and the `--no-clutter` frame are pixel-identical (mean absolute
  difference 0.00, zero changed pixels) across every row where the fade has culled every instance,
  while the fade-off frame fills those same rows with buildings. A collapsed card contributes
  nothing.
- **The fog-volume clutter overlapping the templates fade, disproven on the data.** C5's
  `fogvol.zrd` field is 16,170 cloud sprites over 17 volumes at `fade 1200-1800 m`, well outside the
  200–900 m the templates author, and they are clouds rather than buildings.
- **The gamez buildings wanting a `far_fade_range` of their own, disproven by decode.**
  `FUN_004d5de0` is reached from two places only, `FUN_004d5d90` (the per-cell clutter instance list,
  `+0x40` count over `+0x44` at stride `0x60`) and the clutter quadtree `FUN_004d6010`, and
  `FUN_004d5d90` is itself called once, from the world walk `FUN_004d5910`, behind the
  `CameraRenderClutter` global. Ordinary scene nodes draw through `FUN_004d4a20` in that same walk
  and never reach the fade test. The `node type == 5` compare inside `FUN_004d5de0` selects how the
  instance's transform is written, not which nodes the fade applies to, and the near/far/reciprocal
  triple it reads (`+0xd`/`+0xe`/`+0xf`) is the stamper's own step-11 output. The original does not
  fade a non-clutter building.

The alpha-texture lighting gate of [`org/vertexLighting.md`](org/vertexLighting.md) is ruled
out too: the band is a function of camera distance and it disappears under a mip-policy switch that
changes no lighting term. It is not `BL-613`.

The buildings themselves were misread in the report: C5's towers **are** clutter. With
`graphics.clutterFarFade=false` the whole city stands out to the fog wall, so every one of them is a
templates stamp, and the two families the data authors (`cb02/03/04/05b/07-10/17/19/20` at 700–800 /
800–900 m against `cb00/01/06/11-16/18/21-24` at 200–300 / 300–350 m) are why the low city thins out
several hundred metres before the towers do.

**What stays open** is the judgement the mechanism hands back, and it needs the user at the
controls: the L1 dip is the original's art, so honouring it is right, but nothing here settles at
what distance it is *meant* to be reached. `BL-538` is rewritten onto that half.

**Model recommendation.** High. Three live candidates, a rendering pass that other items in this
wave also touch, and a real chance the answer is a decode rather than a fix.

**Verify.** The C5 pose that reproduces the band, run from the repo root with
`$env:CSVM_DATA_ROOT` set in a worktree:

```
.\RunProbe.ps1 --freecam --chapter=C5 "--pos=-9491,140,-3479" "--direction=-0.588,-0.03,-0.809" `
    --det --mute "--screenshot=.scratch\a3-band.png"
```

Add `--mips=generated` for the A/B that identifies the band, and put
`{"graphics":{"clutterFarFade":false}}` in `CSVM/config.json` with `--no-det --seed=1 --jitter=0
--no-pads` for the fade pair (`--det` drops config overrides, so the pair cannot be taken under it;
a `--det`/`--no-det` control at the same settings differs by 12 of 921,600 pixels).

**⚠ Traps.** The dither is not the fault and must not be changed to chase the band. This item and
A1/A2 all end in the world pass, so a fix here that moves the measured facade or water numbers has
broken one of those items rather than fixed this one; take A1's and A2's numbers as a control if
they have landed. A screenshot pair proves which pass owns the band and nothing more, so do not
read the on/off difference as the mechanism. ⚠ Decorrelating the dither lattice per instance (a
phase off the stamp origin, so overlapping stamps at the same alpha stop discarding the same screen
pixels) was tried as a probe and changes the frame barely at all; it is not the mechanism and the
dither still must not be touched.

**Verified.** <pending orchestrator run>

---

# Wave B — Effects and the animation scope

## B11 ◐ `BL-546`: a nitro engage produces no prop swap and no exhaust smoke

**Partly landed — the exhaust smoke half only; the prop swap is still open and carried forward
as the restored `BL-546`.** Marked ◐, not ☑: the visible symptom the user reported ("no prop
swap") is not delivered by this change, so calling it done would read wrong to anyone checking
this item against the game.

The def never started at all: `nitro_boost`/`nitro_decay` author their anchor as NAME `warhawk`
(`plane_props.zrd`), which never resolves inside a per-plane crash rig's own index (that index is
built only from the flown aircraft's own subtree, and no `player_*` model's own node is named
`warhawk`) — the identical shape `startprops`/`stopprops` carry in the same file. Those two work
because `FlightController.Respawn` plays them with `AnimRuntime.Play(name, PlaneModel,
applyReset: false)`, whose fallback-to-anchor kicks in once the NAME search comes up empty;
`AdvanceNitro` instead called `PlayWithin(PlaneModel, name, …)`, which has no such fallback and so
silently started nothing. The fix swaps both nitro call sites (engage and decay) from
`PlayWithin`/`StopWithin` to `Play`/`Stop` (`FlightController.cs`, `AdvanceNitro`), matching the
working sibling pattern. That restores the part of `nitro_boost`'s own sequence that targets
nodes every flyable model carries: the `nitropuffN` exhaust puffers at `exhaust1..4`. Regression:
the `nitro-boost-anchors` suite, which builds the real `player_warhawk` rig, plays `nitro_boost`
through the production call shape, and asserts the exhaust puffer starts sustaining.

The prop swap is NOT fixed by this change and is not yet understood well enough to fix. No
flyable `player_*` airframe's own built model carries the `nitropropN` disc nodes the def
cross-fades in (confirmed empirically across all eleven `player_*` rigs against `planes.zbd`),
even though `extracted/planes/nodes.json` declares 34 `nitropropN` nodes total and carries both a
bare-named root and a `player_*` root for nearly every aircraft. Whether the original ever showed
this disc swap on a flyable aircraft, or only ever ran it on the bare library model, is an open
question this pass did not settle; see the restored `BL-546` for the two competing readings and
what would separate them.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

**Original approach (kept for reference).**

**Goal.** Engaging the boost swaps the `nitropropN` discs in, ramps them up, spins them, plays
`snd_nitrostart`, and reverses all of it on decay, on the human flight path.

**Evidence (confidence: traced).** Every part of the wiring is present, which is what makes this an
item rather than a feature request. The `nitro_boost` def ships in `plane_props.zrd` as
`LOCAL_NODES_ONLY` / `ACTIVATION ON_CALL` / `AUTO_RESET_NODE_STATES OFF`, and its sequences set
`OBJECT_ACTIVE_STATE nitropropN ACTIVE`, ramp `OBJECT_OPACITY_FROM_TO` 0 to 1 on the same discs,
spin them through `spin_nitrorotorN`, and play `snd_nitrostart AT_NODE nitroprop1`; `nitro_decay`
reverses it. The airframes carry 34 `nitropropN` nodes between them. `PlaneBuilder` classifies the
disc and builds it hidden for that def (`PlaneBuilder.cs:275-277`, `PropParts.cs:25`),
`EffectCatalogue.NitroAnims` binds both defs, and `FlightController` plays them off the
`NitroSystem` edges (`FlightController.cs:1727-1741`). The data is authored, the node is built, the
def is bound and the call site fires, so the break is between the call and the frame. The boost
does accelerate the aircraft and the dial reads correctly, so `NitroSystem`'s own state machine is
confirmed working and the defect is downstream of the edge.

**Approach.** Establish which half fails before changing anything: engage the boost with
`--debug-anim` and see whether `nitro_boost` starts at all. If it does not, the suspects are
`CrashRuntime` or `PlaneModel` being null on the human flight path, or `PlayWithin` failing to
resolve `nitropropN` inside `PlaneModel`. If it does start, the disc is being activated and then
drawn invisible, which points at `OBJECT_OPACITY_FROM_TO` against a material with no transparency,
or at the hidden build state surviving the `ACTIVE` event. Settle the prop first; the smoke is a
second question and the prop is the one whose whole chain is already readable.

**Model recommendation.** Medium. The chain is fully traced and the diagnosis is a two-branch split
that one instrument settles; the work after that is ordinary.

**Verify.** A boost engage and decay in flight with the discs visible, spinning and audible, then
gone again on decay. <TODO: name the exact launch command and the chapter/plane the check flies.>
Then the `--run-tests` suites over the anim runtime and the full gate.

**⚠ Traps.** **The absence of a `nitro engaged` line proves nothing**: `FlightController.cs:1733`
logs through `Log.Debug("flight", …)`, which the file sink does not take, so do not conclude the
edge never fired from a quiet log. The `ai_nitro_boost` and `ai_nitro_decay` wrappers in the same
file are retargeting shims the executable never references, so do not wire the AI to them while
chasing this. The smoke half may belong to a different def and is not evidence about the prop half.

## B12 ☑ `BL-587`: chapter-scope animation files no mission lists still run

**Landed.** The reader gate `AnimProgram` already applied to the shared scope (`ListedSharedFiles`)
now applies to the chapter scope too (`ListedChapterFiles`, same shape: the chapter's own
`cam_anim.zrd` listing plus any chapter files a mission's `mis_anim.zrd` adds directly), gated
behind the same `haveMissionManifest` condition so a reader-only extraction still degrades to its
old ungated behaviour. `AnimRuntime` gained a matching `ChapterFilesSkipped` census line beside
`SharedFilesSkipped`.

**The decode.** Every mission's `mis_anim.zrd` and every chapter's `cam_anim.zrd` across the whole
install was checked for the files this item names, not sampled: none of C1's seven missions (IA1,
M02, M04, M05, MP1, MP2, MP3) references `clouds`, `lightning`, `spotlights` or `train_smoke`
anywhere. The original's compiled archives derive from exactly these lists, so the original
genuinely never runs them; this was the open question the item's own trap raised, and the decode
answers it rather than the fix special-casing anything.

**New finding beyond the item's original evidence.** The 8-chapter `--freecam` regression this
change was verified against found three more chapter files gated by the same mechanism that
`BL-521`'s close did not name: C3's `flag_british`, `flag_rollout` and `hydrogen`. No mission's
`mis_anim.zrd`/`cam_anim.zrd` in C3 lists them either, so they follow the identical traced
mechanism as the C1/C2/C4/C5 files this item was filed on; they are not a separate item.

**Measured, before vs after the gate (`anim:` census line, `CSVM_DATA_ROOT` set, same poses,
zero errors both ways in all eight chapters):**

| Chapter | Files newly gated | Anim defs (before → after) | Anchored (before → after) | Destructible instances (before → after) |
|---|---|---|---|---|
| C1 | `clouds`, `lightning`, `spotlights`, `train_smoke` | 666 → 660 | 361 → 360 | 186 → 186 |
| C1B | (none) | 476 → 476 | 169 → 169 | — |
| C1C | (none) | 476 → 476 | 169 → 169 | — |
| C2 | `game_targets`, `police_blockade`, `police_blockade1`, `police_blockade2`, `police_destroy`, `security_destroy` | 632 → 584 | 326 → 279 | 192 → 176 |
| C2B | (none) | 477 → 477 | 169 → 169 | — |
| C3 | `flag_british`, `flag_rollout`, `hydrogen` | 735 → 731 | 428 → 425 | 210 → 210 |
| C4 | `bhmhookup`, `bhm_warhawks` | 586 → 564 | 268 → 254 | 92 → 92 |
| C5 | `steinmann` | 633 → 624 | 325 → 318 | 166 → 158 |

Puffer emitter and point-light counts are identical before/after in every chapter (the gated files
carry no puffer/light defs); gamez node and mesh-instance counts are untouched by construction,
since `AnimProgram` never builds or removes world geometry, only toggles state on nodes
`SceneBuilder` already built.

**The C1 cloud montage and the user's judgement.** No pinned golden's `--freecam` pose reaches
`cloudparent`'s 1069.7–1875.6 m altitude band, so no golden hash moved. But the overcast-match
milestone (`docs/plans/PLAN-overcast-match.md`) was judged at a pose that does reach it
(`--pos=-7323,1192,-3829 --direction=0,0,-1`), with C1's `cloudparent#` 0.6 opacity — sourced from
`clouds.zrd`, one of the files this gate stops loading — in place. A before/after montage at that
exact pose was put in front of the user rather than assumed safe: at a glance the two frames read
as the same overcast bank, but a pixel diff shows a real, `cloudparent`-shaped difference
concentrated on the cloud facades (more contrast, brighter lit faces and darker undersides, once
the 0.6 blend is gone). **The user has seen the montage and approved the cloud change.** Screenshots
and the diff stayed in the landing worktree's `.scratch/b12/` (gitignored, not part of this
landing): `c1-clouds-before.png`, `c1-clouds-after2.png`, `c1-clouds-before-after-montage.png`,
`c1-clouds-diff.png`.

**Suites checked.** `mission-off-turrets` (the closing suite for `BL-521`'s shared-scope gate this
mirrors) and `.\RunTests.ps1 -Quick` (226 units, 13 engine suites) both green; `dotnet build` 0
warnings; `CheckCommentCaps.ps1 -Summary` clean.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

**Original approach (kept for reference).**

**Goal.** A chapter-scope animation definition file that no mission's `ANIMATION_DEFINITION_FILE`
list names does not load, matching the original, whose compiled archives derive from those lists
and so never run an unlisted file.

**Evidence (confidence: traced to the data).** The shared scope is already gated on the lists a
mission sees (`AnimProgram`, and `docs/formats/anim-definitions.md` under "Shared-scope files are
listed per mission too"); the chapter scope stays unconditional. The unlisted chapter files are
C1's `clouds`, `lightning`, `spotlights` and `train_smoke`; C2's `game_targets`, `police_*` and
`security_destroy` (M01 and M02 list two of them); C5's `steinmann` (M01 lists it); and C4's
`bhmhookup` and `bhm_warhawks` (M04 lists them). Filed at the close of `BL-521`
(`git log --grep=BL-521`). <TODO: re-verify still-open against the code.>

**Approach.** Apply the same file gate the shared scope already uses to the chapter scope. The
change is small; the work is in the verification, because it removes content from four chapters at
once.

**Model recommendation.** Medium. The code change is mechanical and the decode is already written
down; the judgement is entirely in reading what disappears from the goldens.

**Verify.** The full 8-chapter `--freecam` regression, read for what stops being built rather than
for errors, plus the C1 goldens specifically. <TODO: name the C1 goldens in
`analysis/goldens/manifest.json` that carry `cloudparent` cards.> The user's eyes decide before it
lands, because of the trap below.

**⚠ Traps.** C1's `cloudparent#` 0.6 opacity comes from `clouds.zrd`, which is one of the unlisted
files this gate would stop loading, and the overcast match
(`docs/plans/PLAN-overcast-match.md`) was judged with that opacity in place. So this item can
regress a milestone that was closed at the controls: the C1 goldens and the user's judgement decide,
and a green test run is not sufficient to land it. If the C1 clouds do change, the question the item
raises is whether the original really ran without them, not whether to special-case `clouds.zrd`.

## B13 ◐ `BL-535`: a repeat sonic burst pays a 10 to 14 ms slot re-reset once the pool recycles a still-live slot

**Goal.** A sonic burst that recycles a still-live pool slot costs what a burst onto a fresh or
idle slot costs: about 0.6 to 2.7 ms of `effect_checkout`, rather than 10 to 14 ms (or worse under
instrumentation overhead, up to 38 ms measured — see below).

**Evidence (confidence: traced, and the mechanism is corrected from this item's opening text).**
Re-verified open against the code in this session. With every emitter pre-built at bind
(`AnimRuntime.PrewarmEmitters`), the weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08
--weapon-fire --infinite-ammo --weapon-surface=default --weapon-standoff=90 --no-det --no-vsync
--seed=1 --no-pads --frames=1200 --screenshot=<path>`, `hitchMonitor.floorMs` 10 and
`medianMultiple` 1.2 in `CSVM/config.json`) reproduces the symptom: the `.hitches.jsonl` sidecar
names `effect_checkout` samples of 15.6 ms (sim frame 464) and 12.2 ms (sim frame 1154), both well
clear of the lowered 10 ms floor; the stock monitor never trips (PERF-14 still holds).

A per-burst `Stopwatch` placed around `ResetCheckedOutCopies`' own def/anchor loop (11 rocket
launches, one `PlayEffectAt` each) shows that loop is NOT the cost: `closureDefs=17` and
`matchedAnchors=17` on every single burst, and `Stop`/`RestoreRestPoses` together cost under 3 ms
throughout. The cost sits inside `ApplyInstant`'s `RESET_STATE` dispatch, and within that almost
entirely in one event kind: `ObjectOpacityState` on `sonic_ring1`..`sonic_ring5` (`PoseChannel.
HandleActiveState` → `SetSubtreeOpacity` → `ApplyOpacity`, a plain recursive `GetChildren()` walk
over the ring's own subtree calling `SetInstanceShaderParameter`/`EnsureOpacityPath` on every
`GeometryInstance3D`, plus `WorldCollision.SetFaded` → `SyncSubtree`/`FadedAbove` re-deriving every
collider under the ring). `EnsureOpacityPath`'s own `Shader.Code.Contains` scan, timed separately,
is NOT the bottleneck (0.06–0.8 ms, mostly under 0.1 ms) — the cost is in the walk-and-resync
itself, not in re-deriving fade-capability.

⚠ **This corrects the item's opening claim that the step is "neither a wrap nor construction."**
Correlating the per-burst timings against the log's own recycle line shows the jump lands exactly
at the wrap, not two bursts before it: burst 4 (`totalMs=2.236`, cheap) is immediately followed by
`DEBUG [anim] anim: effect pool for 'sonic_ground_effect' recycled slot 0 of 4 while it was still
live — overlapping calls beyond the pool size share a copy again`, and burst 5 is the first
expensive one (`totalMs=13.315`, `applyMs=9.740`), with every burst after it landing on an
already-recycled, still-live slot and costing 11.8–30.3 ms depending on run (30 ms figures are from
a build carrying diagnostic `Stopwatch`es of their own and are not the clean number; the two clean
sidecar samples above, 15.6 and 12.2 ms, are). This is consistent with, not contrary to, the
mechanism `ResetCheckedOutCopies` exists for: `sonic_ground_effect`'s pool (root default, 4 base
slots at 1 player) has no idle gap between bursts at this fire rate, so from the wrap on every
checkout inherits a ring mid-fade rather than one already at rest, `SetSubtreeOpacity`'s
last-pushed-value cache (`_opacity`) misses, and the full walk runs every time.

**Approach.** The def/anchor walk in `ResetCheckedOutCopies` is already cheap and bounded (17/17,
constant); it is not what to narrow. The candidate fix sits one level down, in
`PoseChannel.EnsureOpacityPath`/`WorldCollision.SetFaded`, and needs its own isolated
before/after timing (split `ApplyOpacity`'s material/shader-param cost from `SetFaded`'s
collider-resync cost, which this session did not separate) before changing either, since both are
shared machinery used far beyond the sonic burst — `WorldCollision._fadedRoots` in particular is a
single process-wide counter, so its `FadedAbove` ancestor walk degrades for every faded object in
the world, not just this one, once more than one is faded at a time.

**Model recommendation.** Medium-to-large now that the mechanism is one level deeper than
`ResetCheckedOutCopies` itself: the fix is in shared opacity/collision-sync plumbing other callers
depend on, so isolating `ApplyOpacity` from `SetFaded` and re-verifying broadly (not just
`effect-pool-reset`) is real work.

**Verify.** The same weapon-lab probe with `hitchMonitor.floorMs` at 10 and `medianMultiple` at 1.2
under `--no-det --no-vsync`, reading the `.hitches.jsonl` sidecar for `effect_checkout` samples
rather than console hitch lines (the stock monitor never trips, and console noise from the lowered
floor is heavy). Take the baseline first. Then the `effect-pool-reset` suite (the pose contract)
and the full gate.

**⚠ Traps.** `docs/verification.md` PERF-14: the stock probe cannot fail on this, so a green stock
run is not evidence. The lowered monitor is the only instrument that sees it and it needs both
knobs, because the trigger is the larger of the floor and the median times the multiple. The
re-reset exists to stop the rings vanishing from the fifth burst on, so a fix that makes the cost
go away by resetting less must be checked against that symptom, not only against the timing — and
per the corrected evidence above, "the fifth burst on" is not a coincidence with this item's own
cost step, it is the same event (the wrap), so the two are not independent things to guard
separately. `--frames=N` alone never quits the probe; it needs `--screenshot=<path>` too (only the
pairing ends the run at N sim frames — `docs/tooling.md`'s perf-stage note), or `RunProbe.ps1` hits
its `-TimeoutSec` and is killed with nothing captured.

**Verified.** <pending orchestrator run>

---

# Wave C — The allocation and tick budget

## C21 ☑ `BL-536`: the gen1/24-29 ms reading is the session build settling; the settled pause is set by finalizable-object count

**Landed.** The premise the item was filed on does not survive a capture that runs long enough.
"Every collection is gen1, about 30 MB promoted, about 45k pending finalization, 24 to 29 ms every
12 s" is the **world build settling**, and it ends. A GC-verbose capture opened at process start
shows seven collections in the first three seconds of session life (six gen1 and one gen2,
18 to 52 MB promoted, 20 to 45 ms), then a gen1 at 34 MB and 26 ms, then a gen1 at 9 MB and 20 ms,
and from about 50 s of process life onward nothing but **gen0**, promoting 2.7 MB with a 10 to
13 ms pause every 13 s. Both regimes reproduce to the byte between independent runs (the first
settled collection promotes 2.69 MB over 26,053 finalization-promoted objects in each), so the
short capture the item was filed on was not noisy, it was measuring the transient.

**What promotes.** Nothing is unaccounted for. `GCMarkWithType` attributes the settling
collections' promotion to stack and older-generation roots, which is the built world being tenured,
and gen2 grows from zero to about 70 MB across them and then stops. In the settled regime the
2.7 MB breaks down as 1.73 MB from older-generation roots, 0.95 MB from finalization promotion and
0.08 MB from handles, which is the whole of it. The finalizable Godot wrappers do survive their
first collection by construction, as the item said, but they are 0.95 MB of it, not 30 MB.

**Registration or marking: neither.** Registration onto the finalization queue happens at
allocation, outside the suspension window, so it cannot appear in a `GCSuspendEE` to
`GCRestartEEStop` interval at all. `MarkFinalizeQueueRoots` promotes 0.00 MB at every one of the 20
collections captured, so it is not resurrection marking either. What the pause does track is the
finalization-promoted object COUNT: across two builds and 26k to 85k objects per collection it
holds at 0.4 to 0.5 ms per thousand, while ms-per-promoted-MB varies several-fold over the same
collections. The cost is the per-object queue walk and the move to the ready-to-finalize list.

**The consequence, and it is the useful part.** The pause is set by how many finalizable objects
died, and a settled flight retires about 2000 a second whatever else changes, because every Godot
wrapper is finalizable and drags Godot's own instance-tracking weak references with it. So cutting
ordinary allocation cannot shrink the pause; it can only batch the same work into fewer, longer
ones. Two allocators were cut anyway, since both were plain waste:

- `AnimInstance.Live()` was a `yield return` iterator called once per live instance per frame from
  the retirement scan, and was the largest single allocator of a flight session at 39 MB of a
  134 MB sampled window. It is now a struct enumerator, so the call sites are unchanged and the
  allocation is gone.
- `GaugeCluster.DrawGaugePoly` built a fresh `Vector2[]` and `Color[]` per polygon per draw, 40 MB
  of the same window. `DrawPolygon` marshals both into packed arrays before it returns, so they are
  now scratch buffers kept per vertex count. All 16 goldens stay hash-identical.

`GodotWorldQuery.Ray`, the item's first suspect and the one whose fix carried a re-entrancy risk,
was left alone: the ray-query objects are **1.6%** of sampled allocation
(`PhysicsRayQueryParameters3D` 0.43 MB, the result `Dictionary` 0.43 MB, `Array<Rid>` 0.53 MB,
`PhysicsShapeQueryParameters3D` 0.75 MB, against 134 MB), so reusing a query object per caster
would have bought nothing and could have corrupted a query. `Godot.StringName` is the larger
finalizable source and is where a follow-up would go.

**Baseline and post-change numbers**, both from
`--fly --chapter=C1 --plane=player_bhawk --perf --no-vsync`, `dotnet-trace` on
`Microsoft-Windows-DotNETRuntime:0x1:5`, compared within the one vsync mode (PERF-13):

| | baseline | after |
|---|---|---|
| sampled allocation | 3.84 MB/s | 1.53 MB/s |
| settled collection | gen0 every 13.1 s | gen1 every 31 to 36 s |
| promoted | 2.69, 2.76, 2.84 MB | 7.82, 6.93, 7.39 MB |
| finalization-promoted | 26053, 26328, 26404 | 63859, 67674, 72803 |
| pause | 9.80, 11.97, 12.65 ms | 27.11, 25.43, 27.18 ms |
| total pause per wall second | 0.88 ms | 0.79 ms |

The per-collection pause is larger and the total per second is not, which is the trade the finding
predicts. In dropped frames it is a mild improvement: one dropped frame every 13 s becomes two
every 33 s. The item's goal as written, that the 24 to 29 ms pause goes, is not reachable by
attacking allocators, and the reading it was written from was the transient.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

**For C22.** This item changed nothing in `GodotWorldQuery` or on the physics tick, so C22's
baseline is unaffected by it; the `--fly` C1 `physics_ms` sat at 0.06 to 0.09 ms throughout, and the
per-sim-step ray casts are not a meaningful allocator, so C22 should not expect allocation to
explain its 39 ms step. The instrument used here is worth reusing: a `dotnet-trace` GC-verbose
capture parsed for `GCStart`, `GCHeapStats` and `GCMarkWithType`, with the window opened at least
50 s after launch so it does not land in the settling regime (PERF-19, PERF-20).

**Original approach (kept for reference).**

**Goal.** Collections in flight stop being uniformly gen1 with about 45k objects pending
finalization and about 30 MB promoted, and the 24 to 29 ms pause every 12 s goes.

**Evidence (confidence: traced).** The 90 to 120 ms stall every 190 frames this item was filed on is
already gone: it was the `EXECUTION_BY_RANGE` sweep re-measuring 69 deferred anchors' mesh bounds on
every 8 m cell crossing, about 25 MB/s of finalizable `StringName` and `Godot.Collections.Array`
wrappers, now measured once per anchor (`AnimRuntime._rangeOriginLocal`,
`git log --grep=BL-536`). What remains is under `HitchMonitor`'s 40 ms floor and no longer trips,
but a `dotnet-trace` GC-verbose capture on `--fly --chapter=C1 --plane=player_bhawk --perf
--no-vsync` still shows every collection as gen1 (`gc0_delta` and `gc1_delta` move together),
`FinalizationPendingCount` about 45k at each one, and a residual 1.7 MB per 60-frame `--perf`
window. The sampled allocators are `GodotWorldQuery.Ray` (a `PhysicsRayQueryParameters3D`, an
`Array<Rid>` and a result `Dictionary` per cast, several casts a sim step), `AnimInstance.Live()`
(an iterator per frame from `AnimRuntime.Retirable`) and `GaugeCluster.DrawGaugePoly` (a `Color[]`
and a `Vector2[]` per polygon per draw). <TODO: re-verify still-open against the code.>

**Approach.** The question is what promotes about 30 MB into gen1 per collection when the allocation
rate is 3 MB/s. Finalizable Godot wrappers survive their first collection by construction, so the
ray-query objects are the first suspect and the first fix shape is reusing one
`PhysicsRayQueryParameters3D` per caster. Then establish whether the 24 to 29 ms pause is the
finalizer queue's registration rather than marking, because those want different fixes. Take the
three sampled allocators in that order; `GaugeCluster.DrawGaugePoly` may be contended by the
`bl-431-cockpit-gauge-drive` worktree. <TODO: check whether `bl-431-cockpit-gauge-drive` has
unmerged work in `GaugeCluster` before touching it; it had none at the time this plan was written.>

**Model recommendation.** High. Three allocators, a promotion mechanism that is not yet established,
and a measurement discipline where the wrong comparison silently produces a false result.

**Verify.** A `dotnet-trace` GC-verbose capture on the same `--fly --chapter=C1
--plane=player_bhawk --perf --no-vsync` run, showing `gc0_delta` and `gc1_delta` separating and
`FinalizationPendingCount` down, with the baseline capture taken first. Then the full gate, and
C23's assertion green.

**⚠ Traps.** `docs/verification.md` PERF-13: compare only within one vsync mode. The residual pause
needs `hitchMonitor.floorMs` lowered to be seen at all, which is PERF-14's lowered-monitor caveat,
so a stock run showing no hitch is not evidence of a fix. `BL-355` (closed) is the capture that
first showed the unexplained trip and its record should be read before re-deriving any of this.
Reusing a query object per caster is only safe if no caster is re-entrant within a sim step;
establish that before doing it.

## C22 ❌ `BL-562`: the 39 ms is Godot's 1 Hz worst-tick monitor; the step is 1.8 ms and the sim keeps up

**Disproven as stated, on both legs.** Re-verified against the code first: nothing in the physics
chain had moved, C21 changed neither `GodotWorldQuery` nor the tick, and Godot's physics tick rate
and `max_physics_steps_per_frame` are still at the engine defaults (60 Hz, 8) because
`CSVM/project.godot` carries no `[physics]` section and no code writes
`Engine.PhysicsTicksPerSecond`.

The item's 39 ms came from `physics_ms`, which is Godot's `TIME_PHYSICS_PROCESS` monitor. That
monitor holds the **worst single physics tick of the last wall second** and refreshes about once a
second, so it is neither a per-frame cost nor a mean. Three independent readings say so, and the
first two need no new code: a `--fly --chapter=C1` window reported `physics_ms=27.52` while the
worst *frame* in that same window was `max_ms=8.33`, which no per-frame cost can do; and the flown
CM11 log repeats one `physics_ms` value byte-for-byte across every hitch record of a second
(frames 15802 to 15811 all read `physics_ms=144.94` against frame costs of 43 to 167 ms).

The third reading is the instrument this item lands. `PhysicsTickCost` brackets the whole physics
tick between two nodes at the extremes of Godot's physics priority order, so the pair spans every
`_PhysicsProcess` callback wherever in the tree it sits, and `--perf` gained three terms from it:
`phys_tick_ms` (the mean measured wall cost of one tick), `phys_tick_max_ms` (the worst tick in the
window) and `phys_hz` (the ticks the window actually got per wall second). Over 333 windows of a
loaded CM11 session:

| | median | p95 | max |
|---|---|---|---|
| `physics_ms` (Godot's monitor) | 16.96 ms | 29.37 ms | 163.39 ms |
| `phys_tick_max_ms` (measured worst tick) | 15.48 ms | 26.64 ms | 177.37 ms |
| **`phys_tick_ms` (measured mean tick)** | **1.81 ms** | **3.20 ms** | **5.79 ms** |
| `frame_ms` | 13.48 ms | 25.59 ms | 37.38 ms |

The monitor tracks the measured *worst* tick, not the mean. The step the 16.7 ms budget is written
against is **1.81 ms**, about a ninth of it, so there is no budget overrun to fix.

**The second leg fails on its own measurement.** `phys_hz` read a median of **60.0** and a minimum
of 58.6 across those windows, and the session's aggregate was **18,404 physics ticks over 306.7 wall
seconds, so 306.7 sim seconds, a sim-to-wall ratio of 0.9999**, with the roadblocks woken, the
trailer segments running and the five Firebrands activated by objective 57. A realtime clock
advances the physics-stepped sim exactly one `1/60` step per tick (`GameClock.PhysicsDt` returns
Godot's fixed delta, and `CampaignDirector.Step` accrues the mission's `TimeMs` off that same dt),
so 60 ticks a wall second *is* real time. The sim was never running at half speed here. The
original session's "2770 rendered frames covered 52 sim seconds" is 53 rendered frames per sim
second, which at that session's own frame costs is a shortfall of tens of percent in its heaviest
segment, not a factor of two; over its whole length that log shows 815 sim seconds against about
875 seconds of session, and it holds two mission attempts rather than one.

**Attribution across the five named candidates**, from a 40 s `dotnet-trace`
`dotnet-sampled-thread-time` capture of the loaded session, taken 130 s in so it cannot land in the
world-build settling regime (PERF-19), and read only as shares *within* the physics stack: the
capture's absolute totals do not reconcile with the bracket (it charges 49 % of sampled thread time
to physics where the bracket measures 11 % of wall), so the bracket is the authority on cost and
this is the authority on proportion.

| candidate | share of the physics tick | reading |
|---|---|---|
| the AI mode machines | `SelectRankedTarget` **23.7 %** (of which `AimCandidateSet.AddStructures` 14.5 %) | the machines themselves do not appear; the target ranking bolted beside them is the largest single consumer |
| the six flight models | inside `FlightController.SimStep`'s 30.6 % exclusive bucket, alongside its transform writes | no separate cost worth naming |
| the projectile sweeps | `ProjectilePool.SimStep` **3.4 %** | one ray per live round per tick |
| the objective graph's per-tick scans | **below 1 %**, absent from the top 30 | `NameResolver.FindAll` is memoised, so the 61-objective scan is dictionary probes |
| the trailer's puffer emitters at 1 m intervals | `AnimRuntime.HandlePufferState` **3.1 %**, `EmitterDirector.Tick` 0.8 % | the emission is a floor division; the 2000-particle integration is in `_Process`, not on this tick |

Two the item did not name: `ZeppelinRuntime` **8.9 %** (almost all of it `Place`) and
`GodotWorldQuery.Ray` **13.4 %** across all callers, `ProbeGroundBlow` being 9.4 % of the tick on
its own. So the ray *casts* do cost real physics time even though C21 showed the ray *objects* are
not a meaningful allocator. But 13.4 % of 1.81 ms is 0.24 ms, and removing the whole ranking pass
would take the step from 1.81 ms to about 1.38 ms. Nothing here is worth optimising against a
16.7 ms budget, so no fix lands and none should.

**What does land.** The instrument (`CSVM/src/Utils/PhysicsTickCost.cs`, the two bracket nodes added
in `Launcher._Ready`, the three `--perf` terms, five units in `PhysicsTickCostTests`), the rule
(`docs/verification.md` PERF-21, with `docs/cli.md` and `docs/tooling.md` corrected, since both told the
reader `physics_ms` was empty under `--det` and nothing about what it means when it is not), and the
regression gate below.

**The perf scenario, and the honest limit on it.** `c2m02-hollywood`
(`--chapter=C2 --mission=M02 --plane=player_bhawk --hold=0.2,0.1,0,1`) is in
`analysis/perf/scenarios.json` and runs clean. It gates the **world** CM11 is built on: the
chapter's node census, its 52 world emplacements and the destructible registry the ranking pass
scans, which is what the tick's cost scales with. It cannot gate the flown mission: the roster and
objectives need a `CampaignProfileStore` profile under `user://Profiles/`, which is machine state
the manifest has no way to create, and the campaign session brings the node count from 12,583 to
28,640. That gap is stated in the scenario's `exercises` rather than hidden. The three new `phys_*`
terms are registered as awareness metrics with their reasons: the harness runs `--det`, which makes
the clock parent-driven, so the tick is empty there (`phys_tick_ms` reads 0.034 ms) and these are
numbers for a `--no-det` hand-run.

**Verified.** <pending orchestrator run>

**What could NOT be verified here.** Nobody flew the mission. The loaded state was reached with
`--debug-objective=18`, which drives the trailer chain and, 115 s later, objectives 19 → 58 → 57 and
the five Firebrands; the player aircraft flew itself, crashed early and respawned, so no sustained
firefight, no burst of live projectiles and no destruction cascade is in these numbers. The
attribution therefore under-weights `ProjectilePool` and the debris motions relative to a real
engagement. It also means the residual below is characterised from spikes this run happened to hit,
not from the ones a pilot provokes.

**The residual, which is real and is not this item's claim.** `phys_tick_max_ms` reached 72, 102 and
177 ms on single ticks. A tick that long costs the sim time for a different reason than a slow
average: Godot caps catch-up at 8 steps per frame, so a 177 ms stall discards about six sim steps
outright. That is a spike problem in the shape the C21 agent's chain-map predicted
(`NameResolver.ClearFindCache` turning the next tick's name resolutions into a walk of the ~5000-row
index with an `IsInstanceValid` per row, on the spawn and warm-up paths), and it is what `BL-562`
is rewritten onto.

**Original approach (kept for reference).**

**Goal.** The late-CM11 physics step comes under the 16.7 ms budget on the reference rig, so the sim
clock tracks the wall clock and the mission takes as long to play as its `TimeMs` records.

**Evidence (confidence: traced).** A flown CM11 (C2/M02) session's hitch records
(`.scratch/logs/game-*.out`, the `[perf] hitch … physics_ms=…` lines) show the frame baseline rising
from 9 ms at launch to 30 to 40 ms with `physics_ms` at about 39 ms of it, once six aircraft, the
trailer's dust puffers and the roadblocks are live; 2770 rendered frames then covered 52 sim seconds
(one parked-plane `flight:` line per sim second). Godot caps physics catch-up per frame, so a
physics-bound frame lets the sim clock fall behind the wall clock: the mission takes about twice as
long to play as its `TimeMs` records, and every `_Process`-driven consumer still reading wall time
drifts against the aircraft. This is why the animation runtime moved onto the physics tick
(`AnimRuntime._PhysicsProcess`, the `anim-clock-realtime` suite).
<TODO: re-verify still-open against the code.>

**Approach.** Profile one CM11 session past the roadblocks with `--perf` and the hitch sidecar's
`samples`, and attribute the physics step across the `FlightController._PhysicsProcess` chain: six
flight models, the AI mode machines, the projectile sweeps, the objective graph's per-tick scans,
and the puffer emitters running at 1 m distance intervals on the trailer. Bring the step under
16.7 ms on the reference rig. A perf scenario in `analysis/perf/scenarios.json` for the late-CM11
state is the regression gate and should land with the fix.

**Model recommendation.** High. The attribution is open across five subsystems and the fix is
whichever of them the profile names, which is a judgement made against the data rather than a known
edit.

**Verify.** The new late-CM11 perf scenario in `analysis/perf/scenarios.json`, with `physics_ms`
under 16.7 ms on the reference rig, and a flown CM11 whose rendered-frame count and sim-second count
track each other. Baseline first. Then the full gate.

**⚠ Traps.** A wall-clock measurement of anything in that session is not a sim measurement, so
compare durations in sim seconds (the log's 1 Hz `flight:` cadence, `GameClock.Frame`) and never in
wall seconds. Do not raise `max_physics_steps_per_frame`: it only deepens the catch-up spiral. C21
touches the same per-sim-step ray casts, so if C21 lands first, re-baseline before attributing
anything here, and if it has not, do not attribute this step's cost to allocation without the GC
capture that would show it.

## C23 ❌ `BL-584`: `PerfSampleTests.AScopeAllocatesNothing` is not same-build stable in the parallel unit stage

**Disproven — as stated.** Re-verified against the code first: `PerfSampleTests` is still the only
class touching `PerfSample`'s ambient statics (`Z:\CSVM\.claude\worktrees\m5p6-c23\CSVM.Tests`
carries no other `PerfSample.Scope`/`EndFrame`/`Reset` call, and the class's own doc comment already
states the invariant); the eight production call sites
(`Launcher.cs`, `AiFlightAssembler.cs`, `WorldSounds.cs`, `TextureArchive.cs`, `AnimRuntime.cs`,
`EmitterDirector.cs`, `FlightController.cs`, `EmitterRenderer.cs`) are all reached only from Godot
runtime code the `dotnet test` unit stage never loads, so no other unit test class can be charging
allocation to `PerfSample.Scope` in-process either. That already rules out the backlog's first
candidate mechanism by construction, not just by absence of a repro.

Both candidate mechanisms were then tested directly rather than assumed:

- **Cross-thread contamination** (another class's work landing on the same thread-pool thread).
  `GC.GetAllocatedBytesForCurrentThread` is documented as a per-thread reading; an in-process
  experiment held sixteen background tasks continuously allocating and forcing gen-0 collections
  for the full width of the measured window, on the same process, while the measured loop ran on
  the test thread. Eight of eight trials read exactly zero bytes. The full unit stage was also run
  repeatedly (2,558/2,558 each time) while sixteen external processes independently saturated every
  logical core, to maximize scheduler contention beyond the stage's own fourteen classes; six of six
  runs stayed green, with per-run wall time roughly doubling under that load, confirming the
  contention was real without ever perturbing the assertion.
- **A tiered-JIT recompile landing mid-scope.** The existing 10,000-iteration warm-up loop
  (`PerfSampleTests.AScopeAllocatesNothing`, added for `BL-379`) exists to pay off exactly this. An
  isolated experiment removed it entirely (0, 1, 5, 50, 500 warm-up iterations against the same
  10,000-iteration measured loop) so any tier-up or on-stack-replacement transition the warm-up
  would normally absorb had to land inside the measured window instead. Every warm-up size read
  zero bytes across repeated trials, including zero warm-up, the case most likely to force a
  mid-loop recompile.

Across every attempt to force the contended condition described in the backlog (in-process
cross-thread allocation and GC pressure, external whole-machine CPU saturation, and a JIT warm-up
starved to nothing), the assertion never read anything but zero. Neither mechanism is demonstrated;
both are now actively contradicted by the same instrument the item asks to make reproducible. No
code changes to `PerfSample.cs` or `PerfSampleTests.cs` land: the hard rule against a plausible fix
for an unexplained flake applies precisely because no explanation survived testing, and the
assertion's own contract (exactly zero, PERF-15) stays untouched.

The single historical `Actual: 3984` reading is not explained by this investigation and is not
reproduced by it either; it remains a seen-once anomaly on this test's own confidence ladder, now
with its two named causes tested out rather than assumed.

`BL-584` therefore stays in `backlog.md` rather than being deleted with this item, rewritten as a
`[Research]` entry that records the two ruled-out mechanisms and names what to capture on a
recurrence (the failing build's binary hash and the concurrent-class list from the TRX). The plan
item is ❌ because this run does not fix it, not because the symptom is settled: a disproof of a
mechanism is not a disproof of the event, and the entry exists so nobody repeats these forty
trials.

**Verify — what would prove a recurrence fixed, if one is ever seen again.** Treat runs as
independent Bernoulli trials at the originally observed rate of about one failure in ten. By the
rule of three, zero failures in `n` clean runs bounds the true failure rate at roughly `3/n` with
95% confidence; at the observed ~10% rate, `P(no failure in n runs) = 0.9^n`. Thirty consecutive
clean full-unit-stage runs give about 95% confidence the true rate is no longer near 10% (`0.9^30 ≈
4%`); forty-four give about 99% (`0.9^44 ≈ 1%`). Below that count, "it passed N times" is not
evidence of a fix at this base rate, which is why this item does not close on a handful of green
runs alone — a future recurrence should log the failing build's binary hash and the concurrent
class list from the TRX, since neither was captured the one time this fired, and either would turn
"lead-only" into a traced mechanism on the next occurrence.

**Original approach (kept for reference).**

**Goal.** The assertion holds on every run of the full unit stage, for the reason it is asserting
rather than by luck of scheduling.

**Evidence (confidence: lead-only, seen once).** The full `RunTests.ps1` unit stage reported it red
once (`Expected: 0, Actual: 3984` bytes) on a tree whose only difference from six green runs was
PowerShell and documentation edits; it then passed three times alone and on every later complete
run. An allocation assertion measured with `GC.GetAllocatedBytesForCurrentThread` or similar shares
a process with fourteen concurrent test classes, so another class's work on the same thread pool
thread, or a tiered-JIT recompile landing mid-scope, can charge bytes to it. The mechanism is a
lead: neither cause has been demonstrated. <TODO: re-verify still-open against the code.>

**Approach.** Pin the measurement to the current thread and warm the scope once before the asserted
call, or move the test to a non-parallel collection and say in the test why it is there. Read
`docs/verification.md`'s PERF rules and `docs/plans/PLAN-fast-verification.md` C23 first, since the
parallel unit stage is that plan's design and the reason the test shares a process at all.

**Model recommendation.** Medium. Small, well-bounded, and the two candidate fixes are both
ordinary; the judgement is only in not widening the contract.

**Verify.** <TODO: decide what proves a once-seen flake fixed. Repeated full unit-stage runs are the
obvious instrument, but the failure has been seen once in ten runs, so name a run count that would
mean something, or make the failure reproducible first by forcing the contended condition.>

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

**⚠ Traps.** Do not widen the assertion to a tolerance: zero allocations is the contract
`PerfSample` makes, and a tolerance would hide a real regression, which is exactly the regression
C21 and C22 need this test to catch. A test that passes because it now measures less is not fixed.

---

# Wave D — Damage share and target class

## D31 ☑ `BL-586`: one burst deals a world destructible one splash share per collider body it carries

**Landed.** A burst deals one share per world OBJECT, where the object is the node a collider body
belongs to, not the resolved destructible and not the body. `WorldCollision.OwnerOf` names it:
`SceneBuilder.AttachCollision` carves one mesh node's collision into one `StaticBody3D` per surface
class and stamps each with `SurfaceIdMeta`, so those siblings answer their shared parent while every
other body (a clutter region, a plane hull, a suite's bare plate) answers itself.
`ProjectilePool.ApplyDamage` walks its nearest-first candidate list keeping the first body of each
group, seeded with the struck node's group since it already took the full unscaled figure. The
32-target cap now counts objects, as the original's buffer does.

**The decode overturned the plan's fix shape, and no `DamageSink` change was made.** `FUN_004cb420`
walks the spatial grid and hands each object to `FUN_004cb950`, which recurses: a node with
`node+0x24` bit `0x40` clear is a group contributing nothing itself and passing the walk to its
children, while a node carrying `0x40` and `0x100` is a leaf writing exactly one entry keyed on its
own pointer at entry `+0x28`. Neither the gather nor `FUN_005acac0` dedupes further. So the buffer
holds one entry per collidable LEAF, not one per top-level object, and a model of several leaves
takes several shares against one HP pool. Collapsing per resolved destructible would therefore have
been a second fidelity bug in the opposite direction, and it would have merged C3/M01's four
`hydrogentank` bodies, which are genuinely separate parts. The pool needs no destructible key
because the grouping is Godot scene structure it already holds.

Nearest-first is derived, not chosen: the sibling bodies being collapsed stood for ONE original leaf,
whose single entry records the distance to that whole node's bounding-sphere surface, so the nearest
of the group is the share the original would have recorded.

**Measured.** In `turret-self-fire`, a neighbour's flak dropped into `aagun36`'s pit dealt
`-10@g20/col_buildings -9.98@g20/col -9.88@g6/col_buildings -9.78@g6/col`: four shares from two
nodes, each doubled by the surface-class carve. After the change, `-10@g20/col_buildings
-9.88@g6/col_buildings`, one share per node with the nearest magnitude of each pair unchanged. The
suite now asserts the share count equals the distinct struck-parent count, an invariant that holds
whatever the geometry, rather than a pinned literal. The item's original `-8.18/-8.04/-8/-7.36`
trace is no longer reachable from the gun's own burst, which `BL-573`'s owner-body gate now refuses.

**Suites checked** (the plan's open TODO): `turret-self-fire` is the one whose numbers move.
`blast-curve-cover-cap` was the real risk, since its forty lab plates and its `exactly 32 damaged`
and `three targets and nothing else` assertions count bodies that resolve to no destructible at all;
they survive because a body `SceneBuilder` did not build keys on itself. `alpha-cutout-ray-census`
reads `BlastCoverCensus`, which is deliberately left per-body. `blast-neighbor-shape`,
`zeppelin-damage` and `zeppelin-cannon-burnout` (both pick a blast-less weapon on purpose),
`world-turrets`, `mission-off-turrets` and `damage-hd` (all drive `DamageAt` directly) do not move.
`air-to-air` is the canary that the dedupe did not leak into the aircraft branch, which is keyed
per plane already.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

**Original approach (kept for reference).**

**Goal.** One burst deals a world destructible one splash share, whatever number of collider bodies
the destructible carries, matching the original's hit buffer of one entry per node.

**Evidence (confidence: traced).** The C1 aagun carries ten collider bodies, and one flak bursting
over its own pit dealt `-8.18`, `-8.04`, `-8` and `-7.36`, one share per body, in the
`turret-self-fire` trace. The original's hit buffer holds one entry per node (`FUN_004cb420`).
Filed at the close of `BL-573` (`git log --grep=BL-573`).

**Approach.** Dedupe `ProjectilePool.ApplyDamage`'s world candidates per resolved destructible,
keeping the nearest body's share. This needs a `DamageSink`-side key, because the pool cannot
resolve destructibles itself, so the interface change is the substance of the item and the dedupe
is the easy half. Read `docs/org/ordnanceTypes.md` and the `turret-self-fire` suite first.

**Model recommendation.** High. It puts a new key on the `DamageSink` interface, which every damage
consumer sees, so the blast radius is wider than the fix.

**Verify.** The `turret-self-fire` trace showing one share on the C1 aagun where it showed four,
with the share's magnitude unchanged. Then the full gate, reading the destructible and weapon
suites for any consumer whose expected damage moved.

**⚠ Traps.** Keeping the nearest body's share is a decision, not a derivation: if the original's
buffer keeps the first entry rather than the nearest, the totals differ on an off-centre burst, so
check `FUN_004cb420` before choosing. A destructible whose bodies are genuinely separate damageable
parts must not be collapsed by the same key; establish that the resolved destructible is the right
granularity rather than assuming it.

## D32 ☑ `BL-598`: CM08's patrol boats take hits but cannot be targeted or aim-assisted

**Landed.** Re-verified open against the code: `AimCandidateSet` fed only `Vehicles` (aircraft, via
`ProjectilePool.CollectAircraft`), `Turrets` (carried gunners plus world emplacements) and
`Structures` (`DestructibleRegistry`); no collector walked `SurfaceVehicleRuntime.Vessels`, so a
patrol boat's hull was in none of the three.

The decode: `docs/org/aim-assist.md` "The four lists" already names `DAT_0071dabc` /
`FUN_004729b0` (net registration string `s_VehicleList_00627414`) as **`VehicleList` — "aircraft
and AI ground/sea vehicles"**, walked by `FUN_004897c0` as the per-plane sim pass; `TargetRef`'s
own class model (`docs/org/targeting.md` "The class model", `FUN_004b5cd0`) independently confirms
it: with neither the `otherTarget` nor `objectiveTarget` mission flag set, only a `TargetVehicle`
or `TargetProjectile` can be classified Enemy/Ally at all (`TargetRef.Classify`'s
`kind is not (Vehicle or Ordnance)` gate) — a `Structure` or `Turret` candidate with neither flag is
not selectable, which is why routing a boat through `AddStructures`/`subParts` (the zeppelin
sub-part path) would have landed it on no cycle rather than the Enemy one. `VehicleList` is not "the
aircraft list stood in for convenience": it is the original's own list for a ground/sea AI vehicle,
and the aircraft roster happens to be the other thing on it.

**What changed.** `SurfaceVehicleRuntime.CollectVehicles(AimCandidateSet)` appends every built hull
as a vehicle candidate (team off the block, `Live` = woken and not destroyed, no velocity — nothing
here reads the follower's speed yet, a candidate for a follow-up). `FlightController` carries a
`SurfaceVehicles` field beside `Destructibles`, wired by `GameSession` through `FlightWorldBindings`
(`AiFlightAssembler`/`HumanFlightAdapter`), and `StepTargeting`/`ApplyFireOutcome` call it beside
`CollectAircraft`, so a hull reaches both the HUD's candidate pool and the player's own gun aim
assist. `SelectRankedTarget` (the AI gunner's own acquisition, `BL-523`'s territory) is untouched:
it already filters to `c.Source is FlightController`, so it would have ignored a boat regardless.

**Verified.** The complete `.\RunTests.ps1` on the merged tree carrying every landed item of this
run: build clean, 2558 of 2558 units passed, 175 of 175 engine suites passed with engine errors
clean across 4 shards, and all 16 golden shots hash-identical, in 144.7 s against a 180 s budget.
The one failure the merged tree produced was `destructible-census`, which no per-item agent could
have seen, and it was B12's intended effect meeting a pinned count rather than a regression: see
the census re-pin's own commit.

Suites (foreground, `$env:CSVM_DATA_ROOT="Z:\CSVM"`):
`.\RunTests.ps1 -Suite "campaign-surface-vehicles,target-pool,target-selection,aim-assist,targeting-candidates" -SkipUnits -SkipGoldens`
— all pass. `campaign-surface-vehicles` is extended with a direct, no-flight assertion (preferred
per the plan's TODO: candidate-list membership needs no flight): after `Wake()`, a woken hull is
confirmed `Live` on `AimCandidateSet.Vehicles`, lands on `TargetPool.Enemy` (the HUD's cycle) under
the player's team, and is the winning candidate of `AimAssist.Scan` from a muzzle behind it (the
gun aim assist). `.\RunTests.ps1 -Quick` is also green (226 units, 13 engine suites).
`dotnet build` is 0 warnings; `CheckCommentCaps.ps1 -Summary` is clean.

**Not verified.** A live CM08 flight past 181 s with the HUD actually drawing a bracket and the
tracer visibly bending onto a boat — the suite proves candidate-list membership and the scan's
winner, not the on-screen draw. Flying it is straightforward
(`--chapter=C1B --mission=M03 --pos=<near a woken boat> --target=nearest` after 181 s of sim time)
but was not run here, since the suite settles the mechanism the goal names.

### Original approach (kept for reference)

**Goal.** A surface vehicle is a target like the other target classes: the HUD brackets it and the
aim assist snaps to it.

**Evidence (confidence: traced).** Reported at the controls: the four boats that C22 of run 5's
surface launch wakes at 181 s in CM08 (C1B/M03) drive their nets and rounds hit them, but the
targeting HUD never brackets one and the aim assist never snaps to one.
`SurfaceVehicleRuntime`'s hull is neither an aircraft rig nor a world turret, so `TargetSelection`
and `AimAssist`'s candidate lists (vehicles, turrets, structures) do not see it.
<TODO: re-verify still-open against the code.>

**Approach.** Decide which list the original puts a surface vehicle in, reading its targets record
and the HUD class it draws, then register the hull there. Read `SurfaceVehicle`, `TargetHud` and
`AimAssist.AddStructures`. Decode lane: <TODO: name the `crimson.exe` routine that builds the
target candidate list and the class field it keys on.> `BL-523` covers the boats' gunnery and is a
separate item; take nothing from it here.

**Model recommendation.** Medium. The registration is ordinary once the class question is answered,
and the class question is a data read rather than a design call.

**Verify.** A CM08 flight past 181 s with a boat bracketed by the HUD and the aim assist snapping to
it. <TODO: name the launch command for CM08 and whether the check can be made without flying the
mission from its start.> Then the full gate and the targeting suites.

**⚠ Traps.** This item depends on `SurfaceVehicleRuntime`, which lands with run 5's C22, so it
cannot start from a branch taken from today's main. Registering the hull in the aircraft list
because it is the easiest list to reach would give it aircraft target behaviour (lead, class icon,
threat handling) that the original may not give a boat; the class the original uses decides, not
convenience.
