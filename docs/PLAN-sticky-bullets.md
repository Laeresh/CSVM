# Sticky bullets — the player's gun aim assist

**ACTIVE PLAN** (written 2026-08-13). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

This plan builds `BL-342`: the per-muzzle gun aim assist the original ships and CSVM does not, plus
the removal of a scatter CSVM applies and the original does not. Both halves live at the same fire
call, which is why they are one plan rather than two, but they are separate changes with separate
verification and the wrong-scatter removal goes **first** so the assist is not tuned against a
dispersion the original never had. The mechanism is fully decoded in
[`org/aim-assist.md`](org/aim-assist.md) (two Ghidra passes, 2026-08-10 and 2026-08-12) and this
plan builds it rather than re-deriving it. Every constant here has an address behind it; none came
from footage.

`BL-342` was **re-verified still-open in the session that wrote this plan**, against both the
record (`git log --grep=BL-342` returns only `de2d8cf`, which closed the research predecessor
`BL-091`) and the code (`Projectile.cs:533` still fires straight down the muzzle axis, and no
target scan exists anywhere in `src/Flight/`). No other backlog item is drawn in.

Out of scope: AI gunnery. The original's AI path is a different, simpler mechanism (one per-plane
`+0x95c` "dead eye" scalar through the same cone function, no scan, no lead, no smoothing) and
nothing in CSVM shoots back yet, so there is nothing to hang it on. Also out of scope: the
multiplayer transmission of the assisted vector, deferred to a networking milestone because CSVM
has no networking to defer it into (Decision 5) — B6 keeps only the part that matters today. And
out of scope: whether the shipped constants *feel* right in VS, which stays `BL-301`'s call from
`PT-43` evidence.

Two of the original's four candidate lists have no CSVM equivalent yet (turrets, AI ground/sea
vehicles) because both arrive with M4's AI. B4 builds their scan passes anyway, iterating nothing,
so M4 wires a list in rather than rediscovering this item.

## Milestone goal

- The player's guns fire along an assisted vector: a per-muzzle gun line that leans toward the
  constant-velocity intercept of the best-aligned valid target, smoothed in plane-local space.
- Guns become a practical kill weapon in `--vs` without rockets, which is the `PT-43` complaint.
- Nothing in CSVM scatters a round by `CANNON_SPREAD` any more, because the original never did.
- The candidate set matches the original's four lists, not just aircraft.

**This plan changes only the launch direction.** Nothing touches a round after spawn — no steering,
no homing, no per-frame re-aim. If a change lands in `Projectile`'s integrator, it is the wrong
change.

## Decisions (2026-08-13)

