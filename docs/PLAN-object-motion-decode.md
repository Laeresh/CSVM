# OBJECT_MOTION — the original's launch, contact and run-time model, decoded

**ACTIVE PLAN** (written 2026-08-10). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan replaces the engine's `OBJECT_MOTION` launch and termination model with the one decoded
from the original's own per-frame update, `FUN_004e8fa0` in `crimson.exe`. Three shipped readings
are wrong: the `translation_range` elevation is a **linear** parameterisation, not a spherical
angle; the ground-contact test is the **default**, not something `do_intersections` switches on;
and `RUN_TIME` is a **ceiling with a partial final step**, not a flight duration. Those three
account, between them, for `BL-319`'s two symptoms (`m_build03` cut mid-arc, `genx12` ending
underground), `BL-245`'s deferred falls, and most of what `BL-022`'s hand-tuned
`DebrisTune.LaunchScale` of 0.65 was compensating for — so the tune is deleted rather than
re-judged.

Out of scope: the `OBJECT_MOTION_FROM_TO` and `OBJECT_MOTION_SI_SCRIPT` event kinds, which share a
`RUN_TIME` field name but not this update path; `BL-059` item 1's missing air-crash **trigger**,
which is unaffected; the `morph` channel, which the original's update reads and this engine still
does not; and the `impact_force` velocity-inheritance mechanism found in the same function, which
belongs to `BL-008`/`BL-122` and is filed, not built, here (see A2). Each folded-in backlog item was
re-verified still-open against both the record (`git log --grep=BL-319`, `--grep=BL-245`,
`--grep=BL-022`) and the code before being drawn in; `BL-022` is already **closed** (`a151289`), so
the constant's removal is minted under a fresh ID rather than reopening it.

## Milestone goal

- A `translation_range` launch aims where the original aims, at the speed the original launches it
  — including the gun-casing ejection, which shares the same expression.
- Every gravity-bearing ballistic body is contact-tested by default; `NO_ALTITUDE` opts out and
  `DO_INTERSECTIONS` upgrades to the geometry sweep, matching the original's two tiers.
- `RUN_TIME` behaves as a ceiling everywhere, and a launch that carries none is bounded by the
  original's own watchdog rather than by an invented launch-height solve.
- No global debris tuning knob exists in the tree.
- A golden capture actually exercises debris coming to rest.

**No compensating scalar is introduced for anything this plan changes.** If the result reads wrong
at the controls, the answer is a further decode or a filed item — not a multiplier. Re-absorbing a
decode error into a "look" is the exact failure this plan exists to undo.

## Decisions (2026-08-10)

