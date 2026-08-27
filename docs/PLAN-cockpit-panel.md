# Cockpit panel: the four open instrument defects

**ACTIVE PLAN** (written 2026-08-27). It sits in `docs/`, which by this repo's convention makes it a
live plan. Move it to `docs/plans/` with a `COMPLETE` banner, and add its row to
[`plans.md`](plans.md), when every item lands.

This plan finishes the authored 3D instrument panel inside the pilot's own `cockpit1`. The needle
drive, the two warning lamps, the nitro dial gate and the character readouts landed under `BL-431`
on the `bl-431-cockpit-gauge-drive` branch. Four defects reported at the controls remain: the weapon
gauges' belt lights never change colour, the damage display never changes colour, the artificial
horizon is inert, and the whole panel vibrates against a cockpit that is still. Each was re-verified
open against the code in this worktree, not taken from the backlog entry on trust.

Out of scope, deliberately. The `comp` compass drum is unwired for the same reason the horizon is
and would be a natural fifth item, but the user's list does not name it and it needs its own decode.
`BL-431`'s remaining judgement call, whether the screen-space `GaugeCluster` retires in first person
now that the 3D panel reads live, stays open and stays that item's; it is a taste call at the
controls, not work this plan can settle. The damage-dial post-hit blink timing (5 s, 0.32 s) is an
undecoded TUNE and stays one.

## Milestone goal

- Every belt position on both weapon gauges shows its loadout's colour, red for an empty or unfitted
  slot, on the authored 3D dial as well as the flat one.
- Every damage zone on the 3D dial shows its part's colour tier and blinks after a hit.
- The artificial horizon tracks the aircraft's attitude under a law decoded from `crimson.exe`.
- The panel is as steady against the airframe as the airframe is against the cockpit shell.

**No new instrument gets a guessed law.** Every quantity this plan writes comes from `crimson.exe`
or from the shipped model data. This project has been wrong repeatedly by measuring off footage, and
the horizon is exactly the shape of item that invites it.

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | The character cells collapse into one surface on import, so the readouts cannot be addressed per cell. | `4char_ammo` carries four distinct material indices (355-358); the builder's group-by-material keeps them apart. |
| 2 | An unwritten readout cell renders blank. | The blank is a real glyph at index 36 and the archives ship `space.png`; unwritten cells kept their authored characters, which is why the readouts first read `BOOMAA` and `1113`. |
| 3 | The belt low tier is 0.15 and hardpoints never show yellow. | The threshold is a quarter full at `0x006034f4`, and one state function (`FUN_004547a0`) serves both gauges. A single-round pylon simply cannot reach the low tier. |
| 4 | The arrow sweeps at 168.7 °/s with an ease. | `FUN_004544b0` steps at a constant 288 °/s, no easing. |
| 5 | `--hold` pins the aircraft, so a capture under it is a static scene. | It holds control inputs. The aircraft glides, so every "static" vibration measurement taken under it was of a moving plane. |
| 6 | The panel vibration is a draw-order fight between the bezel rings and the `dash` panel. | The interior's own `DepthBiasScale = 1 / InteriorScale` fixed the draw-order fight and the lateral motion survived it. |
| 7 | The vibration is the depth bias moving the instruments. | `VERTEX *= 1.0 - (depth_bias + node_bias)` scales toward the eye, which preserves projected position exactly. It cannot be a lateral source. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | C21 | The measurement is real; whether the fix is worth its cost is the call. |
| **Leads only — no mechanism yet** | B11, C20 | Budget for investigation. B11 may end in a disproof that the original drives it at all. |

**⚠ Worktree hazard.** This plan runs on `bl-431-cockpit-gauge-drive` in
`.claude/worktrees/bl-431`. `git stash` is repo-global and shared across worktrees; never use it
here. Use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the binary or the compiled JSON before writing a
  value; never guess one. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data or code before building on it; a correct disproof that lands no code is a success here.
- **`PROJECT_CONTEXT.md` and `docs/formats/hud.md` are updated in the same turn** as each landed
  item; the landing commit's message carries what landed and how it was verified, and the item is
  deleted from `backlog.md` rather than marked fixed there.
- **The Ghidra project is read-only.** No renames, comments, structs, prototypes, analysis runs or
  saves. Knowledge accumulates in the repo, never in the database.
- **Read `docs/verification.md` before measuring anything.** The instruments here mislead.
- **Every item ends on the full battery**: `RunTests.ps1` (build, units, in-engine suites, goldens,
  hitch) plus a targeted Cockpit capture at the condition the report came from.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven.

### Wave A — the two drives that are built but never reached

