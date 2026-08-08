# Ground contact — the `do_intersections` query behind `BL-059` item 1

**ACTIVE PLAN** (written 2026-08-08). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

This plan implements the one thing left open in `BL-059` item 1: the **166 `ObjectMotion` events
that author `do_intersections: true`** must end their flight where the world says it ends, instead
of running their authored `RUN_TIME` out and finishing wherever the parabola left them — routinely
below the terrain. The mechanism is a **swept segment ray** along the body's own trajectory, and on
contact the body stops and dispatches its `BOUNCE_SEQUENCE`, with the branch chosen from the struck
collider's surface. Re-verified still-open against the code on 2026-08-08: `MotionRuntime.cs:242-255`
solves a landing only for launches that carry *no* `RUN_TIME`, and `MotionRuntime.cs:115-118` records
the `run_time`+bounce family — which is 150 of the 166 — as deliberately left alone.

**Out of scope, deliberately.** `BL-245`'s 379 falls (they author `do_intersections: false`, so this
plan's rule does not reach them — see Decision 7); the 120 bounce-shape launches `BL-240` already
solves by return-to-launch-height (also `false`, and confirmed to match the original); `BL-059`
item 2's air variant, which is a missing *trigger*, not a missing query. The `bounce_sequence`
dispatch itself and the `SOUND_GROUPS` resolver are **already landed** and are not re-done here.

## Milestone goal

- A `do_intersections: true` body stops at the first real collider its trajectory meets — terrain,
  rooftop or wall — and comes to rest there.
- Its `BOUNCE_SEQUENCE` fires at that contact point, on the branch the struck surface selects
  (`water` where the collider is water and the branch is live, `default` otherwise).
- A session with no collision world behaves exactly as it does today, byte-for-byte.
- The cockpit check that has failed since the crash rig landed — `player_crash_dirt`'s `piece1`–`4`
  hanging above the ground — passes.

**This plan implements what the data asks for; it does not decide whether to override the data.**
Every event that authors `do_intersections: false` keeps its current behaviour, including the ones
that visibly sink through terrain — `PT-46` (d) confirmed the original does the same. Changing that
is `BL-245`'s argument to have, not this plan's.

## Decisions (2026-08-08)

Settled in a grilling session with the author. This table is the authority where the prose below
contradicts itself.

| # | Question | Decision |
|---|---|---|
| 1 | Down-ray, swept segment ray, or shape cast? | **Swept segment ray**, last origin → this origin, per tick, masked to `CollisionLayers.World` — copying `ProjectilePool`'s reused `PhysicsRayQueryParameters3D` (`Projectile.cs:325`). No down-ray first cut: the plumbing is the cost, and the segment gets roofs and walls for the same price |
| 2 | What happens with no collision world? | **Fall back to today's launch-height path**, gated on `Target.GetWorld3D()?.DirectSpaceState` being non-null — *not* on `SessionSpec.BuildsCollision` (`SessionSpec.cs:183`), so `MotionRuntime` takes no dependency on the spec. Golden captures build no colliders and stay byte-identical. A contact/fallback counter keeps the split honest |
| 3 | Which population gets the query? | **`do_intersections: true` only** — 166 events. The 120 bounce-shape and 167 vanish-shape all author `false` and already match the original at the controls |
| 4 | What does contact do to the motion? | **Truncate.** `RUN_TIME` becomes a ceiling; contact ends the body at the hit point and arms `PendingBounce`, so `MotionSet.Tick`'s existing dispatch (`MotionSet.cs:72-76`) is reused unchanged. A true-flagged event with no bounce simply rests |
| 5 | How is a frame-one self-hit avoided? | **A launch-relative epsilon** — no contact until ~2 m or ~0.1 s from launch, whichever first, plus Godot's default `hit_from_inside: false`. **Not** apex-based: the eleven airframes and `agyrobus` never rise. The epsilon is a named TUNE constant, refined by playtest |
| 6 | Select the bounce branch by surface now, or always `default`? | **Select now.** `Water` → `water` when the branch is non-null, else `default`; `Quicksand` and everything else → `default`. The classifier (`Projectile.cs:418-428`) and the collider are both already in hand |
| 7 | Does `BL-245` come along? | **No.** Its fall families author `do_intersections: false` (8 for 8 across `gasbag`/`cargozep`/`chuteman`/`lifesaver`), so a ray does not unblock it — it is blocked on a *decision* to diverge from authored data. Retag it after this lands; its `[Blocked: ground ray]` tag is a misdiagnosis |
| 8 | Acceptance bar? | **Collider-backed engine suite + two named cockpit checks** (C1 airfield crash; `agyrobus` downed in C5), with the existing headless `bounce-launch` suite staying green untouched as proof of the fallback |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `no_altitude` is the engine's *default* terrain test, so ~1,632 events already ground-test and the collider query is only an upgrade | Tempting: `no_altitude: true` is authored on exactly the 8 `gunshell` casings, one per chapter — precisely the body you would opt out. But if 1,632 events ran a terrain test the original's debris would not sink through the ground, and `PT-46` (d) confirmed at the controls that it does. The flag means something else — most likely gravity or spawn positioning reckoned relative to terrain altitude, which is what a casing ejected at height would want to skip |
| 2 | The `run_time`+bounce family is `BL-245`'s deferred half (as `MotionRuntime.cs:115-118` currently says) | 150 of its 204 events author `do_intersections: true` — they are *this* plan's population, and the code comment's attribution needs correcting as part of the work |
| 3 | `lava` needs a branch decision | Dead data: 0 of 324 `bounce_sequence` blocks name a lava branch install-wide. Per the author, it is engine baggage from another title — the only lava here is C3's volcano, too small to matter |
| 4 | A shot-down AI plane is the way to see the airframe fall | Nothing can kill an AI plane. A downed *player* rig plays `player_crash_dirt`/`_water` (`EffectCatalogue.cs:68`), not the per-type airframe def, so `warhawk-warhawk`'s `MAIN_ROOT_NODE` fall has no trigger — same shape of gap as `BL-059` item 2. `agyrobus` in C5 is the reachable carrier of that motion |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy. This plan is being run from
`.claude/worktrees/bl-059-ground-contact`.