| # | Question | Decision |
|---|---|---|
| 1 | Is the wrong-scatter removal part of this plan or its own backlog item? | **Its own wave here, landing first** — it is a separate defect, but it edits the same fire call, and leaving it in place would corrupt every judgement about how the assist feels. |
| 2 | Port the per-target cone override (`+0x50`) even though no shipped entity sets it? | **Yes, build it** — it costs one field and one branch, and scoping it out is a silent behaviour change the moment a mission authors `STICKINESS`. |
| 3 | Port the AI's dead-eye scatter path at the same time? | **No** — nothing in CSVM shoots back; it would be untestable code. |
| 4 | Re-tune the assist constants if it reads as aimbot? | **No, not in this plan** — the shipped constants are the original's answer. Strength is `BL-301`'s call from `PT-43` footage, not a taste call made here. |
| 5 | Build the multiplayer transmission path? | **Deferred to a networking milestone** — CSVM has no networking at all (no `rpc`/`MultiplayerApi`/`ENet` anywhere under `CSVM/src/`, checked 2026-08-13), so there is no wire and the code would be untestable. B6 records the invariant so the later milestone inherits it. |
| 7 | Who gets the assist in splitscreen? | **Every human pilot, all panes.** The original's `param_1 == DAT_0071c298` test reads as "the local player" only because that engine has no splitscreen and therefore exactly one human. Its real content is human-versus-AI, which is what the else-branch does. Gating on `PlayerIndex == 0` would be a misread. |
| 6 | What does the gun reticle follow once rounds stop leaving along the nose axis? | **Decode it inside B5, do not design it** — this is a fact about the original's HUD, and the plausible answer ("the pipper follows the assist") may be wrong. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `sticky_bullet_*` steers rounds in flight toward a target | The 2026-08-10 decode: nothing touches a round after spawn. The pre-decode wording in `formats/vehicle.md` was wrong and has been retracted. |
| 2 | `CANNON_SPREAD` is a per-gun dispersion cone that composes somehow with the 1° assist scatter | The 2026-08-12 decode: the key has exactly one reader in `crimson.exe` (`FUN_004ba6f0` @ `0x004ba9da`), which stores `−cos(value × π/180)` into `[def+0x210]+0x08`, read only by the four assist scorers. It is the assist's **acceptance cone**, not a scatter. There is no composition. |
| 3 | `[def+0x210]+0x08` has a non-zero built-in default when `CANNON_SPREAD` is absent | The block is `calloc(1, 0x38)` at `0x004ba6f6`, so an absent key leaves `0.0` = `−cos(90°)`: the whole forward hemisphere. |
| 4 | The assist is aircraft-only | Four scorers over four lists, byte-identical scoring: vehicles, turrets, `MStruct` mission structures, and live proximity-fused ordnance in flight. Guns snap onto an incoming rocket by design. |
| 5 | `param_1 == DAT_0071c298` means "player one", so only the first splitscreen pane is assisted | Not yet disproven by a test, because it is a *porting* misread rather than a claim about the original: that engine has no splitscreen, so its one local player is its one human. `FUN_004b6530`'s else-branch shows the real split is human-versus-AI. In CSVM every pane is a human. See Decision 7. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, B2, B3, B4, B5, B6 | Confirm the trace against `org/aim-assist.md`, then implement. Every constant has an address. |
| **Direction sound, magnitude a judgement call** | — | none. |
| **Leads only — no mechanism yet** | C7 | A judgement at the controls, not a mechanism. It may end in a `BL-301` tuning note rather than code. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The four `player.json` keys, and what the parser (`FUN_004735b0` @ `0x004744e0`–`0x004745ad`)
stores:

| Key | Shipped | Stored as | Built-in default |
|---|---|---|---|
| `sticky_bullet_catchup_rate` | 5.0 | raw, per second | `1.0` |
| `sticky_bullet_inaccuracy` | 1.0 | × π/180, so the file value is **degrees** | `1°` |
| `sticky_bullet_forget_interval` | 1.5 | raw, seconds | `0.5` |
| `sticky_bullet_dist_factor` | 0.0 | raw, per metre | `2.5e-4` |

⚠ Do not reproduce the original's defaults bug: the missing-key branch for `inaccuracy`
(`0x00474550`) writes into `catchup_rate`'s global. The shipped file always carries the key, so it
never fires.

The weapon side, per def: `+0x1c` = `RANGE`, `+0x20` = `RANGE²` (precomputed at `0x005adfb8`),
`+0x2c` = `VELOCITY` m/s, `[+0x210]+0x08` = `−cos(CANNON_SPREAD°)`, the assist cone. Stock guns
ship `CANNON_SPREAD 6.0`, so the cone is a 6° half-angle and the stored cosine is −0.994522.

The per-target override, candidate `+0x50`: `−1.0f` from every entity constructor (planes
`0x004b0006`, turrets `0x004a9ae7`, MStructs `0x004a25ee`, tracked ordnance `0x00441be1`, all
vtable `0x00608b58`), meaning "no override, use the weapon's cone". The only authored writer is the
turret key `STICKINESS`, which [`formats/turrets.md`](formats/turrets.md) records shipping **zero
times**. So with the shipped data the cone is always the firing weapon's.

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

### Wave A — Stop scattering by a number that isn't a scatter

1. ☑ Remove the `CANNON_SPREAD` jitter from the fire path and requalify the key everywhere it is described

### Wave B — Build the assist