1. ☐ Pass the interior's materials to `CockpitGauges.Bind` in the live flight path
2. ☐ Make the belt and damage drives fail loudly when nothing binds

### Wave B — the artificial horizon

11. ☐ Decode the horizon's drive law out of `crimson.exe`
12. ☐ Drive `horizn` from the decoded law

### Wave C — the panel vibration

20. ☐ Confirm or kill the float-precision hypothesis with a near-origin capture
21. ☐ Prototype the interior in its own pass with the camera at the origin

## Dependency and parallelism notes

A1 blocks A2 and blocks any judgement of the belt and damage colours at the controls, so it runs
first. Wave B is independent of both other waves and can run in parallel with them; it touches
`CockpitGauges.cs` and `GaugeCluster.cs`, which A1 does not (A1's edit is in
`Session/HumanFlightAdapter.cs`), but A2 does add to `CockpitGauges.cs`, so B12 and A2 must not run
in parallel worktrees. C20 must complete before C21 is started at all: C21 is a large change and
C20 is the cheap test that says whether it is the right one. C21 contends with `PlaneBuilder.cs`,
`PlayerRig.cs` and `SplitScreen.cs`, none of which any other item here touches.

---

# Wave A — the two drives that are built but never reached

## A1 ☐ Pass the interior's materials to `CockpitGauges.Bind` in the live flight path

**Goal.** The belt lights and the damage zones change colour in a flown Cockpit view, the same way
they already do in the in-engine suite.

**Evidence (confidence: traced).** The drives are written and correct. `CockpitGauges.Bind` takes an
optional `materials` argument, defaulting to null, and builds from it the instance-id to
texture-name map that `Skin.For` needs to tell an indicator's light from its hilite bar.
`Session/HumanFlightAdapter.cs:140` calls `CockpitGauges.Bind(planeBuilder.CockpitInterior)` with no
second argument. The map is therefore empty in every flown session, `Skin.For` finds no named
surface on any node and returns null, and `Belt.FindAll` and `DamageZoneSkin.FindAll` both return
empty lists. `Apply` then loops over nothing.

This also explains why the readouts work and these two do not: `Readout.Build` does not consult the
name map at all, it duplicates whatever material each surface carries. And it explains why the
in-engine suite passes while the controls do not: `Testing/WorldAndToolSuites.cs:229` passes
`builder.InteriorMaterials` explicitly, so the suite exercises a binding the game never makes.

**Approach.** Pass `planeBuilder.InteriorMaterials` as the second argument at
`HumanFlightAdapter.cs:140`. Then check for any other production caller of `Bind` and give it the
same argument. Do not change `Bind`'s signature to make the argument required in this item; A2
covers the reason that would not have caught this anyway.

**Model recommendation.** Medium, low effort. The fix is one argument and the trace is already done;
what remains is mechanical.

**Verify.** A Cockpit capture with a partly spent gun belt and at least one damaged zone: the spent
belt position reads red or yellow while a full one reads green, and the damaged zone's border and
hatch leave green. Take the baseline capture first, since an all-green panel is what the bug also
produces and an unchanged image would otherwise look like a pass. Full battery after.

**⚠ Traps.** The suite's green is not evidence. `DrivenBelts` passed throughout the period the
feature was broken at the controls, because it constructs the binding the bug is in. Any check added
for this must go through the same call the game makes.

## A2 ☐ Make the belt and damage drives fail loudly when nothing binds

**Goal.** A future caller that binds the panel without its materials is caught by the suite rather
than by a report at the controls.

**Evidence (confidence: traced).** A1's defect survived a green battery because the only in-engine
coverage constructed its own binding. The optional parameter is what allowed a caller to be silently
wrong: an omitted argument produced an empty map, an empty map produced empty lists, and empty lists
are indistinguishable from a plane whose panel genuinely has no belts.

**Approach.** Two changes, either of which would have caught it and both of which are cheap. Make
the in-engine cockpit suite bind the panel through the same path `HumanFlightAdapter` uses, so the
production call is what gets exercised. And have `Bind` report the count it found, so a bind that
locates the `gauges` subtree, the needles and the readouts but zero belts and zero damage zones is a
visible anomaly rather than silence. A `Log.Debug` line under the existing flight channel is enough;
an airframe that genuinely ships no indicators would then say so once at bind rather than never.

**Model recommendation.** Medium. Choosing what the suite should assert without making it brittle
across airframes is a judgement call.

**Verify.** Revert A1's argument locally, confirm the suite now fails, restore it, confirm it passes.
An unchanged number is not evidence unless you have seen it able to fail. Full battery after.