## What the data actually ships

Censused over all 8 chapters' `cam_anim` on 2026-08-08 (`analysis/object-motion-ground-rest/`, plus
the flag cross-tab run for this plan).

**The three `gravity` flags take only four combinations install-wide:**

| `complex` | `no_altitude` | `do_intersections` | events |
|---|---|---|---|
| false | false | false | 1,378 |
| **true** | false | **true** | **166** |
| true | false | false | 88 |
| false | **true** | false | 8 (`gunshell`, one per chapter) |

`do_intersections: true` is a strict subset of `complex: true`.

**`do_intersections` against the four motion shapes** (`FINDINGS.md:37-49`):

| shape | `false` | `null` | **`true`** |
|---|---|---|---|
| `NEITHER` (fly-then-vanish) | 167 | — | — |
| `bounce` (`BL-240`'s solved landings) | 120 | — | — |
| `run_time` | 1,118 | 343 | **16** |
| `run_time+bounce` | 54 | — | **150** |

**Bounce branches:** 324 `bounce_sequence` blocks, 104 with a live `water` branch, **0** with `lava`.

**The canonical worked examples.**

`player_crash_dirt` — each `pieceNseq` holds two ballistic motions: the launch
(`do_intersections: true`, `translation.initial (0, 10, 0)`, `run_time 6.0`,
`bounce_sequence { default: "pNhit", water: null, lava: null }`) and a second, weaker one
(`do_intersections: false`, `initial (-3, 3, 0)`, `run_time 5–7`, no bounce). The bounce is not
synthesised by the runtime: `pNhit` is authored, pops the fireball and re-launches the piece itself.

`agyrobus` (C5) — the one reachable target carrying every shape at once:

```
MAIN_ROOT_NODE  do_int=true  rt=20.0  { default: bounce_effects, water: destroyed_water }
MAIN_ROOT_NODE  do_int=true  rt= 3.0  { default: bounce_effects, water: destroyed_water }
piece1..4       do_int=true  rt=20.0  { default: pNgrndhit,      water: pN_wtr_hit     }
```

`warhawk-warhawk` and the ten sibling airframes — `MAIN_ROOT_NODE`, `translation.initial (0,0,0)`,
`rnd_xz z −1`, gravity −9.8, `run_time 20.0`, `impact_force: true`,
`bounce_sequence { default: "bounce_effects", water: "destroyed_water" }`: a pure fall with no apex,
and the reason contact-arming cannot key on apex. Unreachable today (trap 4 above).

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

### Wave A — the query

1. ☑ A contact sweep ends a `do_intersections: true` body where the world stops it
2. ☑ The struck surface picks the `BOUNCE_SEQUENCE` branch
3. ☑ Make the tallies and the code comments tell the truth about who owns what
4. ☑ A collider-backed suite pins contact truncation, branch choice and the fallback

### Wave B — confirm at the controls

5. ☑ The two cockpit checks, and the arming epsilon TUNEd from what they show
6. ☑ A bounce follow-up continues from the landing, not from the authored rest pose (from B5 round 1)
7. ☑ The stopper idiom must not relaunch a piece that has already landed (from B5 round 2)
8. ☑ The `MAIN_ROOT_NODE` sentinel must resolve, and its launch must take the node over (from B5 check 2)
9. ☑ The settle hop must not bury the piece it just landed (from B5 round 3)

### Wave C — record it

10. ☐ Land the decode, close `BL-059` item 1, retag `BL-245`

## Dependency and parallelism notes

Strictly linear — A1 → A2 → A3 → A4 → B5 → C6 — and it is not worth parallelising: A1, A2 and A3 all
edit `MotionRuntime.cs`, and A3 and A4 both touch the counter surface A1 introduces. **Do not run any
two of A1–A4 in parallel worktrees.**

A1 is the item everything rests on; if its arming rule is wrong, B5 fails in a way that looks like a
branch-selection bug. A2 is separable in principle (it is a dictionary lookup on the collider A1
already has in hand) but pointless before A1 lands. B5 needs a human at the controls and cannot be
delegated. C6 must not start before B5 reports, because what B5 sees decides whether `BL-059` item 1
closes outright or keeps a TUNE residue.

---

# Wave A — the query

## A1 ☑ A contact sweep ends a `do_intersections: true` body where the world stops it — **landed 2026-08-08**

**Landed.** `MotionRuntime.TryContact` casts the step the body is about to take — last origin to
next origin, converted to world through the parent — and on a hit freezes the body at the contact
point: `_landed`/`_landedOrigin`/`_landedAt`, with `Finished` true from that frame and `Seek`
holding the landed pose (and the tumble/scale frozen at the contact instant, interpolated by the
hit's fraction along the step, so nothing snaps to the end of the frame). The sweep sits in `Tick`
and never in `Seek`, so a backwards scrub cannot land a piece. `AnimRuntime.ContactMask` carries the
mask in from the session — `GameSession` for the world runtime, `WorldEffectsFactory` for the crash
rig, both gated on `BuildsCollision` — and a runtime nobody wires keeps the old path, which makes
Decision 2's fallback structural rather than remembered.

**Verified.** `.\RunTests.ps1` with `CSVM_DATA_ROOT` at the primary tree: 696 units / 0 skipped,
**26/26 engine suites** (engine errors clean), **13/13 goldens hash-identical**. `bounce-launch`
passes untouched and every golden is byte-identical — which is the fallback proving itself, since no
golden build hands over a mask. Build is StyleCop-clean. ⚠ Nothing here proves contact *fires*; no
suite exercises it until A4, and the counters that would catch a silent zero are A3's.

**Not done here, deliberately.** The branch is always `default` (`ChooseBounce` ignores its `struck`
argument until A2), and `PROJECT_CONTEXT.md`'s "Current status" was left alone: it names
`docs/PLAN-overcast-match.md` as the active plan, another session's live work, and this branch stays
isolated until the whole plan lands. Repoint it at merge time.

### Original approach (kept for reference)

**Goal.** A flagged body stops at the first collider its trajectory meets — terrain, rooftop or wall
— and rests there, instead of integrating its authored `RUN_TIME` to the end and finishing below the
ground. Where there is no collision world, nothing changes at all.

**Evidence (confidence: traced).** `MotionRuntime.Seek` (`:296-325`) is a closed-form solve —
`origin = held + v0·tb + ½·tb²·accel` in the node's **parent frame** — and writes
`Target.Transform` directly; nothing consults the world. `MotionRuntime.Create` (`:242-255`) solves a
landing *only* when `run_time` is absent, so the 166 flagged events (150 of them `run_time`+bounce)
never get one. The query to copy is `ProjectilePool`'s reused
`PhysicsRayQueryParameters3D { CollisionMask = CollisionLayers.WorldAndAircraft }` (`Projectile.cs:325`);
`FlightController.cs:1503-1506` and `GameSession.cs:1869-1877` are the same call shape against
`GetWorld3D()?.DirectSpaceState`. Ruled out already: apex-based arming (the airframes and `agyrobus`
never rise), and `no_altitude` as a pre-existing terrain test (⚠ table, row 1).

**Approach.** Read the flag once in `Create` — `data.Obj("gravity")?.Bool("do_intersections")` — and
store it; a body without it takes today's path untouched. Do the sweep in **`Tick`, never `Seek`**:
`Seek` is also the scrub/pose entry point (`AnimRuntime.cs:2263`, `:2317`, and AnimLab's timeline),
and a contact test there would fire on a backwards scrub. In `Tick`, compute the next origin, convert
both ends to world through the parent's `GlobalTransform`, and cast the segment masked to
`CollisionLayers.World`. On a hit: place the body at the hit point (back through the parent frame),
force `Finished` by setting `_t = _runTime`, and record the struck collider for A2. Gate the whole
thing on `Target.GetWorld3D()?.DirectSpaceState` being non-null — **not** on `SessionSpec`
(Decision 2). Arming per Decision 5: a named TUNE constant, ~2 m or ~0.1 s from the launch origin,
whichever comes first, relying on `hit_from_inside` staying false.

**Do not put the query in `MotionSet`.** Its class remark (`MotionSet.cs:16-20`) makes a point of
holding `Node3D`s while dereferencing none — every operation is identity comparison. A physics query
there would quietly cost that property. `MotionRuntime` already writes `Target.Transform`, so it is
the honest home.

**Model recommendation.** high — it edits the animation runtime's hot path, the frame/scrub
distinction is easy to get subtly wrong, and a mistake here shows up as a wrong-looking playtest
three items later.

**Verify.** A4's suite is the real gate. Interim: a `--fly` C1 crash shows `piece1`–`4` resting on the
ground rather than hanging; the headless `bounce-launch` suite (`Suites.cs:107`) must stay green
**untouched**, which is what proves the no-collider fallback still runs the old path.

**⚠ Traps.** The parent-frame/world-frame conversion is the likeliest bug — the solve is in the
parent frame and the query is in world space; getting it backwards yields contacts at plausible-
looking but wrong places. `Seek` may be called past `run_time` by an instant land (`:300-302`), so a
truncated body must stay idempotent under a later `Seek`. `AnimRuntime.cs:2282` sets the sequence's
`duration` from the *predicted* flight before any contact happens — that stays the authored ceiling
and is correct as an upper bound, but do not "fix" it to match the shortened flight. Finally, the
epsilon is TUNE, not fact: give it a name and the reasoning, never a bare `2f`.

## A2 ☑ The struck surface picks the `BOUNCE_SEQUENCE` branch — **landed 2026-08-08**

**Landed.** `ChooseBounce` now reads the struck body: water with a live `water` branch takes it,
everything else takes `default` — a null `water` branch falls back rather than suppressing the
bounce, and quicksand is not water. No `lava` path, and the doc comment says why (0 of 324 blocks;
engine baggage from another title on the same engine, C3's volcano being the only lava here and far
too small). The classifier arrives as `AnimRuntime.SurfaceIsWater`, bound by the session to
`ProjectilePool.ClassifySurface` — the same read a round's impact and a wingtip graze make — so
Mech3 still has no dependency on the flight layer. Unbound (null) answers "not water".

⚠ **Landed in one commit with A3**, whose comment corrections sit in the same two files. The split
was bookkeeping, not two changes.

**Verified.** Full `.\RunTests.ps1`: 696 units, 26/26 suites, 13/13 goldens identical. That the
branch is *selected correctly* is A4's assertion — this run only shows nothing regressed.

### Original approach (kept for reference)

**Goal.** A flagged body that lands on water fires its `water` branch — an `agyrobus` piece pops
`pN_wtr_hit` in the sea and `pNgrndhit` on land — and everything else takes `default`.

**Evidence (confidence: traced).** `ProjectilePool.ClassifySurface` (`Projectile.cs:418-428`) maps a
struck body's group tag to `SurfaceClass` (`WeaponDefs.cs:10`); `FlightController.cs:1225-1228`
already does exactly this two-way read for the player's own crash. 104 of 324 `bounce_sequence`
blocks carry a live `water` branch; **zero** carry `lava` (⚠ row 3). `MotionRuntime.cs:115-118`
records the branch choice as the reason this family was deferred — A1 supplies the missing collider.

**Approach.** Extend the `PendingBounce` arming to pick from the block rather than always reading
`default`: `ClassifySurface(hit) == SurfaceClass.Water` and a non-null `water` → `water`, else
`default`. `Quicksand` and every other class → `default` (Decision 6). No change to `MotionSet.Tick`
or the `Landing` dispatch — they carry whatever string is armed.

**Model recommendation.** medium — a small, well-fenced lookup on machinery A1 has already built.

**Verify.** A4's water case; then B5's `agyrobus` over C5 water if one can be downed there.

**⚠ Traps.** A null `water` branch means "the author didn't distinguish", so it falls back to
`default` — it does not mean suppress the bounce. Do not add a `lava` path "for symmetry": there is
no data behind it and it would be untestable dead code.

## A3 ☑ Make the tallies and the code comments tell the truth about who owns what — **landed 2026-08-08**

**Landed.** `MotionSet.ContactLandings` / `ClockEndings` count only bodies that actually test
contact, and `LogMotions` prints them once a second under `--debug-anim`, with an explicit
"NO CONTACT AT ALL (is a mask wired?)" when the first is zero — the silent-zero failure mode named
in the plan. Left standing by `Reset`, the same rule as `LaunchCount`. The deferred tally at the
`OBJECT_MOTION` handler no longer files a contact-tested body as unhandled (`bounceArmed` now
accounts for a body that arms its bounce at contact rather than at creation), and the three comment
sites that attributed the 204 `RUN_TIME`+bounce events to `BL-245` now say what is true: 150 of them
author `do_intersections: true` and are this plan's. `docs/formats/destructibles.md`'s "Debris
tumbles" bullet carries the new mechanism, the lava finding, the arming rule, and the corrected
`BL-245` framing ("not blocked on a ground ray — blocked on a decision to diverge from the data").

**Verified.** Full `.\RunTests.ps1` green as above. ⚠ The counter itself is unproven until something
makes it non-zero; A4 is what does that.

### Original approach (kept for reference)

**Goal.** The runtime's own reporting stops describing this work as deferred, and starts reporting
whether contact is actually happening.

**Evidence (confidence: traced).** `AnimRuntime.cs:2273-2281` files every unarmed bounce under
`ObjectMotion(bounce_sequence deferred)` and its comment attributes the 204 `run_time`+bounce events
to `BL-245` — but 150 of them are this plan's population (⚠ row 2). `MotionRuntime.cs:39-42` and
`:115-118` carry the same misattribution, and `:115-118` additionally says the water branch "cannot be
chosen without the struck collider", which A2 falsifies.

**Approach.** Add the Decision 2 counter — landings by contact vs by fallback — alongside the
existing `Count(...)` tallies, and surface it where a `--fly` session can see it. Narrow the
"deferred" tally to what genuinely remains deferred (`BL-245`'s falls, and the `do_intersections:
false` remainder). Rewrite the three comment sites to say what is now true, and update
`docs/formats/destructibles.md`'s "Debris tumbles" bullet in the same turn.

**Model recommendation.** medium, low effort — mechanical once A1/A2 have settled the behaviour.

**Verify.** A `--fly` C1 crash reports a non-zero contact count and a zero-or-explained fallback
count; a `--freecam` run reports the mirror image. A fly session reporting **zero** contacts is the
failure this counter exists to catch.

**⚠ Traps.** Do not delete the deferred tally — `BL-245` still needs it, and a tally that silently
stops reporting reads as "solved". The counter must survive a crash respawn the way `LaunchCount`
deliberately does (`MotionSet.cs:31-34`).

## A4 ☑ A collider-backed suite pins contact truncation, branch choice and the fallback — **landed 2026-08-08**

**Landed.** New `ground-contact` suite (27 now, was 26). It builds the chapter world with
`collision: true`, **asserts the collision space exists** before asking anything of it (the trap:
a collider-less suite would pass by taking the fallback and prove nothing), probes down for the real
surface height, then runs one hand-authored `do_intersections: true` body — 5 m/s downward from 60 m
up, gravity −9.8, `run_time 20`, both branches named — three times:

| run | result |
|---|---|
| mask + land | ends **3.03 s**, resting **0.00 m** from the surface, `default` branch |
| mask + water hook | takes the `water` branch |
| **no mask (control)** | runs the full **20.02 s** and ends **2,000 m below** the surface |

Synthetic rather than a chapter's own debris on purpose: the reachable carriers reach their launch
through a death sequence and a randomised draw, and what is under test is the sweep. The water case
stubs `SurfaceIsWater` rather than hunting for reachable sea — the classifier has its own coverage,
and the hook is the seam.

**Verified — and shown able to fail.** Disabling `TryContact`'s gate makes the contact run report
the same 20.02 s / −2000 m as the control and four checks go red. Full `.\RunTests.ps1`: 696 units,
**27/27** suites, 13/13 goldens identical.

⚠ **What the able-to-fail pass actually taught.** My first attempt broke `ArmDistance` alone and the
suite stayed green with a byte-identical note — because arming is distance **or** time, and
`ArmSeconds` still armed the body at 0.1 s, long before a 60 m drop reaches anything. So this suite
does **not** cover the arming rule; B5's cockpit checks do, since arming only bites where a piece
starts inside the wreck it launched from. Recorded in the suite's own doc comment.

### Original approach (kept for reference)

**Goal.** The behaviour is guarded headlessly, so a later refactor cannot quietly restore the sink.

**Evidence (confidence: traced).** The precedent for a collider-backed suite is already here:
`Suites.cs:1006-1026` classifies a surface and casts against a live `DirectSpaceState`, and the
`air-to-air` suite (`:89`, `:968-1111`) builds two real rigs with `AircraftBody` colliders and scores
a real `Downed`. The existing `bounce-launch` suite (`:107`, C1's `refuel1` defs) is headless with no
colliders and is the fallback's witness.

**Approach.** A new suite that launches a flagged body over known geometry and asserts: the flight
ends **shorter** than the authored `RUN_TIME`; the body rests at the surface rather than below it;
the `default` branch dispatched; and — the water case — that a water collider selects the `water`
branch. Assert bands, not exact times, following the rule the `bounce-launch` suite already states
(`:2774`). Leave `bounce-launch` itself unedited; its staying green is the assertion.

**Model recommendation.** medium — pattern-following against two existing suites, but the assertions
need care to be able to fail.

**Verify.** `RunTests.ps1`; plus deliberately break the arming epsilon locally and confirm the suite
goes red — an unchanged number is not evidence until you have seen it able to fail.

**⚠ Traps.** A suite that builds no colliders will pass by taking the fallback path and prove
nothing; assert that the collision world exists before asserting anything about contact.

# Wave B — confirm at the controls

## B5 ☑ The two cockpit checks, and the arming epsilon TUNEd from what they show — **landed 2026-08-09**

**Landed — both checks confirmed at the controls, over three rounds.** Each round found a real
defect and each became its own item, which is what the check was for:

| round | report | became |
|---|---|---|
| 1 | *"the plane is looping … jumps back to the crash point 4 times"* | **B6** — a bounce follow-up continues from the landing |
| 2 | *"check 1 still happening"* | **B7** — the stopper idiom must not relaunch a landed piece |
| 2 | *"the agyrobus exploded but continued on its animation path"* | **B8** — `MAIN_ROOT_NODE` is a sentinel, not a name |
| 3 | *"no repetition but plane went through ground"* | **B9** — the settle hop must not bury the piece it just landed |
| 4 | *"works. flown into a wall and the plane dropped to the ground."* | ✅ both checks pass |

Round 4 adds an unasked-for third look worth recording: flying into a **wall** also brings the wreck
down onto the ground — the collide-destructible path reaching the same sweep, which is the case
Decision 1 chose a swept segment over a down-ray for.

**The epsilon: both values kept, and the A/B is the reason.** Measured 2026-08-09 on the C1 airfield
dive under `--det`, arming disabled outright versus shipped:

- the four **launches** land at **byte-identical coordinates** either way — the epsilon suppresses no
  real contact and buys the launches nothing in any reachable case;
- what it buys is the **settle hop**, which starts lying on the surface: unarmed it contacts on its
  first frame and the authored ~0.5 m hop is cancelled (11 contact endings became 13, the extras
  being hops that ended instantly).

So `ArmSeconds = 0.1f` is the limb doing the work, bounded above by the hop's ~0.6 s round trip and
below by one frame. `ArmDistance = 2f` binds only for a body fast enough to clear 2 m inside 0.1 s
(a launch inheriting a dive) and **no measured case exercises it** — it stays an unfalsified guard
rather than a confirmed value, and the constant's doc comment says exactly that rather than implying
a measurement it does not have.

⚠ The 0.044 s tunnelling that buried the hops before B9 is **not** an argument for tightening this.
That body had no business carrying the dive's momentum; removing it, not shrinking the window, is
the fix that holds.

### Original approach (kept for reference)

**Goal.** The defect that motivated the item is gone where a player can see it, and the epsilon is a
measured value rather than a guess.

**Evidence (confidence: traced for what to look at; the epsilon itself is TUNE).**
`analysis/object-motion-ground-rest/FINDINGS.md:65` records the failing observation of 2026-08-08:
`player_crash_dirt`'s four pieces stay above ground, and an `agyrobus` shot down in C5 was lost
between buildings. `agyrobus` carries both shapes and live water branches on all six motions (see the
data survey), which makes it the one reachable target that exercises everything.

**Approach.** Two looks, per Decision 8:
1. **Crash on the C1 airfield.** `piece1`–`4` must rest *on* the ground, each popping `pNhit` at the
   surface, then settling through the second authored motion.
2. **Shoot down an `agyrobus` in C5** over land — root and pieces stop at the surface or on a
   rooftop, firing `bounce_effects` / `pNgrndhit`. If one can be downed over C5 water, the same run
   gives `destroyed_water` / `pN_wtr_hit`.

Then set the epsilon from what the first flight-frames actually do, and record the value with its
reasoning.

**Model recommendation.** medium — the judgement is the author's at the controls; the agent's job is
to prepare the run and record the verdict.

**Verify.** The looks themselves, plus the standing rule: a full 8-chapter `--freecam --chapter=<X>`
regression with zero errors and unchanged mesh/node counts. Read `docs/verification.md` first.

**⚠ Traps.** If C5 has no reachable water, the water branch is confirmed by A4's suite **only** — say
so plainly rather than implying a cockpit confirmation. A piece resting on a rooftop is a *pass*, not
a bug: it is the behaviour the collider query was chosen for over a down-ray. And a piece that still
sinks is only a defect if its event authors the flag — check before filing.

## B6 ☑ A bounce follow-up continues from the landing, not from the authored rest pose — **landed 2026-08-08**

**Why it exists.** B5 round 1, from the controls: *"if I crash straight down the plane is looping.
at the rest position it jumps back to the crash point 4 times until staying there."* Four jumps,
four pieces.

**Landed.** `MotionRuntime.Create` re-homes every non-`TopLevel` ballistic launch to the node's
authored rest pose (`:334-338`) — a rule that keeps a pooled effect template's repeat explosions
from drifting. A bounce follow-up is the case where it is wrong: contact dispatches `pNhit`, which
re-launches `pieceN` with its second authored motion, and the re-home teleported the piece back to
the crash point before it flew. `Landing` now carries `ByContact`; the dispatch path marks the node
via `AnimRuntime.MarkLandingResume`, and `Create` consumes that one-shot mark to keep the live pose.
Narrow on purpose — the re-home rule is load-bearing everywhere else.

**Verified — and shown able to fail.** New check in the `ground-contact` suite: land a body by
contact, mark it the way the dispatch path does, create the follow-up, and assert it starts within
1 m of the landing rather than 60 m up at the authored rest. Reverting the fix turns it red. Full
`.\RunTests.ps1`: 696 units, 27/27 suites, 13/13 goldens identical.

## B7 ☑ The stopper idiom must not relaunch a piece that has already landed — **landed 2026-08-08**

**Why it exists.** B5 round 2: the jumping was still there after B6. A deterministic headless repro
(`--collision --hold=-1,0,0,1`, dive into the ground, `--debug-anim`) showed **`piece1` landing four
times per crash while pieces 2–4 landed once** — and the data says exactly why: only `p1hit` carries
`STOP_SEQUENCE piece1seq`.

**The mechanism.** `AnimInstance.StopSequence` implements the *stopper idiom*: when nothing is
running under the name, START it, the way an ON_CALL teardown block nobody calls gets reached.
`piece1seq` is a single OBJECT_MOTION, so its runner is finished the instant the piece leaves —
which means the landing's own `p1hit` "stopped" a finished sequence and thereby **relaunched the
piece from the crash point**, which landed, which dispatched `p1hit` again. Before this plan the
bounce never dispatched, so the idiom never bit.

**Landed.** Two parts. `AnimInstance.AddRunner` is now the one way a runner joins an instance, so
"has this instance ever run that sequence?" cannot be bypassed — the bootstrap that starts
non-ON_CALL sequences goes through it too, which matters because **no call site in the whole def
ever calls `pieceNseq`**. And `StopSequence` suppresses the restart only for a sequence that has
already run **and** whose body launches a `do_intersections` motion.

⚠ **The narrowness is not fastidiousness — it is measured.** Suppressing the restart for *any*
already-run sequence moved the **`c1-destroy-effects` golden**: some world def genuinely relies on
the stopper idiom re-running a finished sequence. That is a real, separate decode question about the
idiom, left untouched here. Gating on "launches a contact-tested body" confines the change to this
plan's 166 events, and the golden is byte-identical again.

**Verified.** Repro now shows exactly 4 landings, one per piece, and the counter reads
`4 by contact, 0 on their run time`. Full `.\RunTests.ps1`: 696 units, 27/27 suites, **13/13 goldens
hash-identical**.

## B8 ☑ The `MAIN_ROOT_NODE` sentinel must resolve, and its launch must take the node over — **landed 2026-08-08**

**Why it exists.** B5 check 2, from the controls: *"the agyrobus exploded but continued on its
animation path."* First read as a `STOP_ANIMATION agbus_fly` failure and proposed as a separate
backlog item; the user chose to fix it inside this plan, and the diagnosis that followed showed why
that was right — it is **this plan's own population**, not a neighbouring bug.

**The mechanism, in two halves.**

*The sentinel never resolved.* `MAIN_ROOT_NODE` / `INPUT_NODE` mean "the node this definition was
invoked on". `AnimRuntime` already knows that — `IsSelfNodeRef` is applied to `PufferState`'s
`AT_NODE` and to condition nodes — but **`Targets` did not**, so it fell through to the symbol table
(no binding) and then to name matching (no node is called that), counted one unresolved op and
dropped the event in silence. A headless probe on the C5 bus reads the whole bug in one line:
`randomdestseq/ObjectMotion:MAIN_ROOT_NODE` dispatched, `launches+0`, and the only driver still on
the root is `ScriptPlayback@agbus_fly`. **154 events install-wide** carry the sentinel under a key
`Targets` resolves, and a census of the OBJECT_MOTION ones is stark:

| kind | key | count | carriers |
|---|---|---|---|
| `ObjectMotion` | `node` | **90** | the 11 airframes' whole-hull fall (8 chapters × 11) + `agyrobus` ×2 — **all 90 author `do_intersections: true`** |
| `ObjectRotateState` | `name` | 32 | the `map` prop's `open_map`/`close_map` |
| `ObjectActiveState` | `node` | 16 | `genx12`, the generic exploder template |
| `ObjectMotionFromTo` | `name` | 16 | the `map` prop |

So 90 of this plan's 166 flagged events — the entire self-referencing half, including every one of
the eleven airframe falls A1 was written for — never launched at all. The sweep could only ever have
been exercised on the other 76.

*And the launch must not re-home.* Resolving the target alone made it worse: the bus has **no
placement of its own** (the world reports it "unplaced, left at the origin" and `agbus_fly`'s SI
script flies it), so its authored rest is the map origin and B6's re-home teleported the wreck
**13.9 km** away to fall out of sight. `MotionRuntime.Create` now also skips the re-home when another
live motion is driving the node — a *takeover*, the same family as B6's landing resume: the live pose
is authoritative when something else just put the node there. `MotionSet.Add`'s existing
one-per-(target, channel) rule then evicts the playback, and the bus falls where it was hit.

**Verified — and shown able to fail twice, once per half.** New `self-ref-launch` engine suite on the
real C5 world: fly the bus for 2 s, assert the SI script is what carries it and that it is >100 m
from its authored rest, kill it, then assert the anchor carries a `MotionRuntime`, that the launch
starts within 5 m of where the bus was, that `agbus_fly` no longer drives it, and that it goes down
rather than on. Disabling the sentinel branch reddens four checks (`driver=ScriptPlayback`,
`launches+0`, 338.6 m of route in 5 s, *climbing*); disabling the takeover reddens the second with a
`jump=13907.1 m`. Deliberately **branch-agnostic**: `randomdestseq` opens with `IF RandomWeight(0.5)`
and only one branch stops the fly script, so every assertion holds for both draws.

⚠ **`STOP_ANIMATION agbus_fly` was never broken** — the first read was wrong. It sits in only one of
the two branches, and the branch without it relies on the launch displacing the playback, which is
what the original must have done and what this item restores.

Full `.\RunTests.ps1`: 696 units, **28/28** suites, **13/13 goldens hash-identical** — which is the
answer to the obvious worry, since this newly resolves `genx12`'s two `ACTIVE_STATE`s and the `map`
prop's four pose ops as well, and `c1-destroy-effects` is a `genx12` carrier. The B7 crash repro is
unmoved: 4 landings per crash, one per piece.

## B9 ☑ The settle hop must not bury the piece it just landed — **landed 2026-08-09**

**Why it exists.** B5 round 3, from the controls: *"check 1 no repetition but plane went through
ground."* The looping was gone (B7); the burial was not.

**What it actually was — and the first reading was wrong again.** The obvious culprit is the
`pNhit` settle hop's own `do_intersections: false`, so the first fix was to let a hop **inherit** the
test from the landing it continues. That was necessary but did nothing on its own: the repro still
read `4 by contact, 0 on their run time`, four landings for eight bodies. Tracing the sweep showed
why — the hop was already **underground when it armed**:

```
DIAG sweep piece1 t=0.20 from=(…, 176.92, …) to=(…, 176.18, …) hits=0
DIAG sweep piece1 t=0.40 from=(…, 167.98, …) to=(…, 167.24, …) hits=0
```

45 m/s downward, from a body authored at **+3 m/s up**. That is the aircraft's dive momentum,
handed to the hop by `InheritedWorldVelocity` — the rule that makes launched debris scatter along
the plane's travel. The first launch spends that momentum; the hop is a piece lying at rest on the
ground, and re-inheriting it covered the 2 m arming epsilon in **0.044 s**, so by the time the sweep
armed, every ray started below the terrain and found nothing. Not a missing test: a body that
tunnelled before the test could look.

**Landed.** One question — *does this motion continue a contact landing?* — asked once at the top of
`MotionRuntime.Create` (the resume mark is one-shot; consuming it **is** the answer), with three
consequences: no inherited momentum, no re-home to the authored rest (B6), and the contact test
carries over.

⚠ **That last one is a judged divergence, not a decode** — the one place this plan reads
`do_intersections: false` as *unset* rather than *opted out*. Its defence is its narrowness: the
mark is set only by a contact landing, so the only motions it can reach are follow-ups the sweep
itself dispatched onto a node the data **did** flag. It does not touch Decision 3's line — a
false-flagged launch that continues nothing still declines the sweep, and the suite asserts exactly
that as a second case. A compiled `gravity` block always carries all four bits, so "false" here
cannot be told from "not re-stated", and it is the object, not the event, the original tests.

**Why it matters:** `player_crash_dirt`'s four pieces are 4 of the 16 `(def, node)` pairs the census
calls ground-tested **and** left lying there — the only debris a player can walk up to. Burying them
100 m under the airfield (3t − 4.9t² at t=5) defeats the item outright.

**Verified.** Repro: **8 by contact, 0 on their run time** — every launch and every hop — where it
read 4 and 4 buried before. Three new checks in the `ground-contact` suite (hop inherits the test;
hop inherits no momentum; an unmarked false-flagged launch still declines), the middle one
band-asserted because the synthetic body is authored throwing downward. Full `.\RunTests.ps1`: 696
units, 28/28 suites, 13/13 goldens hash-identical — structurally safe, since a capture that builds
no colliders never lands by contact, so the mark is never set and none of the three rules can fire.

# Wave C — record it

## C10 ☐ Land the decode, close `BL-059` item 1, retag `BL-245`

**Goal.** The next session finds the decision recorded where it looks, and no item claims to be
blocked on something that now exists.

**Evidence (confidence: traced).** `BL-059` item 1 currently describes work that is two-thirds landed
(the bounce dispatch and the `SOUND_GROUPS` resolver); its rewrite of 2026-08-08 is uncommitted in the
main checkout and still lists as traps two things this plan settled as decisions. `BL-245` is tagged
`[Blocked: ground ray]`, which Decision 7 shows to be a misdiagnosis.

**Approach.** Delete `BL-059` item 1 from `backlog.md` (this repo deletes rather than marks FIXED),
with the record going in the landing commit's message. Retag `BL-245`: not blocked on a ray, blocked
on a decision to override `do_intersections: false` for 379 falls. Fold the flag cross-tab and the
disproven `no_altitude` reading into `docs/formats/destructibles.md`, and refresh PROJECT_CONTEXT.md's
"Current status". Use `/commit-next` per item throughout the plan, and `/close-backlog-item` here.

**Model recommendation.** medium — mechanical, but it touches the files a future session trusts.

**Verify.** `git log --grep=BL-059` tells the story cold; `backlog.md` no longer defines the retired
item; the duplicate-ID and encoding commit hooks pass.

**⚠ Traps.** `docs/HISTORY.md` is frozen (2026-08-06) — the record goes in the commit message, never
appended there. `BL-022`'s trap (c) cites the 16 flagged pairs as "`BL-059`/`BL-245`'s" — that
citation needs updating too, or it will outlive the item it points at.
