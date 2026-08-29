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
2. ☐ `BL-322`: C5's lit facades render at 0.58 to 0.66 of the original with WorldLight already at clamp
3. ☐ `BL-538`: a dark band crosses the large buildings at the range the templates clutter fades out

### Wave B — Effects and the animation scope

11. ☐ `BL-546`: a nitro engage produces no prop swap and no exhaust smoke
12. ☐ `BL-587`: chapter-scope animation files no mission lists still run
13. ☐ `BL-535`: a repeat sonic burst pays a 10 to 14 ms slot re-reset from the third burst on

### Wave C — The allocation and tick budget

21. ☐ `BL-536`: every collection in flight is a gen1 collection pausing 24 to 29 ms about every 12 s
22. ☐ `BL-562`: the physics tick costs about 39 ms late in CM11, so the sim runs at half wall time
23. ☐ `BL-584`: `PerfSampleTests.AScopeAllocatesNothing` is not same-build stable in the parallel unit stage

### Wave D — Damage share and target class

31. ☐ `BL-586`: one burst deals a world destructible one splash share per collider body it carries
32. ☐ `BL-598`: CM08's patrol boats take hits but cannot be targeted or aim-assisted

## Dependency and parallelism notes

The four waves own disjoint files and may each run in their own worktree in parallel, which is why
the plan is cut this way. Inside a wave, the notes below apply.