**⚠ Traps.** Do not assert a fixed belt count. The ring is 8 positions on every airframe but the
damage zones are per-model, and a hard count would break on the next airframe read.

# Wave B — the artificial horizon

## B11 ☐ Decode the horizon's drive law out of `crimson.exe`

**Goal.** A written rule, with addresses, for what `horizn` is posed by: which attitude angles feed
it, in what order, about which axes, and whether the original clamps or wraps at extremes.

**Evidence (confidence: lead-only).** `docs/formats/hud.md:207` records `horizn` as present in the
`gauges` subtree and unwired in the remake, with no decode behind it. Nothing in CSVM poses it
today. The node is named in `hud.md:66` among the dial meshes, and `backlog.md:2028` notes its bezel
ring sits at authored priority 1 over the `dash` panel at 0, which is a draw-order fact and says
nothing about its motion.

Two questions have to be answered before any code is written, and neither can be answered from the
model. Whether the horizon is posed as a node rotation the way the needles are, or by a texture
cycle the way the belt lights and readouts are. And whether it carries both pitch and roll or roll
alone. The needle laws all came from the same region of `crimson.exe` as the gauge state functions
(`FUN_004544b0` and `FUN_004547a0` are the worked examples), so the callers of the same panel update
are the place to start.

**Approach.** Hand this to a fresh-context subagent, since the entry names no address for it. It
prospects and reports; it writes nothing to the database and nothing to the repo. Require every
constant back with the address it came from and the condition it applies under. Report the answer as
numbers, then a formula, then a rule in prose with its constants when the value is conditional.
Decompiler output only if a genuine multi-step algorithm is the answer.

Also settle in the same pass how many airframes ship the node. The name appears on about half of
them, which either means the other airframes' panels use a different name for the same instrument or
that they genuinely ship none, and the drive has to be a no-op in the second case rather than a
crash.

**Model recommendation.** High. This is open-ended reverse engineering with no address to start
from, and the failure mode is a plausible wrong law that then gets built on.

**Verify.** The decode is verified by writing it down, not by running it: every claim names the
function it came from and can be re-checked at the address. The behavioural check belongs to B12.

**⚠ Traps.** If the decode does not find a driver, say so and stop. An inert `horizn` in the original
is a real possible answer, and `hud.md` should then record it as decoded-and-inert rather than
staying an open lead that gets re-chased. Do not fall back on measuring the instrument off footage
if the MCP is unreachable; an unrun decode is an open question, not a licence to guess.

## B12 ☐ Drive `horizn` from the decoded law

**Goal.** The horizon reads the aircraft's attitude in a flown Cockpit view.

**Evidence (confidence: lead-only until B11 lands).** Whatever B11 returns.

**Approach.** If the law is a node pose, it is a sixth `Needle` in `CockpitGauges` and the angle
comes from `GaugeCluster`, computed there so the 3D panel and any future flat draw cannot disagree.
That is the shape every other instrument here takes and there is no reason to break it. If the law
turns out to be a texture cycle, it is a `Skin` and follows the belt lights instead.

The attitude source is `FlightModel`'s own, not the camera's. The camera carries
`CameraController.HeadPitchOffsetRad` and any look-around the pilot has applied, and an instrument
that tracked the camera would read the pilot's head rather than the aircraft.

**Model recommendation.** Medium. The pattern to follow is established; the judgement was spent in
B11.

**Verify.** A Cockpit capture in a banked turn and one in a climb: the horizon matches the view out
of the canopy. An inverted or mirrored horizon is the likely failure and reads as obviously wrong at
the controls, so this is a check the eye makes better than any hash. Then the full battery, and the
goldens must be hash-identical since no golden flies the pilot's own cockpit.

**⚠ Traps.** Note the sign convention that bit the needles: the exposed dial angles are
clockwise-positive while a node rotation about +Z is counter-clockwise-positive, which is why
`Apply` negates the altimeter, speedometer and belt arrows and does not negate the nitro pair. Decide
the horizon's convention from the decode rather than by flipping signs until it looks right.

# Wave C — the panel vibration

## C20 ☐ Confirm or kill the float-precision hypothesis with a near-origin capture

**Goal.** A yes or no on whether the panel's motion is float32 rounding in the transform chain,
established cheaply before anyone builds the expensive fix.

**Evidence (confidence: measured symptom, hypothesised cause).** The instruments' projected centroids
drift sub-pixel between consecutive frames and by different amounts from each other: between frames
240 and 241 the gun gauge moved -0.224 px, the rockets -0.355 px and the altimeter -0.243 px, and
between 241 and 242 the same three moved -0.327, -0.054 and -0.209. The differing per-instrument
magnitude is the diagnostic fact, and it matches the report at the controls that the instruments
move relative to each other rather than as a block.

