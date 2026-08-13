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

**The contact tiers** (re-read in full at C6, 2026-08-12; this paragraph is the corrected text).
`FUN_004e9e30` is the default tier: it calls `FUN_004c76e0(worldDB, x, y+step, z, out)`, a
**terrain-grid column query** that floors `(x, z)` into the database's cell and returns that cell's
surface records (0x2c bytes each, height at `+0x14`) for every node flagged `altitude_surface` AND
`intersect_surface`. It then picks the record whose height is nearest the body's y in **absolute**
value, taking the first unconditionally and rejecting only *replacements* more than 10 m **above**
the body — not "the nearest below within 10 m". The caller reads the struck surface's type at
`+0x20` and maps it `1→1, 4→2`, the `default`/`water`/`lava` `BOUNCE_SEQUENCE` branch index. The
landing fires when `y + stepY < height`. `FUN_004c8ec0` is the `DO_INTERSECTIONS` tier, a full
geometry sweep against the same world database.

**The landing response**, from the same re-read, and **not** what C9's Evidence said before it:
the original does not reflect anything. It **replaces the step's Y** — `stepY = |stepY·0.5| +
height − y` while any of `|vx| ≥ 0.1`, `|vz| ≥ 0.1`, `|vy| ≥ 0.5` holds, else `stepY = height − y`
exactly — leaving X and Z untouched, then multiplies all three velocity components by `0.19999999`
**keeping their signs**. The termination test is `accel² > speed²` at contact: if the incoming
speed² still covers the acceleration², the body damps and continues; otherwise the motion ends.
Both halves also run when the watchdog fires, with a null surface record, which is what makes an
untimed body still dispatch its `default` branch.

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

**The flag census, re-derived** (A2, 2026-08-10; corrected at C7, 2026-08-12,
`analysis/object-motion-flags/census.py`). Over 3,066 `ObjectMotion` events in all 8 chapters,
1,640 carry a `gravity` block and every one of them is ballistic: **1,378** / 166 / 88 / 8 across
the four combinations. `do_intersections` reproduces **166** exactly, and `no_altitude`'s 8
(`gunshell`, one per chapter) were never wrong — the "5 chapter files" grep does not reproduce.
⚠ A2 moved the all-false row to 1,363 and called the published 1,378 an arithmetic slip; that was
backwards. Its scan walked `sequences` only and missed the **15 `ObjectMotion` events in
`unknown_seq`** — the compiled destruction slot (`AnimDefinition.DeathSlot`) that a real kill
dispatches, per `BL-276`. All 15 are ballistic with an all-false gravity block. So the
default-combination bodies that should be landing and are not are 1,378 + 88 = **1,466**,
alongside the 120 bounce-shape and 167 vanish-shape launches currently
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

4. ☑ `translation_range` elevation is linear (`elev/90`), not spherical
5. ☑ `translation.delta` is an acceleration, not a ramp divided by `run_time`

### Wave C — contact and termination

6. ☑ The default contact tier: the ground-column query
7. ☑ `NO_ALTITUDE` as the opt-out, and `gunshell` as its only author
8. ☑ `RUN_TIME` as a universal ceiling; retire `FlightToLaunchHeight` for the watchdog
9. ☑ Landing response: 0.2 restitution and energy-loss termination
10. ☑ `FORWARD_ROTATION`: the tumble is a rate about the launch's own perpendicular, not an angle
    over `run_time` about local X

⚠ `C10` was minted at `C9` (2026-08-13) from a user report at the controls, not from the plan's
original scope: the rotation reads far larger than the original's. It is a decode of the same
function and it must land before `D12` writes the record, since `D12` would otherwise document a
tumble that is about to change.

### Wave D — the tune, the coverage, the record

10. ☑ Delete `DebrisTune` entirely
11. ☑ Pin a golden that shows debris coming to rest
12. ☑ Land `docs/org/objectMotion.md`, and correct the records that carried the disproven readings
13. ☑ Item bookkeeping: `BL-319`, `BL-245`, `PT-46` (d), and a fresh ID for the deleted tune

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
there is nothing to teach it. The census reproduces 166 and 8. ⚠ Its all-false row of **1,363** was
withdrawn at C7: the scan missed `unknown_seq`, so the figure is **1,378** and A3/C6's population is
**1,466**. `impact_force` filed as `BL-343`. ⚠ For A3: every
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

## B4 ☑ `translation_range` elevation is linear (`elev/90`), not spherical

**Landed 2026-08-11.** The trace was re-read off `FUN_004e8fa0`'s `flags & 8` block and confirmed
verbatim: `fVar1 = elev * 0.011111111` cached at `+0x74`, `fVar6 = fVar1 < 0 ? fVar1 + 1.0 : 1.0 -
fVar1` giving the horizontal at `+0x70`/`+0x78`, and only the azimuth taking the `* 0.017453292` and
the `FUN_0053c6c0` sincos. `RangeLaunchDirection` is now that expression and nothing else was
touched; the azimuth's cos-on-X / sin-on-Z assignment stays inherited, as instructed.

**Measured at the controls** (`--freecam --chapter=C1 --destroy=m_build03 --debris-launch=1`, so the
authored arc rather than the 0.65 tune, A/B'd against a temporary rebuild of the old expression).
At the 5.0 s `RUN_TIME` ceiling the pieces sit **21–24 m lower** and are out of the sky rather than
hanging in it — `part9` 167.8 m against the old 191.7, `part5` 177.3 against 198.5, `part8` 169.0
against 185.1. That is the reported symptom's cause, removed.

**⚠ The golden that did NOT move, explained by measurement rather than assumption.** `c1-crash`
moved as expected and is re-pinned. `c1-destroy-effects` did **not**, which A1 and this item's Verify
both call a red flag — and the cause is not blindness in the shot but its amplitude. That shot's only
`translation_range` body is `ap_radiotwr`'s `upper` (elevation 30–70°, speed 2.5–4.5, gravity −2,
starting 0.2 s in), and a `--debris-launch` sweep on the shot's own args gives it a **sensitivity
floor between 0.85× and 2×**: 0.5× and 0.85× both reproduce `5c8a15f7…` exactly, while 2× and 10×
move it. B4's magnitude ratio is 0.745–0.81, i.e. inside the band the shot cannot resolve — the piece
is still inside the fireball column that dominates this framing at frame 120. So the shot covers a
`translation_range` launch, as A1 said, but not one at this amplitude.

**Also worth knowing, because it re-attributes the moving golden.** `c1-crash`'s movement is *not*
`player_crash_dirt`'s four pieces — all eight of its motions are the **vector** `translation` form and
B4 cannot touch them. It moved through `carnage_trails-call_crash_trails`' five `fly_trailN`, the
starburst the class remark already cites, which are the only `translation_range` bodies in that shot.

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

## B5 ☑ `translation.delta` is an acceleration, not a ramp divided by `run_time`

**Landed 2026-08-11.** The division is gone and `rampTotal` is folded straight into `m._accel` at
the point each branch (`translation`/`translation_range`) computes it — the deferred-fold ordering
the old code needed (`rampTotal` collected early, applied once `rtSafe` was settled by the flight
solve) fell out cleanly, since the new fold no longer depends on `rtSafe` at all. `FlightToLaunchHeight`
now solves against the delta-inclusive `m._accel.Y`, which is more consistent than before (the old
code fed it gravity-only accel while the ballistic-origin formula it was approximating already used
the full accel) — no reachable non-`v0y<=0` case is affected, since every `translation_range` event
with a non-zero `delta` authors a `run_time` and never reaches that solve.

**Measured at the controls** (`--chapter=C4 --play-anim=blow_zdome --debug-anim --det --mute`,
`zdome`'s `zdtop1`, `delta` a constant 5 on the launch direction, `run_time` 1.5 s — one of the
non-zero-`delta` `translation_range` events install-wide). A one-line, restore-after A/B (dividing by
the def's own 1.5 s `run_time`, matching the old formula exactly, then reverting): old formula lands
`zdtop1` at y=1341.4 at the ~1 s debug tick, the new formula at y=1341.8 — a +0.4 m difference against
a `0.5·Δaccel·t²` prediction of ~0.33 m, the right size and the right sign for a 5× larger
acceleration contribution over one second. `c1-crash`'s golden hash moves for the same reason:
`flydirt`'s (vector-form) `translation.delta.y = -3.0` over a 5.0 s `run_time` now contributes
-3.0 m/s² instead of -0.6 — the exact "five times as much" the item's own trap warned of.

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

## C6 ☑ The default contact tier: the ground-column query

**Landed 2026-08-12.** `MotionRuntime` now selects a tier once, from the gravity block, in the
original's own branch order, and `TryGroundColumn` sits beside `TryContact` as a second named
mechanism. Both end on one shared `Land`. `MotionSet` tallies the two tiers apart, and the
`--debug-anim` line prints the split. The settle-hop sweep inheritance is deleted rather than kept
alongside: a `pNhit` follow-up authors a gravity block, so it now selects the column like every
other unflagged body, which is what that judged divergence was standing in for.

**Two corrections to this item's own Evidence, from re-reading the trace before implementing.**
Neither changes the mechanism, both change what a later reader would build.

1. `FUN_004e9e30` does **not** pick "the nearest surface below within 10 m". It picks the cell
   record whose height is nearest the body's y in **absolute** value, takes the first record
   unconditionally whatever its height, and applies the 10 m only as a filter on *replacements*
   more than 10 m **above** the body. A surface above can therefore win, and `y + stepY < height`
   then lifts the body onto it. The implementation casts downward and takes the first surface
   under the body, which is what that pick degenerates to whenever the cell holds one ground
   surface, and which cannot lift a piece onto a ceiling it was flying beneath. Recorded as a
   deliberate departure in `TryGroundColumn`.
2. `FUN_004c76e0` is a **terrain-grid** lookup, not a general collision query: it floors `(x, z)`
   into the world database's cell and walks that cell's nodes, admitting only those whose flags
   carry `altitude_surface` **and** `intersect_surface` (`& 4` and `& 8`). The engine builds
   colliders from `intersect_surface` alone, so reusing the collider set diverges by exactly the
   28 nodes install-wide carrying `intersect_surface` without `altitude_surface`: 14 in C1, 0 in
   C1B/C1C/C2B, 2–4 elsewhere, every one a destructible's own sub-part (`healthy`, `dest_base`,
   `front`/`rear`, `prhit`) rather than terrain or a building shell. Measured, not assumed, so no
   second surface pipeline was built. ⚠ Note what this kills: `altitude_surface` is **not** a
   terrain-only bit — 50,927 of 53,303 nodes carry it, buildings included — so filtering the
   column by it does *not* keep debris off rooftops, and any future item reaching for it on that
   reasoning is reaching for the wrong thing.

**Also settled, so C7/C8/C9 do not re-derive it.** A3's descending-step admission and its `COMPLEX`
widening are transcribed but **behaviourally inert given a downward column**: a step that ends
higher than it starts cannot end below a surface the ray found at or under its start, so both only
save the query. They are load-bearing in the original because its cell query can return a surface
above the body and because its step sign is the parent-frame one, which `COMPLEX` makes
meaningless; this engine takes the sign in the world, where it is the true answer for both forms.
The suite records this rather than asserting it, since an assertion would claim coverage that does
not exist.

**Measured.** `--freecam --chapter=C1 --collision --destroy=<def> --debug-anim --det --mute`:
`m_build03` 9 launches, **7 by column, 2 on the clock**; `pass_plane01` (the C1 airfield plane)
4 launches, **2 by column, 2 on the clock**, A/B'd against the same shot without `--collision`,
where the landing and its ground-level fireball are absent. Both remainders are the no-`RUN_TIME`
shape that `FlightToLaunchHeight` still ends at **launch height**, above the ground, which is C8's
item and not a contact failure. Scale and cost: `--destroy=m_build` kills seven buildings at once
for **63 simultaneous launches, 42 landed by column**, at `physics_ms=0.02` and 101–115 fps, so
the per-body query is free at the only scale the install can produce.

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

**Model recommendation.** max — the widest behaviour change in the plan (~1,466 events plus every
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

## C7 ☑ `NO_ALTITUDE` as the opt-out, and `gunshell` as its only author

**Landed 2026-08-13, and it lands no new engine code.** C6's tier selection already reads the flag
in the original's branch order, so the veto shipped with it; what C7 owed was proof that the field
survives extraction and reaches `Create`, which the item itself flagged as unchecked. It does, with
no plumbing: `AnimData` is a property bag over the parsed JSON and `CompiledAnim` maps JSON
true/false to `bool`, so `gravityBlock.Bool("no_altitude")` reads the extracted value directly.

**The census, re-derived independently.** `no_altitude: true` appears on `gunshell` alone, **8
events, one per chapter**, every one `translation_range` form with gravity −3.0, `RUN_TIME` 2.0,
`complex` false, `do_intersections` false and no bounce. A2's figure confirmed rather than replaced.
⚠ The same run found A2's all-false row wrong in the other direction, and that is corrected
throughout: its scan walked `sequences` only and missed the 15 `ObjectMotion` events in
`unknown_seq`, the compiled destruction slot the runtime dispatches (`BL-276`). The row is **1,378**,
not 1,363, so C6's population is **1,466**; `census.py` now walks both blocks and `do_intersections`
still reproduces 166 exactly.

**⚠ The finding that matters for anyone reading this later: in this engine the casing never becomes
a `MotionRuntime` at all.** `ProjectilePool` reads the `gunshell` event's fields into its own
`CasingSpec` and integrates them itself (`Projectile.cs`, `CasingSpecResolve`/`CasingSlot`), so
neither tier can reach an ejected shell whatever the flag says. The veto is live only on the path
that plays the def through `AnimRuntime`, which `muzzle_burst`'s `muzzleburst_effects` does
`CallAnimation` (8 sites, one per chapter). So this item is correctness insurance for that path
rather than a behaviour change, and the "casings must not start resting on the ground" risk it was
written against could not have materialised.

**Verified.** `ground-contact` gained a case that builds the motion from the **real extracted
gunshell event** out of the chapter's own anim program, with a mask wired, and asserts it selects
no tier. Shown able to fail: replacing the veto with `else if (true)` turns exactly that check and
the hand-built `no_altitude` case red (`tier=Column` both), and nothing else, which is also what
proves the flag arrives from the data rather than from the suite's own dictionary. `.\RunTests.ps1`
949/949 units, 37/37 suites, 13/13 goldens hash-identical. At the controls
(`--chapter=C1 --plane=player_bhawk --hold=0.2,0.1,0,1 --fire --infinite-ammo --debug-anim`):
casings eject and tumble as before, 12 live simultaneously, and the run reports **0 ballistic
launches**, which is the direct measurement of the paragraph above.

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

## C8 ☑ `RUN_TIME` as a universal ceiling; retire `FlightToLaunchHeight` for the watchdog

**Landed 2026-08-13.** The item's riskiest interaction is resolved by splitting one number into two.
`_runTime` stays exactly what it was — the authored `RUN_TIME` or the parabola's return to launch
height — and keeps its three existing jobs: the duration the sequence waits on (so `BL-257`'s
vanish-shape pieces hide when they always did), the tumble rate (an angle divided by exactly that,
on 282 of the 296 untimed events), and the channel parameter. `_ceiling` is new and terminates the
body alone: the authored `RUN_TIME`, or an open clock bounded by the watchdog, or — with no contact
tier behind it — `_runTime` again, which keeps every collider-less session untouched.

The watchdog is charged the way the original charges it, not as a flight timer: `+= dt` only on a
step whose contact query RAN and came back EMPTY, so a body descending toward ground it can see
never accumulates a tick, and a climbing body is not even queried. 15 s on the column, 35 s on the
sweep. An open ceiling cannot hang a body forever here, and the reason is the data rather than a
cap: all 296 untimed ballistic events author a gravity block, so every one of them descends.

**Two model corrections that fell out of building it.** (1) A watchdog end freezes the body exactly
as a landing does, so `LandedByContact` was reporting one as the other and `MotionSet` was tallying
a body that found NOTHING as a contact landing — precisely the reading the item's Verify depends
on. Contact and clock are now separate flags. (2) The bounce is armed at contact, or on a watchdog
end from a null surface (which indexes to `default`, as the original does); the create-time arming
survives only on the no-tier fallback, where nothing will ever reach a surface.

**Measured at the controls** (`--freecam --chapter=C1 --collision --destroy=<def> --debug-anim`).
`m_build03` **9 of 9** bodies now end by contact, against 7 of 9 before; `pass_plane01` **4 of 4**,
against 2 of 4. In both cases the remainder was the untimed shape ending at launch height, which is
what this item removed. C3's no-apex oddities behave: `bridge_truck01` launches 12 with 6 landed and
none on the clock, `susp_bridge` 5 with 4 landed and none on the clock. **No body anywhere ended on
the watchdog**, which is the backstop behaving as one. The no-mask capture of `pass_plane01` at
frame 420 is **byte-identical** to the pre-C8 one (`DE5751AF…`), so the collider-less fallback is
untouched.

**⚠ Open, and NOT this item's to fix: the pieces rest too deep in the ground** (user, at the
controls, 2026-08-13). The mechanism is faithful and was re-read to confirm it — `FUN_004cf200`
hands the column the node's ORIGIN, `FUN_0055bbc0` fills the surface record's height from the
struck polygon, and there is no bounding-box or extent term anywhere in the chain, so the original
lands the origin on the polygon exactly as this does (the suite measures the resting origin at
0.00 m from the struck surface). What sinks a piece is therefore how far its own geometry hangs
below its node origin, which nothing in the decode compensates for. **C9 owns the resting pose** —
its half-step-above rule and its exact-rest rule are the only remaining places the original decides
where a landed body sits — so the question belongs there, and it must be answered by a decode or a
filed item rather than by an offset (the milestone boundary).

⚠ Also worth not re-deriving: `FUN_004ccf50`/`FUN_004ccf00` around the column query get and clear
the flying piece's OWN `intersect_surface` bit, restoring it afterwards. That is the original's
self-hit guard, and it is the mechanism our sweep's `ArmDistance`/`ArmSeconds` epsilon stands in
for.

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

## C9 ☑ Landing response: 0.2 restitution and energy-loss termination

**Landed 2026-08-13**, transcribed from the corrected reading above rather than from this item's
original Evidence. A contact holds a moving body half its descending step clear of the surface and
rests a slow one exactly on it (thresholds per axis: 0.1 on X, 0.1 on Z, 0.5 on Y); velocity is
scaled by 0.2 with **every sign kept**; and the body survives the contact while its incoming speed²
still covers its acceleration², re-basing the launch at the corrected pose. Both tiers share it,
since they differ in what they ask the world and not in what they do with the answer. A defensive
contact cap exists and is never reached: each contact takes four fifths of the speed, so a piece
striking at 20 m/s under Earth gravity damps to 4 and ends on its next contact.

**⚠ It does not answer the sinking, and the measurement says why.** The suite's resting body moved
from **0.00 m to 0.06 m** above the struck surface, which is the entire lift this mechanism has:
half a descending step is centimetres once the speed has been damped. The user's report stands
(`the parts should not sink into the ground that much`; and, of the original, `lands not perfectly
above ground but not as much sunken as it is now`), so a gap remains and it is not in the contact
model. That chain is now read end to end and carries no extent term anywhere: `FUN_004cf200` hands
the query the node's ORIGIN, `FUN_0055bbc0` walks the struck mesh's polygons and `FUN_0055d5c0`
returns the polygon's height at `(x, z)`, and the database itself only admits surfaces at or below
the query point. What remains is how far a piece's own geometry hangs below its node origin.
**Left open for the milestone's look pass, and it must be answered by a decode or a filed item
rather than by an offset.**

**⚠ A decode this item did NOT own, found in the same function and worth its own item.** The tumble
is wrong in both axis and rate, and it is the other half of what the user is seeing. This engine
reads `forward_rotation.Time.initial` as a TOTAL angle, divides it by the run time and spins about
the node's local X — a reasoned choice, never a decode. `FUN_004e8fa0`'s `0x80` branch instead
treats `+0x84` as a live RATE (rad/s), seeded from `Time.initial` and integrated by
`+0x84 += dt · Time.delta`, and applies it per frame as the euler triple
`(dirZ · rate · dt, 0, −dirX · rate · dt)` — i.e. about the horizontal axis **perpendicular to the
launch direction**, scaled by the launch direction's own horizontal magnitude `h = 1 − |elev|/90`.
So a steep launch tumbles slowly and a flat one fast, off the same authored number, and the axis
follows the throw instead of the mesh. The `0x40` (`DISTANCE`) branch is the same shape driven by
the step rather than by `dt`.

**Goal.** A landing piece bounces the way the original bounces — velocity scaled by 0.2 — and the
body ends when a bounce stops losing energy rather than after a fixed count.

**Evidence (confidence: traced — ⚠ this paragraph was WRONG until C6 re-read the function; see "The
landing response" above for the full corrected text, and do not reinstate the reflection).** The
original **replaces the step's Y component** rather than reflecting anything: `stepY = |stepY·0.5|
+ height − y` while any of `|vx| ≥ 0.1`, `|vz| ≥ 0.1`, `|vy| ≥ 0.5` holds, and `stepY = height − y`
exactly once all three have fallen below, which is what rests a slow piece on the surface. X and Z
are left alone, so the body keeps its horizontal travel through the contact frame. Velocity is then
scaled by `0.19999999` on all three components **with their signs kept**. The body ends when
`accel² > speed²` at contact and damps-and-continues otherwise, so with Earth gravity a piece
striking at 20 m/s damps to 4 m/s and ends on its next contact: one or two hops, not a count. The
`BOUNCES` token exists in the binary (`0063d1f4`) and may cap this independently; worth a look, not
required.

**Approach.** Implement inside the landing path shared by both tiers, so a column landing and a
sweep landing respond identically — the response is not what the two tiers differ in.

**Model recommendation.** high — small numerically, but it decides whether debris reads as settling
or as jittering, and a wrong termination test loops forever.

**Verify.** Watch a single piece at the controls through its whole landing; it should hop once or
twice and stop. A piece that never stops means the energy comparison is inverted — bound the
iteration defensively even so.

**⚠ Traps.** ⚠ `0.19999999` is `0.2` in float; do not transcribe the artefact. ⚠ The two speed
thresholds (0.1 horizontal, 0.5 vertical) are asymmetric on purpose — do not tidy them into one, and
note the horizontal one is tested per axis (`|vx|`, `|vz|`), not on their magnitude. ⚠ A damped body
whose velocity keeps its downward sign does not hop off the surface; what lifts it clear is the
half-step in the POSE, and reading that as "the response launches it upward" is how the previous
attempt at this wave ended up with debris hovering. ⚠
The bounce *branch* (`default`/`water`/`lava`) comes from the struck surface and is already wired;
this item is the physical response only, and `lava` remains dead data across the install.

## C10 ☑ `FORWARD_ROTATION`: the tumble's axis and its rate

**Landed 2026-08-13.** `Time.initial` is a rate in rad/s and `delta` its acceleration, both read
across verbatim; the run time is gone from the derivation. The axis is the launch direction's own
horizontal perpendicular `(dirZ, 0, −dirX)`, unnormalised, so a launch's `h = 1 − |elev|/90` scales
its own tumble — `TumbleAxis`, shared with the casing ejection the way `RangeLaunchDirection`
already was. It composes as an EULER triple on the node's angles, which is what the original
accumulates (`FUN_004d25c0`/`FUN_004d1ba0` write `node+0x18..0x20`), not as a turn about a live
basis axis.

**The finding that answers the report.** A body launched by the VECTOR `translation` form does not
tumble at all. The cache the tumble multiplies through is filled only by the `translation_range`
branch, and the parser zeroes the whole 0x14c-byte event struct before parsing (`REP STOSD` at
`005085e0`), so the multiply is by zero. That is **495 of the install's 1,399 tumbles**, including
all four `player_crash_dirt` pieces — the biggest authored numbers in the install (15.708 rad/s,
which the old reading turned into a 2.6 rad/s spin). ⚠ `CAP-16`'s wing-panel strip measured
20–30 °/s against the ÷`run_time` reading's ~150 °/s and was recorded as confirming a *total angle*;
it fits "no tumble at all" better than either reading. Noted as agreement only — no measurement off
that footage decides this (`docs/verification.md`, and the `CAP-16` rule).

**What moves, honestly.** For a ranged launch the new rate is `initial · h` against the old
`initial / run_time`, so the ratio is `h · run_time` and it can go either way: `pass_plane01`'s
part1 (6.98 rad/s authored, 60–70°) goes 1.40 → 1.55–2.33 rad/s, its part3 (80–85°, untimed)
drops to 0.03 rad/s, and the crash pieces go to zero. The tumble reading larger than the original's
is answered by the vector-form finding, not by everything getting slower.

**Verified.** 949/949 units, 38/38 engine suites with engine errors clean (a new `forward-rotation`
suite pins the axis geometrically, the rate against two different run times, `h`'s scaling at 60°,
`delta`'s integration, and the vector form's zero), 12 of 13 goldens unchanged. `c1-crash` re-pinned
`b80b8a98…` → `d15e38ed…`, image inspected, `exercises` rewritten. `--destroy=pass_plane01` still
lands 4 of 4 by column with no engine errors.

**Not built.** `FORWARD_ROTATION DISTANCE` (flag `0x40`): all 1,399 tumbles in the install author
`Time` and none authors `Distance`, so the branch is documented and left unwritten.

**Goal.** A tumbling piece spins at the rate the data authors, about the axis the original spins it
about — so a steep launch tumbles slowly and a flat one fast, off the same authored number, and the
axis follows the throw rather than the mesh.

**Evidence (confidence: traced — read at C9, 2026-08-13, and not yet built).** This engine reads
`forward_rotation.Time.initial` as a TOTAL angle, divides it by the run time, and rotates about the
node's local X (`MotionRuntime`'s `_tumbleRate`, and its class remark has always called the axis a
reasoned choice rather than a decode). `FUN_004e8fa0` does neither. Its `0x80` (`TIME`) branch holds
a live RATE at `+0x84`, seeded from `Time.initial` in rad/s and integrated each frame by
`+0x84 += dt · Time.delta` (`+0x80`), and applies the euler triple

```
( +0x78 · rate · dt ,  0 ,  −( +0x70 · rate · dt ) )
```

where `+0x70`/`+0x78` are the launch direction's cached X and Z (`cos(az)·h` and `sin(az)·h`, from
the same `TRANSLATION_RANGE` block B4 decoded). That is a rotation about the horizontal axis
**perpendicular to the launch direction**, scaled by the direction's own horizontal magnitude
`h = 1 − |elev|/90`. The `0x40` (`DISTANCE`) branch is the same shape driven by the step rather than
by `dt`, so it is a rotation per metre travelled rather than per second.

**Approach.** Replace `_tumbleRate`'s derivation and its axis together — they are one reading, and
changing only the rate would leave the spin about a mesh axis it was never about. Carry
`Time.delta` as the rate's own integrator rather than dropping it. Check what `DISTANCE`'s
population is before deciding whether to build both branches or file the second.

**Model recommendation.** high — small diff, wide blast radius (282 of the 296 untimed events carry
a tumble, and the crash pieces carry the biggest ones), and it will move `c1-crash`.

**Verify.** The user's own report is the acceptance test: the rotation must stop reading larger than
the original's. Pin the arithmetic first — a steep launch and a flat one off the same authored
number must differ — then look at `--destroy=pass_plane01` and the crash, and expect `c1-crash` to
move.

**⚠ Traps.** ⚠ `Time.initial` is a RATE here, not the total angle the class remark claims; the "5π
and 4.44π are clean multiples of π" reasoning that produced the total-angle reading is a
coincidence of the authored numbers and must not be used to argue the decode back. ⚠ Do not
normalise the axis: its length is `h`, and that is what makes a steep throw tumble slowly. ⚠ This
lands before `D12`, which would otherwise write the old reading into `docs/org/`.

---

# Wave D — the tune, the coverage, the record

## D10 ☑ Delete `DebrisTune` entirely

**Landed 2026-08-13.** Gone in full, per Decision 5: the class and its file, `LaunchScale` and
`GravityScale` both, `Launcher.ApplyDebrisTune` and its call site, `SessionSpec`'s two properties
and their argument parsing, the `--debris-launch`/`--debris-gravity` flags and their `cli.md` entry,
the `debris.launchScale`/`debris.gravityScale` config keys (which the removed `Config.GetFloat`
reads also removes from `--dump-config`), the `WorldDamageLab` panel with its two sliders, three
buttons, readout and sync, and `Suites.cs`'s `AuthoredArcScope` with both of its uses. The launch
suites need no pin now: the authored arc IS what flies, so their bands apply directly.

**The golden that moved, and it is the one that should.** `c1-crash` re-pinned
(`440c61cb…` → `b80b8a98…`): `call_crash_trails`' five `fly_trailN` now launch at the authored speed
instead of 0.65 of it. The other twelve are unchanged, which is what a knob that only ever touched
launched `OBJECT_MOTION` bodies should do. 949/949 units, 37/37 suites, engine errors clean.

**⚠ The first honest look, and it is not signed off.** With `B4`'s elevation decode and no tune, the
arc is finally the data's own. The two open look questions belong here rather than to any item:
debris resting too deep (`C9`, decoded end to end, no extent term anywhere in the original's chain)
and the tumble reading far larger than the original's (`C10`, now its own item). Neither is to be
answered with a scalar; that is the whole reason this item exists.

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

## D11 ☑ Pin a golden that shows debris coming to rest

**Landed 2026-08-13.** `c1-debris-rest`: `--freecam --chapter=C1 --collision --destroy=m_build03`,
camera hand-placed on the one piece (`part4`) that lands inside its own 5.0 s `RUN_TIME` ceiling,
frame 360 (6.0 s) — late enough for the landing's own bounce/spark puffer (`sparkout4`) to have
fired and be fading, early enough that it still marks the pixels. `A1`'s own three Fly-mode goldens
were re-checked first and confirmed still short: `c1-destroy-effects`'s piece is structurally
untestable (`do_intersections: false`) and `c1-crash`'s pieces launch too late in a 0.333 s window
to reach the ground — neither closes on its own. `m_build03`'s other eight pieces do NOT reliably
land inside their own ceiling under `--det`'s seed (only `part4` does; the rest end on the clock,
one — `part3` — still free-falling past the terrain grid's column at frame 900 with no matching
surface under it, the "a fast piece can pass over a ledge" case C6's Evidence already named) — so
the shot is framed on the one piece that does, not on the building's auto-framed full bounds, which
stays hash-identical with contact on or off (checked and rejected: the flying pieces that would
move it all land outside that framing).

**Shown able to fail, locally, per this item's own acceptance bar.** `GameSession.cs`'s
`if (BuildsCollision)` gate on `session.Runtime.ContactMask` was flipped to `if (false && ...)`,
rebuilt, and the identical probe re-run: `pixmd5` moved from `b77edef2…` (contact) to `5e3e023c…`
(no contact) at frame 360 — the landing spark puffer is the discriminator, and it is gone with the
tier off. The same A/B at frame 900 (well past the puffer's fade) came back hash-IDENTICAL both
ways, which is why frame 360 is pinned and not a later, cleaner-looking one: a golden that cannot
fail is not coverage, and a frame chosen for looks alone would have been exactly that. The edit was
reverted before pinning; `git diff` on `GameSession.cs` is empty in this commit.

**Verified.** `.\RunTests.ps1`: build clean, 949/949 units, 38/38 engine suites, 14/14 goldens
hash-identical including the new shot; `c1-debris-rest`'s own probe log shows
`'part4' landed at (-6104.4863, 158.73997, -4279.1753) — bounce sequence 'sparkout4'` and
`contact-tested bodies ended: 1 by contact, 4 on their run time (column 1/4, sweep 0/0)` at the
pinned frame — `ContactLandings` non-zero, as required.

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

## D12 ☑ Land `docs/org/objectMotion.md`, and correct the records that carried the disproven readings

**Landed 2026-08-13.** `docs/org/objectMotion.md` is the decode's home, in the `puffer.md` house
style: function map, the flag word with its parser addresses, the live slots, then behaviour and
constants — the linear elevation and its non-unit magnitude, `delta` as an acceleration, gravity's
two forms, both contact tiers with the column's exact surface pick, the landing response, the
termination model and both watchdogs, the tumble and its axis. It closes with the divergence table
and **eight retired readings**, each with its cause of death: the spherical elevation, `DebrisTune`,
the total-angle tumble, the ÷`run_time` `delta`, "`do_intersections: false` means no test",
"`no_altitude` is about spawn altitude", `PT-46` (d)'s mechanism (observation intact, attribution
reversed), and "every golden builds no colliders".

**Six records corrected rather than duplicated**, each keeping its own view and gaining a pointer:
`formats/destructibles.md` (the tune bullet, the ground-rest split, the `no_altitude` paragraph, the
`BL-245` deferral, the Wave C status line, the `sin(elevation)` vertical speed);
`formats/anim-definitions.md` (the two `delta` readings, "spherical form", the `DO_INTERSECTIONS`
follow-up framing, and the `FORWARD_ROTATION` bullet's inline addresses, which belong in `org/` now
that it exists); `formats/README.md`'s `org/` index; `architecture.md`'s anim entry, which sheds the
decode narrative it was carrying and keeps the engine-side constraints (⚠ count 6 → 5);
`analysis/object-motion-ground-rest/FINDINGS.md` (a banner retiring its interpretation, with its
tables left standing); `analysis/bl-257-nulled-launch/FINDINGS.md` + its `census.py`;
`analysis/object-motion-range/FINDINGS.md` (title and a two-line correction note);
`analysis/object-motion-goldens/FINDINGS.md`; and `analysis/object-motion-flags/FINDINGS.md`'s open
A3 question, now answered. `MotionRuntime`'s class remark is stripped of its provenance — install
counts, dates, plan tags, Ghidra offsets — down to what each channel is, why, and the pointer.

**Verified.** Every install-wide number on the new page re-derived from `extracted/` rather than
carried across: the four-way flag census (1,378 / 166 / 88 / 8, so 1,466 on the column), 3,066
events / 1,983 ballistic / 1,640 gravity-bearing, 1,399 tumbles with 495 on the vector form and
**zero** authoring `DISTANCE`, `delta` non-zero on 233 of 1,226 range and 92 of 757 vector events,
296 untimed ballistic events **all** carrying gravity, 324 bounce blocks naming `default` and 104
`water` with **no** lava branch, 182 `impact_force`, `gunshell` as the sole `no_altitude` carrier at
8 events. `m_build03` part1's block re-read at source (azimuth 35–55°, elevation 60–70°, speed
28–37, gravity −10, `RUN_TIME` 5.0), and the 0.745–0.81 magnitude band recomputed from it.
`bl-257-nulled-launch/census.py`'s `sin(elev)` → `elev/90` fix re-run: the 159-up / 8-no-apex split
and all eight named defs reproduce exactly, which is what makes the edit a correction rather than a
new finding. `.\RunTests.ps1`: build clean, 949/949 units, 38/38 engine suites with engine errors
clean, **14/14 goldens hash-identical** — a documentation item must move nothing, and it moved
nothing. Repo grep for the disproven phrasings leaves them only inside "this was wrong, here is
how it died" contexts, plus `backlog.md`, which is **D13's** file. Five live restatements are
waiting there, listed so D13 does not have to re-find them: `:85` ("the spherical one"), `:105` and
`:157` (`DebrisTune.LaunchScale` 0.65 as a settled judged look), `:2108` (`sin(elevation)·speed`)
and `:2117` (`BL-245`'s "the original was not collision-testing them", with `PT-46` (d) cited as
confirmation).

⚠ **Not done here, deliberately:** the Ghidra addresses in `MotionRuntime`'s *member* comments. The
convention's code-comment cleanup is what `acbb9ba` was groundwork for and it is wider than this
plan; the class remark is what D12 scoped.

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

## D13 ☑ Item bookkeeping: `BL-319`, `BL-245`, `PT-46` (d), and a fresh ID for the deleted tune

**Landed 2026-08-13.** `BL-319` deleted as answered — `RUN_TIME` is a ceiling everywhere, `m_build03`'s
cut arc traced to the linear-elevation decode (B4) and `genx12`'s underground landing to the missing
default contact tier (C6), both settled. `BL-245` deleted per Decision 7 — `do_intersections: false`
selects the cheap column tier rather than opting a body out, so its 379 falls simply land under C6/C7
with no divergence decision left to make. `PT-46` (d) re-opened in `playtest.md` against a C1
Devastator destruction sortie: the observation (pieces pass through terrain or vanish) stands, and the
new check asks the same question against the corrected mechanism (default column test,
`NO_ALTITUDE`'s opt-out, `DO_INTERSECTIONS`'s sweep) instead of the disproven "no test at all" reading.
`./New-ItemId.ps1 -Kind BL` minted `BL-346` for the at-controls debris-arc judgement left behind by
D10's deletion of `DebrisTune.LaunchScale`; the two other backlog entries that still cited the deleted
0.65 constant (`BL-060`, `BL-122`) were rewritten to point at the executable decode and at `BL-346`
instead.

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