2. ☑ Per-muzzle slot state and the per-frame forget + catch-up update
3. ☐ The constant-velocity intercept solver
4. ☐ The candidate scan, rejection gates and scorer
5. ☐ Wire the assist into the fire call, apply the 1° scatter, and decode what the reticle follows
6. ☐ Who gets the assist (every human pilot, not just pane 1), and the shooter-authoritative invariant for later

### Wave C — Judge it

7. ☐ `PT-43` gun-feel pass in `--vs`, and hand the strength question back to `BL-301`

## Dependency and parallelism notes

Strictly ordered, and not by taste: A1 must land before anything in Wave B, or every judgement about
how the assist feels is made through a 6° dispersion the original does not apply. Inside Wave B,
B3 (the solver) blocks B4 (the scan calls it), B4 blocks B5 (the fire call needs a winner to fire
at), and B5 blocks B6 (the guard and the invariant are about a fire path that must exist first).
B2 is independent of B3/B4 and can be written alongside them, but it also lands in
`FlightController.cs`, so it contends with B5 on that file. C7 needs all of Wave B.

B3, B4 and B2 all land in the new `CSVM/src/Flight/AimAssist.cs`, and B2/B4/B5 all add cases to one
new `aim-assist` suite in `Suites.cs`, so those are shared files too.

**File contention — do not run these in parallel worktrees:** A1, B2, B5 and B6 all edit
`CSVM/src/Flight/FlightController.cs`; A1 and B5 both edit `CSVM/src/Flight/Projectile.cs`; B2, B3
and B4 all edit `AimAssist.cs`. In practice this plan is a single-agent chain, and the only
genuinely splittable item is B3, whose boundary would be the solver method plus its suite cases.

B6 is deliberately small (a guard plus two doc notes) because the transmission half is deferred; do
not let a later session re-read it as an unbuilt feature and start writing a network layer under
this plan.

---

# Wave A — Stop scattering by a number that isn't a scatter

## A1 ☑ Remove the `CANNON_SPREAD` jitter from the fire path and requalify the key everywhere it is described

**Goal.** A gun round leaves the muzzle along the muzzle axis exactly, with no random deviation, as
the original's does. `CANNON_SPREAD` stops being described anywhere in the repo as a dispersion
cone.

**Evidence (confidence: traced).** `CANNON_SPREAD` (string `0x0062b334`) has exactly one reader in
`crimson.exe`: `FUN_004ba6f0` at `0x004ba9da`, which computes `−cos(value × π/180)` and stores it
at `[def+0x210]+0x08` (`0x004ba9ef`–`0x004ba9fc`). The only consumers of that slot are the four
assist scorers (`0x004bb088`, `0x004bb32c`, `0x004bb5d2`, `0x004bb887`); every other `[reg+0x210]`
load in the program was enumerated and none reads `+0x08`. CSVM's use is
`Projectile.cs:534` — `forward = ApplySpread(forward, weapon.CannonSpread ?? 0f)` — inside
`Spawn`, so it applies to **rockets as well as guns**, which the original does not do either.

**Approach.** Delete line 534 in `CSVM/src/Flight/Projectile.cs`. **Keep `ApplySpread` itself**: it
is still used by `SpawnDirtDebris` (`Projectile.cs:1678`) and `SpawnRicochet`
(`Projectile.cs:1709`) for effect spread, which is unrelated. Keep `WeaponDef.CannonSpread`
(`WeaponDefs.cs:75`, parsed at `:237`) — the field stays, its meaning changes, and B4 consumes it
as the assist cone. Retitle its comment from "gun dispersion cone, degrees" to the cone's real role.

Then fix every place the old reading is written down, because each one currently reads as settled:

- `CSVM/src/Flight/IncomingFire.cs:18` and `:38` — the doc comment leans on the jitter to make the
  near-miss sweep read as a gradient, and the `Standoff = 120f` choice is justified by cone growth
  with range. Without the jitter every pass is at exactly the requested distance. State that, and
  decide whether the standoff constant still has a reason (it may simply no longer need one).
- `CSVM/src/Testing/Suites.cs:1882` and `:2160` — comments that budget for a `CANNON_SPREAD`
  deviation the code no longer applies. `:2264` gates on `w.CannonSpread is not > 0f` as a
  rocket/gun discriminator; check whether that gate still means what it says once the field is a
  cone, and leave it working either way.