Wave A's three items all end in a shader or material decision about the world pass, and A1 and A2
share the `csky_world_light` question directly (A1 proposes exempting a material from it, A2 asks
whether these facades should be modulated at all), so run them in sequence in one worktree rather
than in parallel; A1 first, because its measurement is the tighter of the two. A3 is separable
(the clutter fade cutout and the gamez buildings' own fade range) and may run alongside, but not in
a parallel worktree if it touches `csky_clutter_fade` and A1/A2 touch the shared world shader
include. <TODO: confirm whether the world-light and clutter-fade shaders share an include file.>

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

**Verified.** <pending orchestrator run>

## A2 ☐ `BL-322`: C5's lit facades render at 0.58 to 0.66 of the original with WorldLight already at clamp

**Goal.** C5's night facades reach the original's measured brightness: tower faces about 15.5 where
they now read 10.2, low-rise about 37.6 where it now reads 21.7.

**Evidence (confidence: direction-sound, mechanism lead-only).** Split out of `BL-303` at its close
on 2026-08-08 and measured under `CAP-11`: tower faces 10.2 against 15.5, low-rise 21.7 against
37.6, with `WorldLight` already at its clamp of 1.0, so the deficit is not the world-light scalar
running short. It is explicitly not fog: that is `BL-303`'s own adjunct note, and the Wave B fog
work of that plan moved none of it. The candidate direction is the lit-signage and self-lit family,
where `lighting: false` models draw fullbright (`docs/formats/weather.md`), so the question is
whether these facades author a flag or vertex data that we modulate and the original does not.
<TODO: re-verify still-open against the code.>

**Approach.** Start from the facades' own authored material and vertex data in C5's gamez rather
than from the shader: establish what distinguishes the measured faces from the ones that read
correctly, and whether that discriminator is a `lighting` flag, a vertex colour, or a material the
self-lit path should already have caught. Only then decide what stops modulating them. Decode lane:
<TODO: name the `crimson.exe` routine that decides whether a scene node's material is modulated by
SUNLIGHT, and what it keys on.>

**Model recommendation.** High. The mechanism is unknown, the measurement is the only firm ground,
and the change risks becoming a second global brightness knob if the discriminator is guessed
rather than found.

**Verify.** The C5 night poses listed in `playtest/CAP-11/README.md`, measuring the same tower
faces and low-rise the split recorded, with the pre-change numbers reproduced first. Then the full
8-chapter `--freecam` regression and the C5 goldens.
<TODO: name the C5 goldens in `analysis/goldens/manifest.json`.>

**⚠ Traps.** Fog is ruled out and must not be re-chased. `WorldLight` is at clamp here, so raising
it is not available and would break the terrain calibration `CAP-11` pinned. A fix that brightens
every C5 surface rather than the measured family is a regression the goldens should catch, so read
them rather than only the two measured faces. This item interacts with A1: both ask whether a
family should be exempt from the same modulation, and a shared exemption that catches both by
accident is not a result, it is a coincidence to rule out.

## A3 ☐ `BL-538`: a dark band crosses the large buildings at the range the templates clutter fades out

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
scene node rather than only to clutter. <TODO: re-verify still-open against the code.>

**Approach.** Separate the candidates before touching anything: a C5 screenshot pair at the band
distance with `graphics.clutterFarFade` on and off tells you whether the band belongs to the
clutter pass at all. If it does not, `FUN_004d5de0`'s application to every type-5 node is the decode
lane and the answer is that the gamez buildings want their own fade. Read `docs/org/clutter.md`
first; `BL-337` (closed) is where the fade itself was settled and its dead ends are recorded.

**Model recommendation.** High. Three live candidates, a rendering pass that other items in this
wave also touch, and a real chance the answer is a decode rather than a fix.

**Verify.** The C5 pose at the band distance.
<TODO: name the exact `--freecam --chapter=C5 --pos=… --direction=…` the controls report came from;
the backlog entry does not carry one.> Then the full 8-chapter `--freecam` regression and the C5
goldens, since any change to the clutter pass is global.

**⚠ Traps.** The dither is not the fault and must not be changed to chase the band. This item and
A1/A2 all end in the world pass, so a fix here that moves the measured facade or water numbers has
broken one of those items rather than fixed this one; take A1's and A2's numbers as a control if
they have landed. A screenshot pair proves which pass owns the band and nothing more, so do not
read the on/off difference as the mechanism.

---

# Wave B — Effects and the animation scope

## B11 ☐ `BL-546`: a nitro engage produces no prop swap and no exhaust smoke

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
<TODO: re-verify still-open against the code.>

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

## B12 ☐ `BL-587`: chapter-scope animation files no mission lists still run

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

## B13 ☐ `BL-535`: a repeat sonic burst pays a 10 to 14 ms slot re-reset from the third burst on

**Goal.** The third and every later sonic burst costs what the first two cost: about 0.6 ms of
`effect_checkout`, rather than 10.2 to 13.9 ms.

**Evidence (confidence: traced).** With every emitter pre-built at bind
(`AnimRuntime.PrewarmEmitters`), the weapon-lab probe (`--chapter=C1 --weapon-lab=wep_08
--weapon-fire --infinite-ammo --weapon-surface=default --weapon-standoff=90`) still records one
`effect_checkout` sample per burst once `hitchMonitor.floorMs` is 10 and `medianMultiple` 1.2 under
`--no-det --no-vsync`: 0.6 ms for the first two bursts, then 10.2 to 13.9 ms for every burst from
the third on, one call each. The step lands two bursts before a four-slot pool could recycle, so it
is neither a wrap nor construction. It is the cost of re-resetting a slot copy that has run before,
in `AnimRuntime.ResetCheckedOutCopies`, the re-reset that fixed the rings vanishing from the fifth
burst on. Under the stock monitor it never trips, so it is a per-burst cost rather than a hitch, and
at vsync it is inside a frame. <TODO: re-verify still-open against the code.>

**Approach.** Establish what the re-reset walks per copy: every template node of the subtree, or
only the ones the last run posed. If it is the former, the fix shape is recording the END pose at
stop time so the reset replays a short list. `BL-231` (closed) is the pool-size judgement this was
measured under, and the `effect-pool-reset` suite is the pose contract the re-reset keeps, so read
both before narrowing what gets walked.

**Model recommendation.** Medium. The measurement is exact and the suspect routine is named; the
care needed is in not breaking the pose contract the suite pins.

**Verify.** The same weapon-lab probe with `hitchMonitor.floorMs` at 10 and `medianMultiple` at 1.2
under `--no-det --no-vsync`, showing bursts three onward at the first two bursts' cost. Take the
baseline first, since the point of the measurement is a number that must be seen able to fail. Then
the `effect-pool-reset` suite and the full gate.

**⚠ Traps.** `docs/verification.md` PERF-14: the stock probe cannot fail on this, so a green stock
run is not evidence. The lowered monitor is the only instrument that sees it and it needs both
knobs, because the trigger is the larger of the floor and the median times the multiple. The
re-reset exists to stop the rings vanishing from the fifth burst on, so a fix that makes the cost
go away by resetting less must be checked against that symptom, not only against the timing.

---

# Wave C — The allocation and tick budget

## C21 ☐ `BL-536`: every collection in flight is a gen1 collection pausing 24 to 29 ms about every 12 s

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

## C22 ☐ `BL-562`: the physics tick costs about 39 ms late in CM11, so the sim runs at half wall time

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

## C23 ☐ `BL-584`: `PerfSampleTests.AScopeAllocatesNothing` is not same-build stable in the parallel unit stage

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

**⚠ Traps.** Do not widen the assertion to a tolerance: zero allocations is the contract
`PerfSample` makes, and a tolerance would hide a real regression, which is exactly the regression
C21 and C22 need this test to catch. A test that passes because it now measures less is not fixed.

---

# Wave D — Damage share and target class

## D31 ☐ `BL-586`: one burst deals a world destructible one splash share per collider body it carries

**Goal.** One burst deals a world destructible one splash share, whatever number of collider bodies
the destructible carries, matching the original's hit buffer of one entry per node.

**Evidence (confidence: traced).** The C1 aagun carries ten collider bodies, and one flak bursting
over its own pit dealt `-8.18`, `-8.04`, `-8` and `-7.36`, one share per body, in the
`turret-self-fire` trace. The original's hit buffer holds one entry per node (`FUN_004cb420`).
Filed at the close of `BL-573` (`git log --grep=BL-573`).
<TODO: re-verify still-open against the code.>

**Approach.** Dedupe `ProjectilePool.ApplyDamage`'s world candidates per resolved destructible,
keeping the nearest body's share. This needs a `DamageSink`-side key, because the pool cannot
resolve destructibles itself, so the interface change is the substance of the item and the dedupe
is the easy half. Read `docs/org/ordnanceTypes.md` and the `turret-self-fire` suite first.

**Model recommendation.** High. It puts a new key on the `DamageSink` interface, which every damage
consumer sees, so the blast radius is wider than the fix.

**Verify.** The `turret-self-fire` trace showing one share on the C1 aagun where it showed four,
with the share's magnitude unchanged. Then the full gate, reading the destructible and weapon
suites for any consumer whose expected damage moved. <TODO: name the suites that assert splash
damage totals against multi-body destructibles.>

**⚠ Traps.** Keeping the nearest body's share is a decision, not a derivation: if the original's
buffer keeps the first entry rather than the nearest, the totals differ on an off-centre burst, so
check `FUN_004cb420` before choosing. A destructible whose bodies are genuinely separate damageable
parts must not be collapsed by the same key; establish that the resolved destructible is the right
granularity rather than assuming it.

## D32 ☐ `BL-598`: CM08's patrol boats take hits but cannot be targeted or aim-assisted

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