Already ruled out and not to be re-chased: the gauge drive itself, which is identical on `main`
where the symptom also appears; the camera shake pivot; mipmaps and anisotropy; temporal
antialiasing; the autohead idle aim; and the depth bias, which scales toward the eye and so
preserves projected position exactly. The draw-order fight the backlog entry describes was real and
was fixed by the interior's own `DepthBiasScale`; the lateral motion survived that fix.

The hypothesis is that each dial's world transform is composed in float32 under an interior mounted
at `PlaneBuilder.InteriorScale` (0.04) on an aircraft at chapter-scale world coordinates. At about
10 km a float carries roughly 1 mm of resolution, which at the panel's 0.42 m from the eye is about
0.14 degrees, several pixels. Each dial has its own local offset and so rounds differently, which is
what would make them move relative to one another rather than together.

**Approach.** The cheap discriminator is position, not pinning. Capture the same Cockpit scene with
the aircraft near the world origin and again at chapter-scale coordinates, everything else equal,
and measure the same per-instrument centroids across consecutive frames in both. If the residual
collapses near the origin, the hypothesis is confirmed and C21 is the right fix. If it survives
there, the cause is in the shading path and C21 would be wasted work.

Take the second measurement with the aircraft's world transform genuinely fixed between the two
frames. `--hold` does not do that: it holds control inputs and the aircraft glides on, which
invalidated every earlier attempt at a static measurement.

**Model recommendation.** Medium. The measurement technique is already built and the reasoning is
done; what remains is running it carefully.

**Verify.** The measurement is the deliverable. Record both centroid tables in the item's commit
message so the next reader can see the magnitudes rather than the conclusion alone.

**⚠ Traps.** A capture is not automatically comparable to another capture. Match the airframe, the
view, the attitude and the frame indices, or the difference measured is the scene's rather than the
coordinates'. And a near-origin scene may sit over different terrain with different lighting, which
changes the centroid measurement's noise floor even when the geometry is steady; measure the noise
floor in each scene before comparing the two.

## C21 ☐ Prototype the interior in its own pass with the camera at the origin

**Goal.** The panel is drawn in a coordinate frame small enough that float32 rounding is below a
pixel, so the instruments hold still relative to each other and to the cockpit shell.

**Evidence (confidence: direction sound if C20 confirms, magnitude a judgement call).** This is the
original's own architecture: the cockpit interior is a separate model in its own space, not a
subtree of the airframe at world coordinates. It is also what `PLAN-cockpit-view.md` B11 noted when
it parked the interior states. If C20 confirms precision, this fixes the whole panel at once rather
than one instrument at a time, and no per-instrument workaround can do the same.

**Approach.** A second `SubViewport` with its own `World3D`, holding a camera at the origin and the
interior at the origin, composited over the main view underneath the HUD. The interior's transform
in that world carries only the head pitch offset and any look-around, all of it small, so no large
coordinate ever enters the chain.

Three things need deciding as the prototype is built rather than after. The lighting and environment
in the second world, since the interior currently takes the main world's and will look wrong under a
default one. The composite order against `ScreenFlash` and the HUD, which draw into `HudParent`. And
splitscreen, where `SplitScreen` already gives every player a `SubViewport` on the shared world and
this would add a second one per player, so the cost is multiplied by the pane count and the rig has
to build them per player rather than once.

Build it as a prototype behind a flag first and judge it at the controls before committing to it.
The alternative worth naming in the write-up, so it is not re-derived later, is a Godot build with
large-world-coordinates doubles, which fixes the class of problem outright and costs a custom engine
build; it is almost certainly not worth it for one panel, but the next session should not have to
work that out again.

**Model recommendation.** High. This is a large change across the render rig with real blast radius
into splitscreen, and the judgement of whether the result is worth its cost is the item's actual
deliverable.

**Verify.** The same centroid measurement C20 built, on the prototype: the per-instrument residual
must fall below the noise floor C20 established. Then at the controls, in the condition the report
came from, since the eye is what reported this and the eye is what closes it. Then the full battery,
with particular attention to the four plane-bearing goldens and to the hitch check, since this adds
a render pass.

**⚠ Traps.** Do not start this before C20 answers. It is the expensive item in the plan and the
cheap test that justifies it costs an afternoon. If C20 comes back negative, this item closes as
disproven and the write-up of why is the deliverable, which is a success here and not a failure.