- `CSVM/src/Flight/FlightController.cs:1366` — the reticle doc says the reticle marks the centre of
  the `CANNON_SPREAD` cone. After A1 the reticle marks where the round actually goes; after B5 it
  marks the *unassisted* axis while the round goes somewhere else, which is a real HUD question.
  Note it here, do not solve it here.
- `docs/architecture.md`'s `src/Flight/Projectile.cs` section, and the closing delta table in
  `docs/org/aim-assist.md` (its `Scatter` row already carries the ⚠ this item clears).

**Model recommendation.** medium. Mechanical deletion plus a careful sweep of prose that currently
asserts the wrong thing; the judgement is in not over-reaching into the HUD question.

**Verify.** `RunTests.ps1` green, with attention to the two suites whose comments budget for
spread (`Suites.cs:1882`, `:2160`) — they should pass *more* reliably, not less, and if either was
silently relying on the jitter that is a finding. Then an in-engine spread measurement reusing the
`FireOne` pattern at `Suites.cs:2129`: fire N rounds from one fixed muzzle transform at a wall and
record the impact points. **Take that baseline before deleting line 534**, while the pattern can
still be seen to scatter; after, every round lands on the same point. An unchanged number is not
evidence unless you have seen it able to fail.

**⚠ Traps.** `ApplySpread`'s polar sampling is `half * sqrt(rand)`, disc-uniform, and `half` is
`DegToRad(coneDeg) * 0.5f` — it treats the value as a *full* angle. Both are wrong for the
original's assist scatter (uniform polar angle in `[0, θ]`, and the value is a half-angle), so do
**not** repurpose this function for B5; write the scatter B5 needs separately. Do not delete
`weapon.CannonSpread` from the parser in a burst of tidying — B4 needs it. Do not touch the
integrator: this item is one line plus prose.

# Wave B — Build the assist

## B2 ☑ Per-muzzle slot state and the per-frame forget + catch-up update

**Goal.** Each of the plane's gun barrels carries a plane-local gun line that lags behind a target
direction, unwinds to centre when that barrel stops firing, and drags with the aircraft's own
manoeuvring.

**Evidence (confidence: traced).** Eight `0x24`-byte slots at plane `+0x3a4`, indexed
`weaponGroup·2 + barrelToggle` (`FUN_004b6820` computes the index from `+0x604`/`+0x4c4`). Slot
layout: `+0x00` muzzle handle (0 = unused), `+0x04` smoothed direction, `+0x10` target direction,
`+0x1c` last-update time, `+0x20` flags. Both directions are **plane-local**; `FUN_004b3e50` resets
the target to `(0, 0, −1)`. The per-frame pass (`FUN_004b3e50`, called from `FUN_004897c0`, run for
the local player only — which in CSVM means **every human pilot**, see Decision 7, not pane 1)
does two things: if `lastUpdate + forget_interval < now`, reset target to local
forward and push `lastUpdate` to `now + forget_interval`; then slerp smoothed toward target by
`catchup_rate × dt`, snapping outright at `≥ 1.0`.

**Approach.** CSVM already has the index the original computes: `FireControl.cs:241` emits
`(gi, st.NextMuzzle % g.MuzzleCount)` into `outcome.GunShots`, which is exactly
`weaponGroup · 2 + barrelToggle` with the barrel alternation already done. Hang the slot array off
`FlightController` beside `_firableGuns` (`:378`) and tick it in the dt-driven fire block at
`FlightController.cs:955`, immediately **before** `ApplyFireOutcome(_fire.Step(dt, fireInputs))` —
the forget-and-catch-up pass must run on the pre-shot state, since `FUN_004b6530` restamps
`lastUpdate` on every round that goes out. Hold both vectors in the aircraft's local space and
convert only at the fire call (B5). The slot state itself belongs in the new `src/Flight/AimAssist.cs`
that B3 creates, so it is testable without a `FlightController`.

**Model recommendation.** medium. Well-specified state machine; the only trap is the coordinate
space, and it is named.