| # | Question | Decision |
|---|---|---|
| 1 | Decompile vs. `PT-46` (d), which confirmed at the controls that debris sinks through terrain | **Decompile is authoritative; re-open `PT-46` (d)** — (d) confirmed a symptom and attributed a mechanism; the mechanism is what the binary contradicts |
| 2 | One contact mechanism (reuse `TryContact`'s sweep everywhere) or the original's two tiers | **Two tiers, as the original has them** — a sweep everywhere would rest debris on walls and rooftops the original drops straight past, which is divergence dressed as fidelity |
| 3 | Golden coverage, given the `ContactMask == 0` structural fallback keeps the change inert | **Keep the fallback, pin a new golden that shows debris landing** — "inert by default" plus "no shot exercises it" is how a regression walks in unnoticed |
| 4 | What happens to `DebrisTune.DefaultLaunchScale = 0.65` | **Decode it, don't re-judge it** — the elevation decode supplies a 0.745–0.81 magnitude ratio across `m_build03`'s band; 0.65 was a decode error wearing a tune's clothes |
| 5 | How far "remove the knob" goes | **Everything** — class, `--debris-launch`/`--debris-gravity`, the `debris.*` config keys, `Launcher.ApplyDebrisTune`, the `WorldDamageLab` panel, the `Suites.cs` test, and `GravityScale` with them |
| 6 | Work `BL-319` alone, or scaffold a plan | **A plan, decode fixes before contact work** — the contact tier is only judgeable once the arcs are right; landing pieces that still launch 25 % too fast invites a compensating tune |
| 7 | `BL-245`, tagged `[Blocked: a decision to diverge]` | **Folds in and closes here** — its blocker dissolves rather than being decided: `do_intersections: false` means *test with the cheap tier*, so its falls simply land |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `do_intersections: false` means the original ran no contact test, so debris sinking through terrain is faithful | `FUN_004e8fa0` branches `!0x8000 → !0x4000 → ground-column query`. The default path **is** a contact test; `0x8000` (`DO_INTERSECTIONS`) upgrades it to the geometry sweep |
| 2 | `no_altitude` is NOT a second, default terrain test — it "most likely means gravity or spawn positioning reckoned relative to terrain altitude" (`docs/formats/destructibles.md:365-370`) | Exactly inverted. `NO_ALTITUDE` (`0x4000`, set at `00508bf8`) is the **opt-out from the landing test**, authored on `gunshell` alone. The reasoning that killed it ran from `PT-46` (d), which had misattributed its own mechanism |
| 3 | `translation_range.y` is an elevation angle, applied as `sin(el)` with `cos(el)` on the horizontal | The original computes `dirY = elev/90` and horizontal `1 − \|elev\|/90` — an L1 direction, not unit length (0.707 at 45°). Only the azimuth goes through `deg→rad` and a sincos |
| 4 | `translation.delta` ramps velocity over `run_time`, i.e. an acceleration of `delta/run_time` (`MotionRuntime.cs:358`) | The original stores `dir·delta` straight into the acceleration slot (`+0x4c..0x54` → `+0x64..0x6c`) and adds gravity once. No division |
| 5 | Ending a no-`RUN_TIME` launch when its parabola returns to launch height is the best available stand-in, since the original had no test to copy | It had one. `FlightToLaunchHeight` was a stand-in for a mechanism that exists; the original bounds an untimed body with a watchdog (15 s column / 35 s sweep) and ends it on contact |
| 6 | Every golden capture builds no world colliders (`MotionRuntime.cs:212`, `WorldEffectsFactory.cs:372`) | `SessionSpec.cs:183` makes `BuildsCollision` true for `Fly`, and goldens 11–13 are flight sessions — one of them is a plane crash, which cannot work without colliders |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A3, B4, B5, C6, C7, C8, C9 | Confirm the trace against `FUN_004e8fa0` yourself, then implement. Every one of these is transcribed from the original's update, not inferred from behaviour. |
| **Direction sound, magnitude a judgement call** | C9 (the 0.2 restitution's *feel*, not its value), D11 (which shot to pin) | The value is read from the binary; what is judged is whether the resulting look needs a follow-up item. Do not answer a bad look with a new scalar — see the milestone boundary. |
| **Leads only — no mechanism yet** | A1 | Budget for investigation. A correct disproof that lands no code is a success here. (A2 has since landed and moved A3 up: the bit it was to name is `COMPLEX`, confirmed at the parser.) |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

**The update.** `FUN_004e8fa0` (`crimson.exe`) is the per-frame `OBJECT_MOTION` update. Its flag
word is at `motion+0xc`; `RUN_TIME` is at `motion+0x148`. The parser is `FUN_00508590`, which sets
`NO_ALTITUDE → 0x4000` (`00508bf8`) and `DO_INTERSECTIONS → 0x8000` (`00508c41`), and which emits
`OBJECT_MOTION: NO_ALTITUDE fall lacks RUN_TIME` (`0063161c`) — the diagnostic that names the
dependency between the two.

**The launch direction** (`TRANSLATION_RANGE`, flag `0x8`), transcribed:

```
fVar1 = elevation * 0.011111111;                 // 1/90, exactly
fVar6 = fVar1 < 0 ? fVar1 + 1.0 : 1.0 - fVar1;   // h = 1 - |elev|/90
dir   = ( cos(az)*h , fVar1 , sin(az)*h )        // cached at +0x70/0x74/0x78
vel   = dir * speed                              // +0x40..0x48 -> live +0x58..0x60
ramp  = dir * delta                              // +0x4c..0x54 -> live accel +0x64..0x6c
```

Gravity is added once into the acceleration's Y (`+0x68 += +0x18`) when flag `0x1` is set and
`0x2000` is clear. The vector `TRANSLATION` form (flag `0x4`) copies `+0x40..0x54` into the live
slots unchanged, so it is unaffected by the elevation finding.

**What that costs us, on the canonical example.** `m_build03` part1
(`extracted/C1/cam_anim/m_build03-m_build03-m_bld_healthy.json:606-636`): azimuth 35–55°, elevation
60–70°, speed 28–37, gravity −10, authored `RUN_TIME` 5.0 s.

| | `v0y` at e=60, s=28 | `v0y` at e=70, s=37 | flight to launch height |
|---|---|---|---|
| shipped (unit sphere) | 24.2 m/s | 34.8 m/s | **4.85 – 6.95 s** |
| original (`elev/90`) | 18.7 m/s | 28.8 m/s | **3.73 – 5.76 s** |

Under the shipped decode every draw meets or exceeds the 5.0 s ceiling — which is the reported
symptom, "six of the nine parts cut at 67–72 % of arc". Under the original's, most draws come down
inside it and only the tail is clipped. Magnitude ratio across the band: **0.745 – 0.81**, against
a judged `LaunchScale` of 0.65 and a frame-comparison estimate of ~0.58.

**The contact tiers.** `FUN_004e9e30` is the default tier: it calls
`FUN_004c76e0(collisionDB, x, y+step, z, out)` — a vertical **column query** returning candidate
surface records (0x2c bytes each, height at `+0x14`), and picks the nearest below within 10 m. The
caller reads the struck surface's type at `+0x20` and maps it `1→1, 4→2`, which is the
`default`/`water`/`lava` `BOUNCE_SEQUENCE` branch index. On contact the step is reflected, velocity
is damped by **0.2**, and the body ends once a bounce no longer loses energy. `FUN_004c8ec0` is the
`DO_INTERSECTIONS` tier, a full geometry sweep against the same world database.

**The termination model.** With `0x400` (`RUN_TIME` authored) the final step is shortened by the
overshoot so the motion ends exactly on time, and the update returns "done" once elapsed ≥
`RUN_TIME`. With no `RUN_TIME`, `+0x148` is reused as a watchdog accumulator that kills the body at
**15 s** on the column path and **35 s** on the sweep path.

**The flag word at `motion+0xc`, every bit named from the parser** (A2, 2026-08-10 — each bit is
the `OR` `FUN_00508590` executes on recognising the token, at the address given; full table and
corroboration in `analysis/object-motion-flags/FINDINGS.md`).

| Bit | Token | Parser | What the update does with it |
|---|---|---|---|
| `0x1` | `GRAVITY` | `0050868b` | gates the gravity add and the whole contact block |
| `0x2` | `IMPACT_FORCE` | `00508d03` | gates adding the parent object's velocity (`param_1+0xc0..0xc8`) into the launch — out of scope, filed as `BL-343` |
| `0x4` | `TRANSLATION` (vector form) | `00508d27` | copies `+0x40..0x54` into the live slots |
| `0x8` / `0x10` | `TRANSLATION_RANGE_MIN` / `_MAX` | `005095de` / `00509df0` | draws the four ranges and builds the direction |
| `0x20` | `XYZ_ROTATION` | `0050a60f` | steady spin integration |
| `0x40` / `0x80` | `FORWARD_ROTATION DISTANCE` / `TIME` | `0050b309` / `0050b34b` | tumble, two parameterisations |
| `0x100` | `SCALE` | `0050b7ac` | scale ramp, clamped at 0.001 |
| `0x200` | `MORPH` | `0050c21a` | writes `node+0x3c → +0x24`, clamped to 1 |
| `0x400` | `RUN_TIME` | `0050ca4e` | selects ceiling semantics over watchdog semantics |
| `0x800` | `BOUNCE_SEQUENCE` | `0050c678` | resolves the branch name against the def's table |
| `0x1000` | `BOUNCE_SOUND` | `0050c732` | volume scaled by impact speed / `FULL_VOLUME_VELOCITY` |
| **`0x2000`** | **`GRAVITY COMPLEX`** | **`0050899c`** | suppresses the gravity add *and* widens the landing test |
| `0x4000` | `GRAVITY NO_ALTITUDE` | `00508bf8` | opt-out from the landing test |
| `0x8000` | `GRAVITY DO_INTERSECTIONS` | `00508c41` | upgrades the landing test to the geometry sweep |

**The flag census, re-derived** (A2, 2026-08-10, `analysis/object-motion-flags/census.py`). Over
3,051 `ObjectMotion` events in all 8 chapters, 1,625 carry a `gravity` block and every one of them
is ballistic: **1,363** / 166 / 88 / 8 across the four combinations. `do_intersections` reproduces
**166** exactly, and `no_altitude`'s 8 (`gunshell`, one per chapter) were never wrong — the "5
chapter files" grep does not reproduce. The all-false row moved from the published 1,378, which was
an arithmetic slip against `destructibles.md`'s own `do_intersections`×shape table rather than a
data change. So the default-combination bodies that should be landing and are not are
1,363 + 88 = **1,451**, alongside the 120 bounce-shape and 167 vanish-shape launches currently
ended by `FlightToLaunchHeight`. The parser's fifth `GRAVITY` token, **`LOCAL <value>`**, sets no
bit and only selects where the gravity number comes from, so it is erased at compile time and
*cannot* be surfaced by the extractor — the three booleans that ship are the three that exist.

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

### Wave A — ground truth before any code moves

1. ☑ Establish what the goldens actually cover, and take the pre-change baseline
2. ☑ Finish the gravity-flag map (`0x2000`, `GRAVITY LOCAL`) and re-derive the census
3. ☑ Handle flag `0x2000`: the suppressed gravity add and the widened landing test

### Wave B — the launch decode

4. ☐ `translation_range` elevation is linear (`elev/90`), not spherical
5. ☐ `translation.delta` is an acceleration, not a ramp divided by `run_time`

### Wave C — contact and termination

6. ☐ The default contact tier: the ground-column query
7. ☐ `NO_ALTITUDE` as the opt-out, and `gunshell` as its only author
8. ☐ `RUN_TIME` as a universal ceiling; retire `FlightToLaunchHeight` for the watchdog
9. ☐ Landing response: 0.2 restitution and energy-loss termination

### Wave D — the tune, the coverage, the record

10. ☐ Delete `DebrisTune` entirely
11. ☐ Pin a golden that shows debris coming to rest
12. ☐ Rewrite the decode records that carried the disproven readings
13. ☐ Item bookkeeping: `BL-319`, `BL-245`, `PT-46` (d), and a fresh ID for the deleted tune

## Dependency and parallelism notes

**Waves run in order, and the wave boundaries are real.** All of Wave B precedes all of Wave C —
the settled constraint from the 2026-08-10 session: the contact tier is only judgeable once the
arcs are right, and landing pieces that still launch 25 % too fast is precisely the situation that
produced the tune this plan deletes. All of Wave A precedes Wave B, because A1 is the baseline
every later item's verification is measured against, and A2 → A3 decides what C6 and C7 gate on.

**A1 blocks everything.** Nothing may re-pin a golden before A1 has recorded what each golden
covers and what its pre-change hash is.

**A2 → A3 is a chain, and A3 is deliberately split across waves.** A3 lands the half of `0x2000`
that stands alone — the suppressed gravity add — and *specifies* the half that cannot land until a
landing test exists: the widened admission condition, which C6 consumes. C6 must not re-decide it.

**File contention — do not run these in parallel worktrees.** `CSVM/src/Mech3/Anim/MotionRuntime.cs`
is touched by A3, B4, B5, C6, C7, C8, C9 and D10: that is most of the plan, and it is one file. Run
Waves A (from A3), B and C strictly serially. `analysis/goldens/manifest.json` is touched by B4
(re-pin), B5 (possible re-pin), D10 (re-pin) and D11 (new shot) — same rule.

**Genuinely parallelisable:** D12 (documentation) and D13 (backlog bookkeeping) touch disjoint files
(`docs/org/objectMotion.md` + `docs/formats/destructibles.md` + `docs/architecture.md` +
`analysis/*/FINDINGS.md` vs `backlog.md` + `playtest.md`) and can run concurrently once Wave C has
landed, provided each agent owns only its own files. ⚠ D12 also edits `MotionRuntime.cs`'s class
remark, so it contends with anything still open in Waves A–C — run it only after C9 has landed.

---

# Wave A — ground truth before any code moves

## A1 ☑ Establish what the goldens actually cover, and take the pre-change baseline

**Goal.** A written, checked-in answer to "which goldens exercise ballistic debris, which of them
build world colliders, and does any capture window contain a piece actually coming to rest" — plus
the pre-change hashes every later item measures against.

**Evidence (confidence: lead-only).** `SessionSpec.cs:183` — `BuildsCollision => Fly || DamageTest
|| ForceCollision || DebugDamage != null` — and `Mode` resolves "any content arg defaulting to
flight" (`SessionSpec.cs:107`), so goldens 11–13 in `analysis/goldens/manifest.json`
(`--chapter=C1 --plane=player_bhawk …`) are flight sessions and **do** build colliders. That
contradicts `MotionRuntime.cs:212` and `WorldEffectsFactory.cs:372`, both of which assert every
golden capture builds none. Golden 13 is `--crash=5`, a plane crash, which cannot work without
colliders. Unresolved and load-bearing: `PLAN-ground-contact` landed `TryContact` and reported all
13 goldens hash-identical — if goldens 12–13 really do run the sweep, the most likely explanation is
that their capture window ends before any piece reaches the ground, which would mean they do not
cover contact either and D11 has to.

**Approach.** Read `analysis/goldens/manifest.json` end to end. For each shot, record: mode, whether
`BuildsCollision` is true, whether any `OBJECT_MOTION` with a `translation`/`translation_range`
block runs inside the capture window, and whether a body reaches `Finished`. `MotionSet` already
exposes `LaunchCount`, `ContactLandings` and `ClockEndings` — read those at capture time rather than
inferring from pixels; that is what they were built for. Then run `.\RunTests.ps1` and record all 13
hashes as the baseline. Correct the two wrong comments in the same commit. Do **not** re-pin
anything here.

**Model recommendation.** medium — mechanical evidence-gathering across a known file set, but the
conclusion decides D10's scope, so it needs judgement in the write-up.

**Verify.** `.\RunTests.ps1` green, 13/13 hashes recorded in the commit message. The claim "golden N
covers debris landing" is only accepted if `ContactLandings` is non-zero for that shot — an
unchanged hash is not evidence that a shot covers something, only that nothing moved.

**⚠ Traps.** The comments at `MotionRuntime.cs:212` and `WorldEffectsFactory.cs:372` are *assertions
about the goldens*, not the goldens themselves — do not treat them as evidence, they are the thing
under test. And `docs/verification.md`'s rule bites hard here: an unchanged number is not evidence
unless you have seen it able to fail, so before trusting a zero `ContactLandings` reading, prove the
counter can move by forcing a landing.

## A2 ☑ Finish the gravity-flag map (`0x2000`, `GRAVITY LOCAL`) and re-derive the census

**Landed 2026-08-10** — `analysis/object-motion-flags/`. `0x2000` **is** `COMPLEX`
(`OR ESI, 0x2000` at `0050899c`), and all sixteen bits now carry a parser address; the table in
"What the data actually ships" above is the result. Two of this plan's own inferred rows were wrong
and are corrected there (`0x200` is `MORPH`; `TRANSLATION_RANGE` is two bits). `GRAVITY LOCAL` sets
no bit — it is a value selector, erased at compile time, so the extractor cannot surface it and
there is nothing to teach it. The census reproduces 166 and 8; the all-false row is **1,363**, not
1,378, which makes A3/C6's population **1,451**. `impact_force` filed as `BL-343`. ⚠ For A3: every
one of the 254 `complex` carriers is aircraft wreckage, and three of them author gravity *stronger*
than Earth's (−15, −20) — the value is authored deliberately, so the non-constant path A3 is told to
find had better exist.

**Goal.** Every bit of `motion+0xc` that this plan depends on is named with its parser evidence, and
the four-way flag census is re-derived from `extracted/` rather than carried forward.

**Evidence (confidence: lead-only).** The flag table in "What the data actually ships" above is two
bits confirmed and eleven inferred. `0x2000` matters because it does two things at once in
`FUN_004e8fa0`: it suppresses the one-time gravity add (`+0x68 += +0x18` runs only when `0x1` is set
and `0x2000` is clear) and it widens the landing test, which otherwise fires only on a descending
step (`local_28 < 0.0 || (flags & 0x2000)`). `complex` is the leading candidate on population
grounds — 166 + 88 = 254 events carry it, per `destructibles.md:358-363`. Separately, the parser
recognises a `GRAVITY LOCAL` token (`00631ae8`) that the extractor's `gravity` block never
surfaces: three booleans ship where the original parses four. And the census counts themselves are
suspect — a grep for `"no_altitude": true` across `extracted/` hit 5 chapter files against a table
claiming 8 events.

**Approach.** Find the parser's `COMPLEX` and `LOCAL` handlers the same way `NO_ALTITUDE` and
`DO_INTERSECTIONS` were found: xref the strings at `00631ac0`/`00631ae8` into `FUN_00508590` and
read the `OR ECX, <bit>` that follows. Then write a census script under
`analysis/object-motion-flags/` (the `analysis/object-motion-range/census.py` and
`analysis/object-motion-ground-rest/census.py` pair is the shape to copy) that re-derives the
cross-tab over all 8 chapters, adds a `local` column if the extractor can be taught to emit it, and
lands a `FINDINGS.md` beside it.

**Model recommendation.** high — a decode item whose output gates A3, C6 and C7; a wrong bit here
propagates into the contact tier's admission test, and the census numbers end up quoted in
`docs/formats/`.

**Verify.** Each bit claim cites a parser address. The census reproduces the previously published
166 for `do_intersections: true` (a number that has been checked twice and should not move), and any
count that *does* move is explained in the `FINDINGS.md` rather than silently replacing the old one.

**⚠ Traps.** ⚠ **`impact_force` (bit `0x2`) is out of scope and must stay that way.** The same
function shows the original adding the parent object's velocity (`param_1+0xc0..0xc8`) into a launch
when that bit is set and a condition on the parent holds — which is a real velocity-inheritance
mechanism, and `BL-008` was *closed* on the finding that the original does not inherit into world
debris. That closure may have been right for the wrong reason. **File it as a new backlog item and
move on**; building it here would smuggle a second decode into a plan whose verification is already
wide. ⚠ Do not assume `0x2000` is `complex` because the populations match — 254 is also consistent
with other groupings, and the parser evidence is cheap to get. ⚠ If the extractor cannot emit
`local`, say so and leave it; a missing field recorded is worth more than a guessed one.

## A3 ☑ Handle flag `0x2000`: the suppressed gravity add and the widened landing test

**Landed 2026-08-10.** The suppression is real and the conclusion drawn from it was wrong: the
non-constant path exists, forty lines below in the same function. `COMPLEX` does not remove gravity —
it **replaces the scalar add with a transformed one**. The plain form does `accel.y += gravity.value`
in the body's own frame; the `COMPLEX` form skips that and instead runs the world-down vector
`(0, gravity.value, 0)` through the node's matrix into that frame, via the identical machinery the
`IMPACT_FORCE` branch uses to bring the parent object's world velocity in. So a body under a banked,
pitched or inverted parent falls down the **world** rather than down its own hull, and under a
world-aligned parent the two forms are arithmetically identical — which is exactly why the install
authors it on aircraft wreckage and on nothing else. `MotionRuntime.Create` now builds the
acceleration through `GravityAccel()`, converting with the parent basis when `complex` is set.

**⚠ The C6 specification, which C6 must consume rather than re-decide.** The widening belongs to the
**column tier only**. In `FUN_004e8fa0` the per-step admission test inside the
`!DO_INTERSECTIONS → !NO_ALTITUDE` branch is `stepY < 0 || (flags & COMPLEX)`: without `COMPLEX` the
column query is consulted only on a **descending** step, with it on **every** step. The `15 s`
watchdog sits inside that same test, so a non-`COMPLEX` body does not even accumulate it while
climbing. The `DO_INTERSECTIONS` sweep branch carries **no** such condition — it sweeps every frame
regardless of direction, which is what `TryContact` already does, so C6 changes nothing there.

**Also settled, so nobody re-derives it.** `IMPACT_FORCE` is a strict subset of `COMPLEX` install-wide
(182 events, all of them `complex: true`), which makes unreachable the one combination where the
original would apply gravity **twice** — the scalar add followed by the transformed one, for a
gravity-bearing non-`COMPLEX` body with `IMPACT_FORCE` armed. It is in the binary; no data reaches it.

**Goal.** The engine does what the original does when `0x2000` is set — and the plan carries a
written answer to what that flag *is*, rather than a bit nobody named. Concretely: a `0x2000` body
does not receive the one-time constant gravity add, and its landing test is not restricted to
descending steps.

**Evidence (confidence: traced).** In `FUN_004e8fa0` the bit
does exactly two things, both unambiguous in the decompile. Gravity: `+0x68 += +0x18` runs only when
`0x1` is set **and** `0x2000` is clear, so a `0x2000` body never gets the constant acceleration from
the `gravity.value` field. Landing: the contact block's admission test is
`local_28 < 0.0 || (flags & 0x2000)` — with the bit set, the test fires on *every* step, not only a
descending one. The authored field is settled: A2 confirmed the bit is set by the `GRAVITY COMPLEX`
token at `0050899c`, on 254 events / 25 distinct shapes, all of them aircraft wreckage.

**Approach.** Land the gravity half here — it is standalone and needs no contact tier. **Specify**
the landing half here and let C6 consume the specification; do not re-decide it there. Name the bit
`COMPLEX` throughout, per A2.

**Model recommendation.** high — the gravity half can silently remove gravity from a quarter of the
install's ballistic events, and a wrong reading here looks like "debris floats" three items later
with no obvious cause.

**Verify.** A2's census says **254 events / 25 distinct shapes** carry `COMPLEX` — check that many
bodies change behaviour, no more and no fewer, and that they are the aircraft-wreckage family the
census names (the eleven airframes, `player`, both `player_crash_*`, `agyrobus`,
`autogyro_loserotor`, `drop_smokescreen_canister`) rather than any world destructible. The
8-chapter regression, plus a targeted anim-lab look at one affected def.

**⚠ Traps.** ⚠ **"No constant gravity add" does not mean "no gravity".** The name argues that
gravity is computed by a *different, non-constant* path rather than switched off, and A2 supplies
the data that backs it: three of the `COMPLEX` shapes (`player` pieces 2/3/4) author gravity of
**−15 and −20**, stronger than anything non-`COMPLEX` in the install, which is not what a body with
gravity switched off gets authored. 254 pieces of debris floating away is the loud, obvious symptom
of implementing only the suppression. Find that path before landing this, and if you cannot find it,
**stop and say so** rather than shipping the suppression alone. A correct disproof lands no code and
is a success. ⚠ The widened landing test is not cosmetic: a body tested on ascending steps can land
on the ceiling of whatever it launched from, which is exactly the class of behaviour the two-tier
decision was protecting. Specify it precisely for C6. ⚠ Do not conflate this bit with `0x4000`
(`NO_ALTITUDE`) — they sit adjacent, do opposite things to the landing test, and a transcription
slip between them is invisible until debris stops landing.

---

# Wave B — the launch decode

## B4 ☐ `translation_range` elevation is linear (`elev/90`), not spherical

**Goal.** A `translation_range` launch produces the original's direction and speed: `dirY = elev/90`
with horizontal magnitude `1 − |elev|/90`, a vector that is deliberately not unit length. `m_build03`
part1's arc comes down inside its 5.0 s ceiling instead of being cut at 72 % of it.

**Evidence (confidence: traced).** `FUN_004e8fa0`'s `TRANSLATION_RANGE` block, transcribed in full
in "What the data actually ships" above. `0.011111111` is `1/90` exactly. Only the azimuth is
converted `deg→rad` and passed to the sincos at `FUN_0053c6c0`; the elevation never is. The
resulting magnitude dips to 0.707 at 45° and returns to 1 at 0° and 90°. The current expression is
`MotionRuntime.RangeLaunchDirection` (`MotionRuntime.cs:478-484`), which builds
`cos(el)cos(az), sin(el), cos(el)sin(az)` — a unit-sphere direction.

**Approach.** Rewrite `RangeLaunchDirection` to the L1 form. It is deliberately the single
expression of this decode — `MotionRuntime.cs:474-477` records that `ProjectilePool`'s gun-casing
ejection reads the same `gunshell` event and must share it, "two spellings of the maths is how they
disagree" — so changing it here changes shell ejection too, which is correct and intended. Update
the class remark's `translation_range` bullet in the same edit; it currently describes the spherical
reading as settled. Leave the azimuth convention alone: which world bearing azimuth 0 points along
is already recorded as a choice, and `FUN_0053c6c0`'s output order has not been checked — note it,
do not change it.

**Model recommendation.** high — small diff, very wide blast radius (every `translation_range` event
in the game plus every ejected shell casing), and it will move goldens.

**Verify.** Baseline first, from A1. Expect goldens 12–13 to move — a debris-bearing shot whose hash
does *not* move after this is a red flag, not a pass. Re-pin with the `exercises` field **rewritten,
not appended** (the commit hook enforces ≤250 chars, no item ids, no dates). At the controls:
`--destroy=m_build03` with `--debug-anim`, confirming pieces land or reach their ceiling rather than
freezing mid-climb. Then the 8-chapter `--freecam` regression for zero errors. Shell ejection gets
its own look via the PT-46 profile (`--fire --infinite-ammo`) — casings should still arc away
visibly, and a collapse to near-zero throw means the elevation sign or the horizontal term is wrong.

**⚠ Traps.** ⚠ **Do not "fix" the non-unit magnitude.** A direction whose length varies with
elevation looks like a bug and is not — it is the whole finding, and normalising it reinstates the
error this item exists to remove. ⚠ Do not touch `DebrisTune` here; it still ships at 0.65 through
this item and its removal is D10. That means the intermediate build launches at roughly 0.5 of
authored and will look weak — that is expected, and it is not a reason to re-tune. ⚠ The
`translation_range_min_only` rule is unrelated and stays exactly as it is.

## B5 ☐ `translation.delta` is an acceleration, not a ramp divided by `run_time`

**Goal.** The `delta` speed ramp contributes the acceleration the original gives it, with no
division by the flight time.

**Evidence (confidence: traced).** The original stores `dir·delta` into `+0x4c..0x54` and copies it
straight into the live acceleration at `+0x64..0x6c`, then adds gravity once into the Y. There is no
`/run_time` anywhere on that path. `MotionRuntime.cs:356-358` does `m._accel += rtSafe > 0f ?
rampTotal / rtSafe : Vector3.Zero`, with a comment reasoning that a change spread *over* the run
time must be divided by it — a plausible reading that the binary does not support.

**Approach.** Drop the division; fold `rampTotal` in as an acceleration directly. This also removes
the ordering constraint that forced the ramp to be collected early and applied after the flight
solve (`MotionRuntime.cs:254-257`), which simplifies `Create` — but do that simplification only if
it falls out cleanly, since C8 is about to rework the same region.

**Model recommendation.** medium — a one-line semantic change with a narrow population, but it sits
in the file every other item touches.

**Verify.** `delta` is 0 on 984 of 1,217 `translation_range` events, so most of the install is
unaffected and most goldens should not move — which makes this the one item where an unchanged hash
is meaningful, *provided* A1 confirmed the covering shot can move at all. Find a non-zero-`delta`
def and check it directly through the anim lab rather than relying on the aggregate.

**⚠ Traps.** ⚠ The units change: a `delta` that was previously divided by a 5 s run time now
contributes five times as much acceleration. If a specific def looks wrong afterwards, the answer is
to re-read its block, not to reinstate the division. ⚠ Keep this a separate commit from B4 — two
launch-shape changes in one commit make a golden movement impossible to attribute.

---

# Wave C — contact and termination

## C6 ☐ The default contact tier: the ground-column query

**Goal.** Every gravity-bearing ballistic body is contact-tested by default. A piece thrown off a
destroyed structure lands on the ground and stays there, in every session that builds colliders,
without authoring `do_intersections`.

**Evidence (confidence: traced).** `FUN_004e8fa0`'s contact block branches
`!0x8000 → !0x4000 → FUN_004e9e30`, which calls `FUN_004c76e0(collisionDB, x, y+step, z, out)` — a
vertical column query returning `0x2c`-byte surface records with a height at `+0x14`, choosing the
nearest below within 10 m. The landing fires when the step is descending and the next Y falls below
that height. The struck surface's type at `+0x20` maps `1→1, 4→2`, which is the branch index for
`default`/`water`/`lava`. Today `MotionRuntime.cs:217` gates the test on
`do_intersections && ContactMask != 0`, so this tier does not exist in the engine at all.

**Approach.** Add the column tier alongside `TryContact` rather than inside it — two named methods,
because they are two mechanisms and the difference is the point (Decision 2). The admission test
becomes "gravity authored, `NO_ALTITUDE` clear" for the column tier and "`DO_INTERSECTIONS` set" for
the sweep; the per-step condition comes from **A3's specification**, not from a fresh reading of the
decompile — A3 owns `0x2000`'s widening of it. Keep the `ContactMask == 0` structural fallback exactly as it is (Decision 3): a session
that wires no mask still takes the untouched path, which is what keeps the labs and headless suites
deterministic. Reuse the existing surface classification hook (`SurfaceIsWater`) so a landing piece,
a round's impact and a wingtip graze cannot disagree.

**Model recommendation.** max — the widest behaviour change in the plan (~1,451 events plus every
bounce and vanish launch), on the hot path, in the file everything else touches.

**Verify.** `ContactLandings` non-zero for the column tier specifically — instrument it separately
from the sweep's, or the pair cannot answer "is the new tier doing anything". Cockpit: the PT-46
profile (`--fire --infinite-ammo`, C1 Devastator), destroy a ground-sitting structure, and watch
pieces come to rest rather than sink. Then the 8-chapter regression. D11 pins the golden.

**⚠ Traps.** ⚠ **Do not implement this as `TryContact` with a cheaper mask.** A segment sweep rests
debris on walls and rooftops that a column query drops straight past; reusing the sweep here is the
divergence Decision 2 rejected. ⚠ The query is a *column at a point*, not a ray along the
trajectory — a fast piece can pass over a ledge between frames and the original lets it. Do not
"improve" that. ⚠ Performance: this now runs on most debris in the game. Measure before assuming it
is free, and if it is not, the answer is a cheaper query, not a narrower admission test.

## C7 ☐ `NO_ALTITUDE` as the opt-out, and `gunshell` as its only author

**Goal.** `NO_ALTITUDE` suppresses the landing test, and nothing else does. The gun casing keeps
falling through the world; everything else stops.

**Evidence (confidence: traced).** `NO_ALTITUDE → 0x4000` at `00508bf8`, and the update tests it as
the inner gate of the non-`DO_INTERSECTIONS` branch. The parser's
`OBJECT_MOTION: NO_ALTITUDE fall lacks RUN_TIME` diagnostic (`0063161c`) confirms the dependency
from the other side: with the test off, nothing but a `RUN_TIME` would ever end the fall. A grep of
`extracted/` finds `"no_altitude": true` on `gunshell` alone — the one def in the install you would
opt out of a landing.

**Approach.** Read the flag in `MotionRuntime.Create` and use it as the column tier's veto. It has
never been read by this engine, so `AnimDefs`/`AnimData` plumbing may need it surfaced — check
before assuming it arrives. A2's re-derived census settles which defs carry it: `gunshell` alone,
8 events, one per chapter — the published figure, confirmed rather than replaced.

**Model recommendation.** medium — small and well-bounded once C6 exists, but it decides the fate of
the one def that sits in both halves of this plan.

**Verify.** Shell casings behave exactly as they did before this plan (they were never landed, and
must not start): PT-46 profile, `--fire --infinite-ammo`, casings arc and vanish. Every other def
lands. A `gunshell` that suddenly rests on the ground means the veto is inverted.

**⚠ Traps.** ⚠ `gunshell` is also touched by B4 — its ejection direction moves in Wave B and its
contact behaviour is decided here. Do not read a B4 regression as a C7 failure; check the commit
order first. ⚠ `gunshell` authors `RUN_TIME 2.0`, so the parser's
`OBJECT_MOTION: NO_ALTITUDE fall lacks RUN_TIME` diagnostic never fires on this install — the flag's
dependency on `RUN_TIME` is real in the parser but has no unbounded case here to guard against.

## C8 ☐ `RUN_TIME` as a universal ceiling; retire `FlightToLaunchHeight` for the watchdog

**Goal.** `RUN_TIME` ends a motion exactly on time with a shortened final step, everywhere — not
only for the flagged 166. A launch carrying no `RUN_TIME` is bounded by the original's watchdog
instead of by a solved parabola.

**Evidence (confidence: traced).** With `0x400` set, the update shortens the final step by the
overshoot (`dt = frameDt − (elapsed − RUN_TIME)`) and returns "done" once elapsed ≥ `RUN_TIME`. With
`0x400` clear, `+0x148` is reused as an accumulator that ends the body at **15 s** on the column
path and **35 s** on the sweep path. `MotionRuntime.FlightToLaunchHeight` (`MotionRuntime.cs:491`)
and its gate at `:341` are the stand-in this replaces; the class remark at `:44-53` calls it "a
CHOICE, not a decode" and justifies it on the grounds that the original was not collision-testing
these bodies — which disproven claim 1 removes.

**Approach.** Make the ceiling universal (it is currently `PLAN-ground-contact`'s behaviour for the
flagged set only) and add the partial final step. Replace the launch-height solve with the watchdog.
The knock-on is that `AnimRuntime.cs:2293-2312` reads `motion.RunTime` back to decide what the
sequence waits on — the watchdog must not become a 15 s sequence hold where a solved flight used to
be a 5 s one. That is the single riskiest interaction in this item: `BL-257`'s 167 vanish-shape
events are hidden by a null-start `ACTIVE_STATE` that fires when the motion's duration elapses
(`SequenceRunner.cs:329`, `_base = _clock + duration`), so a longer reported duration leaves debris
visible for longer, and a shorter one hides it in mid-air.

**Model recommendation.** max — it rewrites the termination model for every ballistic body and
reaches into sequence timing, where the failure mode is invisible in a still frame.

**Verify.** `dblcannon_flying_parts` via `--effects-test`/`biggun_flying_parts` is `BL-257`'s named
repro and the sharpest test of the sequence-timing interaction — pieces must fly, then vanish, with
neither a mid-air disappearance nor a long hang. `MotionSet.ClockEndings` vs `ContactLandings`
answers whether the watchdog is firing when contact should have. Full 8-chapter regression.

**⚠ Traps.** ⚠ **The watchdog is a backstop, not a duration.** If pieces routinely end at 15 s,
contact is broken — do not tune the watchdog down to hide it. ⚠ Retiring `FlightToLaunchHeight`
removes the no-apex guard that kept `BL-245`'s falls out of the solve; that is intended (they are
now column-tested), but confirm the 8 no-apex oddities the census named — `bridge_truck`,
`susp_bridge`'s burning ropes, the two `fuelbox` rockerarms — behave sanely rather than assuming.
⚠ Keep `Seek` free of the contact test, for the reason `MotionRuntime.cs:430-433` already gives: it
is the pose/scrub entry point and a test there fires on a backwards timeline drag.

## C9 ☐ Landing response: 0.2 restitution and energy-loss termination

**Goal.** A landing piece bounces the way the original bounces — velocity scaled by 0.2 — and the
body ends when a bounce stops losing energy rather than after a fixed count.

**Evidence (confidence: traced).** On contact the original reflects the step (half the descending
step above the surface when horizontal speed exceeds 0.1 or vertical exceeds 0.5, otherwise resting
exactly on it), scales all three velocity components by `0.19999999`, and compares the post-bounce
speed² against the pre-bounce acceleration² — ending the body when it no longer decreases. The
`BOUNCES` token exists in the binary (`0063d1f4`) and may cap this independently; that is worth a
look but is not load-bearing.

**Approach.** Implement inside the landing path shared by both tiers, so a column landing and a
sweep landing respond identically — the response is not what the two tiers differ in.

**Model recommendation.** high — small numerically, but it decides whether debris reads as settling
or as jittering, and a wrong termination test loops forever.

**Verify.** Watch a single piece at the controls through its whole landing; it should hop once or
twice and stop. A piece that never stops means the energy comparison is inverted — bound the
iteration defensively even so.

**⚠ Traps.** ⚠ `0.19999999` is `0.2` in float; do not transcribe the artefact. ⚠ The two speed
thresholds (0.1 horizontal, 0.5 vertical) are asymmetric on purpose — do not tidy them into one. ⚠
The bounce *branch* (`default`/`water`/`lava`) comes from the struck surface and is already wired;
this item is the physical response only, and `lava` remains dead data across the install.

---

# Wave D — the tune, the coverage, the record

## D10 ☐ Delete `DebrisTune` entirely

**Goal.** No global debris multiplier exists in the tree, in any form that can persist across
sessions or silently absorb a future decode error.

**Evidence (confidence: traced — this is Decision 5, not a discovery).** The surface is
`CSVM/src/Mech3/Anim/DebrisTune.cs` (the class, `DefaultLaunchScale = 0.65`, `DefaultGravityScale =
1`, `IsTuned`, `IsAuthored`, `Reset`, `UseAuthored`); `Launcher.ApplyDebrisTune`
(`Launcher.cs:768-791`) and its call site at `:356`; the `--debris-launch`/`--debris-gravity` flags
via `SessionSpec.DebrisLaunchScale`/`DebrisGravityScale`; the `debris.launchScale`/
`debris.gravityScale` config keys; `WorldDamageLab.BuildDebrisTune` (`WorldDamageLab.cs:344-376`)
with its two sliders, three buttons and readout, plus `SyncTuneSliders`/`UpdateTuneReadout`; and a
test in `Testing/Suites.cs`. `MotionRuntime.cs:203-208`, `:267` and `:299` are the code consumers,
and `MotionRuntime`'s class remark at `:75-80` documents the 0.65 as a judged look — that paragraph
goes with the knob, and the launch suites' `DebrisTune.UseAuthored()` pin named there goes with it.

**Model recommendation.** medium, low effort — mechanical removal across a known file list, with the
compiler as the safety net.

**Verify.** `dotnet build` clean with no `DebrisTune` reference anywhere (`Grep` the whole repo, not
just `src/` — `backlog.md`, `docs/formats/destructibles.md` and
`analysis/object-motion-ground-rest/FINDINGS.md` all mention it and are D12/D13's problem, but the
*code* must be free of it). `--dump-config` must not emit the `debris.*` keys. Goldens move here:
launch speed rises from 0.65× to 1× of authored.

**⚠ Traps.** ⚠ Removing the config-key reads also removes them from `--dump-config`; that is
intended, but check nothing asserts on their presence. ⚠ This is the item where the arc finally
reads at full authored speed *with* the B4 correction — the first honest look at the decode. If it
reads wrong, file an item; do not reintroduce a scalar (the milestone boundary).

## D11 ☐ Pin a golden that shows debris coming to rest

**Goal.** A capture in `analysis/goldens/manifest.json` that fails loudly if the contact tier
breaks.

**Evidence (confidence: direction sound — which shot is a judgement call).** Decision 3. A1 supplies
the constraint: if goldens 12–13 turn out not to reach a landing inside their capture window, this
shot carries the whole of contact coverage. It must be a session where `BuildsCollision` is true and
the window is long enough for pieces to arc and settle — a `--destroy=` shot on a ground-sitting
destructible is the natural shape.

**Approach.** Follow the manifest's existing entry shape. Choose a def whose pieces land inside the
window and whose `ContactLandings` is provably non-zero. Prefer the column tier specifically —
goldens 12–13 already touch the sweep, if A1 confirms they reach it.

**Model recommendation.** medium — the mechanics are routine; picking a shot that actually discriminates is the judgement.

**Verify.** The shot's `ContactLandings` is non-zero, and its hash changes if the contact tier is
disabled — prove that by disabling it once, locally, before pinning. A golden that cannot fail is
not coverage.

**⚠ Traps.** ⚠ The `exercises` field is hook-enforced: ≤250 chars, no item ids, no dates, no "also
exercises" clause, and it is **rewritten on a re-pin, never appended to**. ⚠ Determinism first —
`--det --mute` like every other shot, and a landing that depends on the seeded RNG's draw needs its
window chosen so every draw lands, not just the median one.

## D12 ☐ Land `docs/org/objectMotion.md`, and correct the records that carried the disproven readings

**Goal.** This plan's decode has a home of its own in `docs/org/`, the corrected records point at it
instead of restating it, and no document in the repo still teaches a reading this plan disproved —
each dead end left visible with its cause of death.

**Evidence (confidence: traced).** `acbb9ba` ("Harvest the executable decodes out of code comments
into `docs/org/`") established where an executable decode lives and what it looks like: a **function
map first, then behaviour and constants, never decompiler output**; decoded-from-executable and
measured-off-footage labelled apart on every claim; pre-existing conflicts left recorded as
conflicts rather than resolved in either direction; and a pointer line added to
`docs/architecture.md` from the module whose decode the page now owns. `docs/org/puffer.md` is the
reference style, and `docs/org/sequences.md` already owns the `SequenceRunner` decode this plan
leans on in C8.

The passages needing correction: `docs/formats/destructibles.md:365-370` (the "`no_altitude` is NOT
a second, default terrain test" paragraph — disproven claim 2), its cross-tab at `:358-363`
(re-derived by A2), `:304-312` (the launch-height solve justified on the original "not
collision-testing them either", plus `PT-46` (d)), `:313-321` (`do_intersections` framed as the
whole of the question) and `:371-380` (`BL-245` blocked on a decision to diverge);
`MotionRuntime`'s class remark at `:26-57`; and
`analysis/object-motion-ground-rest/FINDINGS.md:96-103` and `:112-120`.

**Approach.** Write `docs/org/objectMotion.md` in the `puffer.md` house style. Function map:
`FUN_004e8fa0` (the per-frame update), `FUN_00508590` (the parser, and where each flag bit is set),
`FUN_004e9e30` → `FUN_004c76e0` (the column tier), `FUN_004c8ec0` (the sweep tier), `FUN_0053c6c0`
(the azimuth sincos). Then behaviour and constants: the flag word, the linear elevation and its
non-unit magnitude, `delta` as an acceleration, the two contact tiers and how the struck surface
picks the bounce branch, `RUN_TIME` as a ceiling with a shortened final step, the 15 s / 35 s
watchdogs, the 0.2 restitution and the energy-loss termination. Close with the retired readings, the
way `org/weather.md` retires `DeckCeilingHeight`'s fits.

Then correct — do not duplicate — the other records: `destructibles.md` keeps the destructible's-eye
view and gains pointers; `MotionRuntime`'s class remark is **stripped of its provenance** (dated
narrative, plan tags, Ghidra addresses) per the coding convention `acbb9ba` is clearing the way for,
leaving what and why plus a pointer; `architecture.md` gains its pointer line from the anim runtime.

**Model recommendation.** high — prose future sessions will treat as ground truth, and this plan's
characteristic failure is a half-corrected record that leaves the next reader re-deriving a wrong
answer. This is the item that prevents the next `BL-319`.

**Verify.** Grep the repo for the disproven phrasings and confirm none survives outside an explicit
"this was wrong, here is how it died" context. Confirm `docs/org/objectMotion.md` contains no
decompiler output and no C-like transcription. Confirm every claim is labelled
decoded-from-executable or measured-off-footage. `docs/architecture.md` updated in the same turn,
per the ground rules.

**⚠ Traps.** ⚠ **Do not paste this plan's transcribed launch-direction block into `docs/org/`.** A
pseudocode transcription is right in a plan and against the house style in `org/` — restate it as
behaviour and constants (`dirY = elev/90`, horizontal `1 − |elev|/90`, magnitude 0.707 at 45°). ⚠
Label the provenance split carefully on `BL-022`'s history: the 0.65 was a judged look and the ~0.58
came from a **frame comparison off footage**, while the 0.745–0.81 is **decoded from the
executable** — and this project's standing rule is that a footage-derived measurement never contests
a decode. ⚠ `PT-46` (d) stays recorded as a conflict resolved *by the decode*, with the observation
intact and only its mechanism reattributed — not deleted, and not written as though the user saw
something that was not there. ⚠ Do not delete the wrong readings outright; the disproven-claims
table exists so nobody re-derives them. ⚠ `docs/HISTORY.md` is frozen; the narrative record goes in
commit messages.

## D13 ☐ Item bookkeeping: `BL-319`, `BL-245`, `PT-46` (d), and a fresh ID for the deleted tune

**Goal.** `backlog.md` and `playtest.md` reflect what this plan settled, with no stale caveat left
restating a disproven reading.

**Evidence (confidence: traced).** `BL-319` is answered outright — `RUN_TIME` is a ceiling
everywhere, and both its symptoms have separate, identified causes. `BL-245` closes per Decision 7.
`PT-46` (d) is re-opened per Decision 1: its observation stands, its mechanism attribution does not.
`BL-022` is already closed (`a151289`) and its constant is deleted by D10 — under the never-reuse
rule the follow-up (re-judge the debris arc at the controls now that it is a decode) is a **new ID
from `./New-ItemId.ps1 -Kind BL`**, not a reopen. A2's `impact_force` finding is already minted as
`BL-343` and needs nothing here.

**Approach.** Use `/close-backlog-item` for the closures — it retires the IDs, logs the outcome in
the closing commit and strikes the caveat everywhere it was restated, which is exactly the failure
mode here. Mint new IDs with `./New-ItemId.ps1`, never by scanning the file. Check `BL-022`'s traps,
which other entries cite, and `BL-059`'s cross-references.

**Model recommendation.** medium — mechanical once the outcomes are settled, but it touches the two
files that a duplicate-ID commit hook guards.

**Verify.** The duplicate-ID hook passes on commit. Grep `backlog.md` and `playtest.md` for
`do_intersections`, `no_altitude` and `FlightToLaunchHeight` and confirm every surviving mention
matches the new decode.

**⚠ Traps.** ⚠ Do not renumber or reuse an ID, ever — a stale cross-reference must fail loudly. ⚠
`playtest.md` and `backlog.md` must move together; the consolidated index is what a human at the
controls actually reads. ⚠ Re-opening `PT-46` (d) means writing what to *look for* now, which is the
opposite of what it recorded before — be explicit that the earlier observation was real and only its
cause was misread, or the next session will read it as a contradiction and re-litigate it.