**Verify.** A new `TestHarness.Suite` registered in `Suites.cs` beside `weapons-fire` (`:121`) and
`air-to-air` (`:130`) — call it `aim-assist`, and let B3/B4/B5 add their cases to the same suite.
Seed a slot's target off-axis, step at a fixed dt, and assert the smoothed direction reaches it on a
~0.2 s time constant and snaps in one step when `dt ≥ 0.2 s`. A second case that the forget timer
measures time since the last *shot*: fire, wait `forget_interval`, assert the target has unwound to
local forward; fire continuously, assert it never does.

**⚠ Traps.** Smoothing in world space is a different feel and the wrong port — the assist must lag
your own roll. `forget_interval` counts from the last shot, so it never expires while the trigger is
held; a "time since lock lost" reading is wrong. The snap at `catchup_rate × dt ≥ 1` means any
frame longer than 200 ms fully snaps the gun line — that is real hitch behaviour, not a rounding
detail to smooth away.

## B3 ☐ The constant-velocity intercept solver

**Goal.** Given a muzzle position, projectile speed, a target's position and the relative velocity,
return the direction to fire and the time of flight, or "no solution".

**Evidence (confidence: traced).** `FUN_00460e30`. Solves for `u = 1/t` in
`|displacement|²·u² + 2·(relVel · displacement)·u + (|relVel|² − speed²) = 0`, taking the root as
`a / (b ± √disc)` rather than `(−b ± √disc) / 2a` — the numerically stable form. Returns
`aimDir = normalize(relVel + displacement / t)` and `t`. Returns 0 (no intercept) on zero
separation or a negative discriminant, so a target outrunning the round is simply not assisted.
The velocity fed in is **relative**: target velocity minus the player's.

**Approach.** A new `CSVM/src/Flight/AimAssist.cs`, holding this solver as a static method with no
engine dependencies. A new file rather than an addition to `Ballistics.cs`: that file is the
round's *flight* model (VELOCITY/ACCELERATION/GRAVITY integration) and this is launch-direction
selection, which is the distinction the whole plan turns on. `AimAssist.cs` then also carries B2's
slot state and B4's scorer, so the assist is one testable unit that `FlightController` calls into.

**Model recommendation.** medium. Closed-form maths with the exact formula given.

**Verify.** Unit tests: a stationary target dead ahead (aim direction equals the displacement
direction, `t = distance / speed`); a crossing target (aim leads it, and integrating the round for
`t` puts it where the target will be); a target receding faster than the round (no solution).

**⚠ Traps.** The original's square root is the `(x >> 1) + 0x1fc00000` bit-trick approximation, so
its lead is accurate to roughly a per cent. Use a real `sqrt` — do not reproduce the approximation
— but do not treat a sub-per-cent disagreement with a hand-computed reference as a bug in either.
Do not "fix" the unstable-root form into the textbook quadratic; the stable form is what the engine
uses and the difference shows up at long range.

## B4 ☐ The candidate scan, rejection gates and scorer

**Goal.** Given a firing muzzle and weapon, pick the target the original would pick, or none.

**Evidence (confidence: traced).** `FUN_004b6530` builds a scan context (muzzle position, weapon
def, best-score cell seeded to `−FLT_MAX` = `0xff7fffff`, best-direction cell aliasing the slot's
target field) and runs four byte-identical scorers over four lists:

| List | Scorer | Contents |
|---|---|---|
| `DAT_0071dabc` | `FUN_004bae60` | `VehicleList` — aircraft and AI ground/sea vehicles |
| `DAT_0071d914` | `FUN_004bb110` | Turrets |
| `DAT_0071d33c`…`DAT_0071d340` | `FUN_004bb3b0` | `MStructList` — `targets.zrd` mission structures |
| `DAT_0064f78c` | `FUN_004bb660` | Live proximity-fused ordnance in flight |

Rejections, in order: the candidate is the local player; a virtual predicate at vtable `+0x14`
returns true (dead / not live); **same team** (`+0x08` matches, or either is 0); no intercept;
`speed² · t² > RANGE²`; outside the cone. The cone cosine is the target's `+0x50` if `≥ 0`
(a half-angle in radians, tested as `−cos`), else the weapon's `[def+0x210]+0x08` =
`−cos(CANNON_SPREAD°)`. Survivors rank by `score = alignment − distance × dist_factor`, alignment
being the dot of the intercept direction with the plane's forward axis. Highest wins; the first
survivor always beats the `−FLT_MAX` seed.

**Approach.** One scorer, run over whichever candidate collections CSVM has. Surveyed 2026-08-13,
the four lists map like this — **two are genuinely empty today, and that is a coverage gap, not a
decision**:

| Original list | CSVM equivalent | State |
|---|---|---|
| `VehicleList` (aircraft + AI ground/sea vehicles) | the live `FlightController`s | **exists** for aircraft. AI ground/sea vehicles do not exist (no AI until M4). |
| Turrets | — | **empty.** Turrets are M4; `docs/SCOPING-M4-ai.md` scopes them. |
| `MStructList` (`targets.zrd` structures) | `Mech3.DestructibleRegistry` | **approximate.** The registry is the shootable-world-object list (`Suites.cs:214`, `Probes.cs:537`). `MissionTargets` is **not** the analogue — it holds objective display strings, nothing damageable. Record the approximation rather than implying a clean mapping. |
| Live proximity-fused ordnance | the live `ProjectilePool` | **exists**, as a filter, not a new structure. |

For the ordnance filter, the original registers a tracking record in `FUN_00441830` after **every**
projectile spawn when the def's secondary-block flag `0x20` is set or its `+0x44`
(`DETONATION_DISTANCE²`) exceeds 0.01 — a proximity fuse longer than 0.1 m, the 13 ordnance
carriers in `formats/weapons.md`. In CSVM that is
`weapon.DetonationDistance > 0.1f` (`WeaponDefs.cs:87`) over the live pool.

Write the scorer so an empty list is *empty*, not *absent*: the turret pass should exist and
iterate nothing, so M4 wiring a turret list in is one line and not a rediscovery of this item.

**Model recommendation.** high. The most judgement-heavy item: it decides what the assist may snap
onto, and each list it silently omits is a behaviour change nobody will notice.

**Verify.** Cases in the `aim-assist` suite, each proving its gate can fail: a same-team target is
rejected; a target beyond `RANGE` is rejected; a target just outside the cone is rejected and just
inside is accepted. A selection case with `dist_factor 0`: a distant on-axis target beats a near
off-axis one.

The rocket-snap check is **cheap in-engine and should be written, not deferred** (assessed
2026-08-13). The harness already does every piece of it: `air-to-air` (`Suites.cs:130`) builds two
live `FlightController`s and fires real rounds between them, and the torpedo case at `Suites.cs:1970`
already spawns `wep_14` into a pool and steps it. So: spawn a proximity-fused round on a closing
track toward the shooter, run the scan, and assert the winner is that round and not the aircraft
behind it. No new rig, no AI needed — the ordnance does not have to be fired *at* anyone by anything
intelligent, it only has to exist in the pool.

**⚠ Traps.** `dist_factor 0.0` deletes the distance term entirely; selection is purely
most-aligned, and "fixing" that during tuning reintroduces the executable's `2.5e-4` default the
shipped data deliberately turns off. Team id 0 on *either* side rejects the pair. Scoping the
candidate set to aircraft is a silent behaviour change, not a simplification. Build the `+0x50`
override even though nothing ships a value (Decision 2), and note the sign convention: the engine
tests `dot < coneCos` with both sides negated, which is the same relation as the positive-alignment
form used in this plan — do not mix halves of the two conventions.

## B5 ☐ Wire the assist into the fire call, apply the 1° scatter, and decode what the reticle follows

**Goal.** A round is fired along the slot's *smoothed* direction, scattered inside a 1° cone, and
the scan result updates the *target* direction for later frames. The gun reticle agrees with where
rounds actually go, in whatever way the original makes it agree.

**Evidence (confidence: traced).** `FUN_004b6530`'s step order, which is asymmetric on purpose:
seed the slot's target with the plane's forward axis and stamp `+0x1c` with the current time (the
"no target found" answer); run the scan; rotate the winner world→local into the **target** field;
rotate the slot's **smoothed** direction local→world as the output; apply the scatter. What gets
fired is the smoothed value from previous frames, not this frame's scan result. The scatter
(`FUN_00460940` → `FUN_004608a0`): build any perpendicular to the aim direction, rotate it about the
aim axis by `rand()/32767 · 2π`, then rotate the aim direction about that perpendicular by
`rand()/32767 · inaccuracy`.

**Approach.** `FlightController.cs:1433` currently passes `muzzle.GlobalTransform` straight to
`Projectiles.Spawn`. Compute the assisted direction there and pass it alongside, rather than
letting `Spawn` derive the direction from the transform's basis. Write the scatter as a new helper
in `AimAssist.cs`; do not reuse `ApplySpread` (see A1's traps). The rocket call at `:1445` gets
**no** assist.

**The reticle is a decode, done inside this item, before the HUD is touched.** `UpdateReticle`
(`FlightController.cs:1368`) averages the selected group's muzzle forwards (`:1401`–`:1413`) and
ballistically projects that to `GunConvergenceDist` — so today it marks the unassisted nose axis.
Once the assist lands, rounds leave along the smoothed per-muzzle line instead, and the pipper and
the rounds stop agreeing, which is exactly the property `:1364`–`:1367`'s doc comment claims. Do
not design a fix: read out of `crimson.exe` what the original's gun pipper actually tracks (the
raw hardpoint axis, the smoothed slot direction, or the selected target), then match it. Start from
`FUN_004b3e50`'s consumers and the HUD draw path; the assist slots at plane `+0x3a4` are the thing
to look for a second reader of. Record the answer in `docs/org/aim-assist.md` with its addresses,
the same as the rest of that page, and land the HUD change with it.

**Model recommendation.** high. This changes the fire path's contract, carries a decode, and is
where the plan's parts meet.

**Verify.** At the controls in `--vs`: fly past a target off-axis and confirm the gun line leans
toward it and lags a hard roll. In the `aim-assist` suite: fire N rounds through the scatter helper
from a fixed direction and histogram the polar angle, asserting it is flat across `[0, θ]` rather
than rising toward the rim — the same measurement A1 uses for its baseline, pointed at the new
helper. Reticle: whatever agreement the decode says should hold, checked at the controls against
where rounds land. Full 8-chapter `--freecam` regression, zero errors.

**⚠ Traps.** Firing this frame's scan result instead of the smoothed direction removes the lag
entirely and reads as an aimbot — the asymmetry is the whole feel. The polar angle is uniform in
`[0, θmax]`, **not** uniform over the cone's solid angle; the reflex sampling puts noticeably more
shots near the rim. On the reticle: "make the pipper follow the assisted line" is the plausible
answer and may well be wrong — the original may deliberately leave the pipper on the nose so the
assist stays invisible. Decode it; do not pick it. And if the decode comes back inconclusive, say so
and leave the reticle alone rather than guessing, since a wrong pipper teaches the pilot to aim
wrong.

## B6 ☐ Who gets the assist, and the shooter-authoritative invariant for later

**Goal.** Every human pilot's guns are assisted, in splitscreen as in single player; AI planes will
not be. When networking is built, the assist is already shaped so that a remote plane's rounds go
where the shooter's own machine decided they go, and nobody re-derives that rule from the binary.

**Deferred to a networking milestone, deliberately.** CSVM has **no networking**: `rpc` / `Rpc` /
`MultiplayerApi` / `ENet` match nothing under `CSVM/src/` (checked 2026-08-13). `--vs` is
splitscreen on one machine, so there is no wire and nothing to transmit over it. Building a
transmission path now would be untestable code guarding a case that cannot occur. What this item
delivers instead is the two things that are cheap now and expensive to recover later.

**Evidence (confidence: traced).** If the network flag is set (`FUN_00440ad0` returns
`DAT_0064f750`, set at `0x00440247` from the `"Network"` subsystem lookup at `0x006237f4`),
`FUN_004b6530` caches its result at plane `+0x704`. Remote planes' shots read `+0x704`–`+0x70c`
verbatim and never run the scan. The assist is computed once, by the shooter. Independently of the
network flag, the scan itself only runs for `param_1 == DAT_0071c298`, the local player; every
other plane takes the AI branch.

⚠ **`param_1 == DAT_0071c298` does not mean "player one".** The original has exactly one local
player — it has no splitscreen at all, which is why `PT-43` is tagged `[Own]` — so that test is
doing two jobs at once: *is this plane the human's*, and *is this plane the only local one*. Only
the first job generalises. **In CSVM's splitscreen every pane is a human pilot and every pane gets
the assist.** The condition to port is "human-piloted", and it is `FUN_004b6530`'s own else-branch
that says so: everything that is not the local player falls through to the AI path, which reads the
hardpoint's authored direction and perturbs it by the plane's `+0x95c` dead-eye scalar. The split
is human-versus-AI, not first-player-versus-everyone.

**Approach.** Two deliverables, both prose plus one condition:

1. **The human-piloted condition at the B5 call site** — the assist runs for a plane a person is
   flying, and AI planes take the dead-eye path instead. Today CSVM has nothing but human pilots,
   so this is a no-op that changes no behaviour; it bites when M4 lands AI aircraft, and writing it
   at the call site now costs one branch and stops M4 from silently giving AI planes a human's
   aim assist.
2. **The networking invariant, written down** where a future milestone will hit it: the assisted
   direction is a *value* produced at the fire call and consumed by `Spawn`, so a remote shot is
   fed a received vector rather than re-running the scan. Keep the B5 signature shaped that way
   (direction passed in, never derived inside `Spawn`) and note it in `docs/architecture.md`'s
   `Projectile.cs` section and in `docs/org/aim-assist.md`, which already records the mechanism.

**Model recommendation.** medium, low effort. One branch and two prose notes.

**Verify.** In splitscreen `--vs --players=2`, confirm **both** panes' guns are assisted — this is
the check that catches the misreading above. The AI exclusion cannot be verified until AI planes
exist, and the transmission behaviour cannot be verified until there is a network layer; say so
rather than claiming coverage.

**⚠ Traps.** Reading `DAT_0071c298` as "player one" and gating the assist on `PlayerIndex == 0` is
the wrong port and would silently leave panes 2–4 firing unassisted, which is very hard to notice
from inside pane 1. Re-running the scan per client is the obvious future networking implementation
and is also wrong: each client has slightly different state and the rounds diverge. If a later
milestone derives the vector locally "because it is cheap", the invariant above is lost.

# Wave C — Judge it

## C7 ☐ `PT-43` gun-feel pass in `--vs`, and hand the strength question back to `BL-301`

**Goal.** A judgement, at the controls, on whether guns are now a practical kill weapon without
rockets — and a recorded answer either way.

**Evidence (confidence: lead-only).** `PT-43` and `BL-301` record that gun kills are impractical
and everything leans on rockets. That was the symptom this plan's mechanism explains. Whether the
fix reads right is a live-cockpit call and cannot be settled from the binary.

**Approach.** Fly `--vs`. Record the outcome against `PT-43`'s gun-feel line. If it reads as
over-assist, that is `BL-301`'s aim-assist-strength item, **not** a licence to retune the constants
here — they are the original's answer (Decision 4).

**Model recommendation.** None — this is the user's own playtest, not an agent task. `PT-43` is
tagged `[Own]` in `playtest.md:203` precisely because no original-game reference exists for
splitscreen dogfight. It is the plan's **exit condition**: the plan is not complete until this is
flown and the outcome recorded, and an agent's job here ends at making it flyable.

**Verify.** `playtest.md:203`'s `PT-43` procedure, launched with
`./RunGame.ps1 --vs --players=2 --chapter=C1` (`playtest.md:200`); the gun-feel line is item (a),
the plane-vs-plane damage balance measured at 18 rounds of `wep_00` in the suite.

**⚠ Traps.** The honest risk named in `BL-342` is over-assist reading as aimbot. Tune only against
footage, never against taste. A "it feels too strong" judgement with no original-game comparison is
a `BL-301` note, not a code change.
