# Milestone 3 polish, run 2 — combat look & feel

**ACTIVE PLAN** (written 2026-07-30). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans.md), when every item lands.

Ten items drawn from `backlog.md` under the criteria: **M3 weapons/destruction fidelity, already
root-caused or with a stated fix shape, not blocked on owed `CAP-nn` captures or future milestones
(M4 AI, menu hub, cutscenes).** Each was re-verified still-open on 2026-07-30 against
`docs/plans/PLAN-m3-polish-quickwins.md`'s landed list (BL-024/025/026/031/040/043/044/049/139/140/159)
and the `m3-polishing` merge into `main` (BL-001–006, all ancestors of `main`); per-item Evidence
below still gets confirmed against the code before implementing, per Ground rules.

Four additions by user direction (2026-07-30): `BL-042` (collider-overlay wireframe offset —
the overlay is `BL-041`'s acceptance instrument, so it must draw true first), `BL-014` (weapon-lab
synchronous volley — minor, but the lab is the test bench for every Wave C item), and
`BL-011`/`BL-012` (muzzle-flash and tracer look — weapon visuals, accepted as cockpit-A/B-gated:
they land code + a re-measured tune but only *close* on the user's playtest).

Out of scope, deliberately: explosive radius and the armour layer (`BL-086`/`BL-085` — plan-sized
combat-model work coupled to M4 questions), and everything blocked on an owed capture
(`playtest.md` §0).

## Milestone goal

- Proximity-gated `OnStartup` animations fire when the data says, not at frame 0 — the C3
  spiderweb reads solid until approached (`BL-183`).
- Destruction and damage feedback renders in real flight: stage smoke/fire on world destructibles
  (`BL-021`), the player's own low-HP trail (`BL-174`), and the `kkgate` door actually dies
  (`BL-023`).
- Impacts answer with the right surface: water splashes exist (`BL-017`), buildings don't sound
  wet (`BL-041`), dirt throws tumbling debris instead of one fat spark (`BL-018`) — and the
  collider overlay used to verify all of it draws where the colliders actually are (`BL-042`).
- Guns and rockets carry their authored secondary visuals: the rocket smoke trail (`BL-015`),
  casing ejection with the muzzle puff/light (`BL-013`+`BL-137`/`BL-138`), and the muzzle-flash
  and tracer look re-measured and retuned toward the reference shots (`BL-011`/`BL-012`); the
  weapon lab fires mounts alternately like flight so it can be the test bench (`BL-014`).
- The `det == 0` invert error on multi-death sweeps is diagnosed to a mechanism (`BL-007`).

**No new combat mechanics land in this plan.** Blast radii, armour pools, AI, and anything that
changes damage *numbers* rather than damage *presentation* stays in the backlog — this run is
about what the player sees and hears when the existing mechanics fire.

## ⚠ Read this before implementing anything

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1 (BL-183), C22 wiring (BL-137/138), C23 (BL-014) | Confirm the trace, then implement. |
| **Direction sound, mechanism to confirm on-site** | A2 (BL-021), A3 (BL-174), B11 (BL-017), B12 (BL-041), B13 (BL-018), B14 (BL-042), C21 (BL-015), C22 look (BL-013) | The *what* is settled; confirm the routing/anchor before coding, and mark magnitudes TUNE. |
| **Direction sound, magnitude a cockpit judgement** | C24 (BL-011), C25 (BL-012) | Re-measure the live build, retune toward the reference shots, then the item stays ◐ until the user's A/B closes it. |
| **Leads only — may end in a disproof** | D31 (BL-023), D32 (BL-007) | Budget for investigation; a correct disproof is a valid landing. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`CLAUDE.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
  landed item; a landed item gets a dated entry in `docs/HISTORY.md` and is **deleted** from
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

### Wave A — animation triggers & effect routing

1. ☑ `BL-183` Parse `EXECUTION_BY_RANGE` and gate `OnStartup` defs on player proximity (C3 spiderweb)
2. ☑ `BL-021` Damage-stage smoke/fire renders in flight, not only headless
3. ☐ `BL-174` Player low-HP smoke/fire trail: settle reachability-vs-parenting, then fix

### Wave B — impacts & surfaces

11. ❌ `BL-017` Sea surface gets a projectile-layer collider so water impacts exist — disproven 2026-07-30: the sea already collides, tags `water`, splashes and sounds; follow-up `BL-186` (splash imperceptible)
12. ◐ `BL-041` Fix the surface-classification polygon vote (buildings tagged water and vice versa) — landed + measured (census + scripted impacts); the at-the-controls overlay read stays owed until B14 draws true
13. ☐ `BL-018` Dirt impacts: small tumbling-debris burst instead of the 3 m spark
14. ☑ `BL-042` Collider-overlay wireframes hug their geometry (fix the shared-axis offset)

### Wave C — weapon secondary visuals

21. ☐ `BL-015` Rocket smoke trail (the authored FLYOUT trail, orange→grey)
22. ☐ `BL-013` Casing ejection + muzzle puff/light (`BL-137` per-shot anchors, `BL-138` puffer+light)
23. ☑ `BL-014` Weapon lab fires a group's mounts alternately, like flight
24. ☐ `BL-011` Muzzle flash: re-measure the live build, retune size/look toward the reference shots
25. ☐ `BL-012` Tracers: re-measure, retune toward short yellow dashes; settle the additive bloom

### Wave D — destruction bugs

31. ☐ `BL-023` `kkgate` door: dies visibly and loses its collider in the live world
32. ☐ `BL-007` Diagnose the `det == 0` invert error on multi-death sweeps

## Dependency and parallelism notes

A2 and A3 both concern puffer routing/parenting around the world-effects runtime — land them
sequentially (A2 first; its findings about the flight `EffectSink` path feed A3's hypothesis B).
A1 is independent of both. **B14 lands before B12's playtest verification** — the collider
overlay is B12's acceptance instrument, and it must draw true before anyone reads it (user
direction, 2026-07-30). File contention: B11, B13, C21, C22, C24 and C25 all touch
`Projectile.cs` — never run two of them in parallel worktrees; suggested order
B11 → B13 → C21 → C22 → C24 → C25. C23 is `WeaponLab.cs`-only and should land **first in
Wave C** — it makes the lab a faithful bench for judging C22/C24/C25. C24 and C25 close only on
the user's cockpit A/B — expect them to sit ◐ across sessions. B12 is `SceneBuilder.cs`-side and
independent; B14 is `ColliderOverlay`-only. D31 and D32 are both destruction-path investigations on
C2 and may share a root cause — run D31 before D32 and cross-check, but per `BL-023`'s trap they
are *presumed separate*; do not close one as the other. A3 (Puffer parenting) and D32 (Puffer
`GlobalPosition` suspect) touch the same module — sequential, and whichever lands second re-runs
the other's verification.

---

# Wave A — animation triggers & effect routing

## A1 ☑ `BL-183` Parse `EXECUTION_BY_RANGE`, gate `OnStartup` defs on proximity

**Goal.** A proximity-gated `OnStartup` def (the C3 spiderweb's `spiderweb_gone`, 50 m radius)
fires only when a player is inside its authored range — the web renders solid and collidable from
every spawn, fades + drops colliders only on close approach.

**Evidence (confidence: traced).** `AnimDefinition.Parse` (`CompiledAnim.cs:224-262`) never reads
the compiled `execution: {ByRange: {min, max}}` field; `AnimRuntime.RunAmbientPasses`
(`AnimRuntime.cs:1309-1338`) runs every `OnStartup` def unconditionally. Range is squared metres
(2500 = 50², convention at `docs/formats/anim-definitions.md:258`). Full trace, spawn-distance
survey and fix shape: `backlog.md` `BL-183`. Closing this also closes `BL-006`'s premise reversal.

**Approach.** (1) Parse `Execution`/`Range` into `AnimDefinition`; (2) in `RunAmbientPasses`, defer
range-carrying `OnStartup` defs to a live nearest-player distance check, re-evaluated on the same
cadence `MapEdgeExtender` uses for cell-crossing diffs — not per frame; (3) keep
`fix/opacity-fade-collider`'s collider-drop-on-fade exactly as is.

**Model recommendation.** fable — general mechanism touching every chapter's bootstrap; the
blast radius (which defs stop firing at t=0 install-wide) needs judgement.

**Verify.** Fly C3 from a normal spawn: web solid until ~50 m, then fades over 0.7 s and stops
colliding (`./RunGame.ps1 --chapter=C3 --plane=player_bhawk`). 8-chapter freecam regression, plus
enumerate which other defs newly defer (C1 `cloudparent#`, C5 `wl_glw`/`cfglow`, C3/C4 barrage
balloons) — verify by what *changes*, not by what looks right.

**⚠ Traps.** (a) Do not special-case `spiderweb` by name — the mechanism is general. (b) Do not
revert the fade or the collider drop; only the trigger timing is wrong. (c) The golden-image
hashes may legitimately change on chapters where a deferred def altered frame-15 state — read
`docs/verification.md` before re-pinning.

## A2 ☑ `BL-021` Damage stages (smoke→fire) render in flight

**Goal.** Guns-only fire into a destructible in real flight shows the authored 60 %/30 % stage
smoke and fire on the object, not just the final blast.

**Evidence (confidence: direction sound).** The stages fire headless (`--damage-hd` proves them),
so this is a render-routing gap: the `DAMAGE_SEQUENCE` stage effects aren't reaching the flight
world-effects runtime. Same class as the D32 gun-smoke follow-up (`BL-061` item 1 context: the
puffer factory is torn down post-build outside the labs). To confirm on-site: does the flight
damage path (`AnimRuntime.DamageAt` → stage sequences) route stage effects through the flight
`EffectSink`/world-effects runtime, or only the death effect?

**Approach.** Trace one stage effect's dispatch in a live flight session (`--log=anim`) from
`DamageAt` to whatever should build its `Puffer`; wire the missing routing the way the death
effect already routes. Note the adjacent, *separate* limit `BL-046` (the fixed 28-name
`EffectStageRoots` set) — if the tower's stage puffers die there instead, that is a scope call to
surface, not silently absorb.

**Model recommendation.** fable — routing diagnosis across `AnimRuntime`/`WorldEffectsFactory`
seams with a known adjacent trap.

**Verify.** `./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`, guns into a tower with HP
falling: smoke at 60 %, fire at 30 %. Confirm a `Puffer` was actually *built*, not just a def
started (`docs/verification.md` WORLD-12 — a started def that draws nothing is a no-op measured
as success).

**⚠ Traps.** (a) `--run-tests`/headless passing says nothing here — the gap is flight-only.
(b) Do not conflate with `BL-046`'s 28-name closure limit; if that is the real blocker, report it
and re-scope rather than widening the set ad hoc.

## A3 ☐ `BL-174` Player low-HP smoke/fire trail renders in flight

**Goal.** A plane held in the low-HP band shows its `player_smoketrail` smoke/fire pair in real
flight before it is destroyed.

**Evidence (confidence: direction sound, two competing hypotheses).** A — reachability: graze
damage (`GrazeMaxDamage` 18) can zero the smallest 15 HP zone in one hit, so flight may never
linger in the ≤0.10 HP band. B — parenting: `FlightRigAssembler.cs:228-229` parents the puffers
under the moving per-player controller, the exact anti-pattern `WorldEffectsFactory.cs:228-233`
documents as already diagnosed for the crash rig (drawn-but-unrendered). "Works in the lab" is
consistent with both. Full argument: `backlog.md` `BL-174`.

**Approach.** Discriminate first: a damage-lab run holding HP in the 10–20 % band (the lab builds
its puffers at a different site, `GameSession.cs:915-916`). If B: re-parent to the world root the
way the crash rig is. If A: raise the threshold or lower `GrazeMaxDamage` relative to the smallest
zone's `MaxHp` — and record whichever magnitude moves as TUNE.

**Model recommendation.** sonnet — the discriminating instrument is already named; the fix in
either branch is small.

**Verify.** The lab discrimination run, then a live shallow multi-hit scrape confirming the trail
renders before destruction (also covers the torn-panel flips, `CAP-15`'s look half stays owed).

**⚠ Traps.** (a) A flight test cannot distinguish "never reached" from "reached but invisible" —
lab first. (b) Not `BL-046`/`BL-061`: `DamageVisuals` never touches `EffectStageRoots`. (c) The
`Puffer` code itself is proven in the lab — the defect is flight-path-specific.

# Wave B — impacts & surfaces

## B11 ❌ `BL-017` Water impacts: sea gets a projectile-layer collider

**Outcome (2026-07-30): disproven — no code.** The C1B sea is already fully collidable and
`water`-tagged: six `--pos`/`--hold`/`--fire` dive probes across the map (including the mission spawn
and 2 km past the map edge, on extension tiles) each logged 8/8 `impact: … -> Water` on distinct sea
tiles, instanced `splash1.flt` and selected `snd_water_bullet`. The plane does NOT fall through to the
under-map backstop either — a sea dive logs `CRASH into g28178/col` (classified Ground, BL-059's known
gap), so the "must not change" constraint described a behaviour that never existed. The user's
"nothing at all" has two measured mechanisms: gun rounds expire silently at RANGE = 1000 m (4 s of
fire at 1380 m slant → zero impacts — the same false negative that produced this item's premise, now
verification.md METHOD-18), and the landed splash covers 0–12 px at 300 m (`--tex-census=splash`,
SHOT-14). The splash *look* is the real gap → `BL-186`.

**Goal.** Gun and rocket rounds striking the sea produce a hit — splash effect and sound — instead
of passing through.

**Evidence (confidence: direction sound).** Hits fire only on a raycast collider strike
(`Projectile.cs:416`); the sea has no collider. The `SurfaceMeta` classifier already keys off a
`water` tag (`Projectile.cs:544`). Target look: small white `Splash0N` sprites
(`OriginalScreenshots/Water Splash.png`).

**Approach.** Give the sea surface a `water`-tagged collider on a **projectile-only collision
layer**, so `ProjectilePool`'s raycasts register while the plane's crash/graze queries do not —
a water crash today deliberately falls through to the under-map backstop, and that behaviour must
not change here (the water crash variant is `BL-059`'s, out of scope).

**Model recommendation.** fable — collision-layer scoping interacts with the crash system; a wrong
mask turns every sea-skim into a crash.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1B` (over water): splash sprites +
sound on water gunfire; then confirm a deliberate sea-skim still respawns via the backstop, not a
crash. 8-chapter freecam regression for collider-count drift.

**⚠ Traps.** (a) Do not let the new collider feed `ClassifySurface` for *crashes*. (b) The splash
look magnitude is TUNE — the mechanism is the deliverable. (c) `BL-041`'s mis-tagging interacts:
if B12 hasn't landed yet, a correct sea collider can still classify wrong — verify tag, not just
hit.

## B12 ◐ `BL-041` Fix the surface-classification polygon vote

**Goal.** A C2 building answers gunfire with ricochet/debris, water with a splash; no building
sounds wet. `ColliderOverlay` colours agree with reality.

**Evidence (confidence: direction sound, measured).** `SurfaceForMesh` votes over a mesh's
polygons but skips unclassified ones, so a texture-name minority wins: 96 of C2's 190 tagged
meshes (51 %) are tagged on a minority of their own polygons, thinnest `water` on 1 of 76.
Numbers + replication script: `analysis/surface-classification/`.

**Approach.** Make the vote defensible — e.g. count unclassified polygons as abstentions with a
quorum, or weight by polygon area — *measured* against the analysis script's census before/after,
not eyeballed. `--tex-census` is the instrument for checking which textures actually cover a
surface.

**Model recommendation.** fable — the vote redesign is a judgement call with measured evidence to
honour; a naive pattern-widening is the documented trap.

**Verify.** Re-run `analysis/surface-classification/` and show the minority-tag count collapse;
then `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire` + **C** with `--collision` — the
playtest acceptance is `BL-041`'s own: no splash from buildings, splash from water.

**⚠ Traps.** (a) The `soil` field is not the answer — MW3 leftovers, near-all `Default`. (b) The
design document does not describe the mechanism — already searched, dead end. (c) Do not widen
name patterns without measuring; the *vote* is what is broken. (d) The overlay was the instrument,
not the defect.

## B13 ☐ `BL-018` Dirt impacts: tumbling-debris burst

**Goal.** A gun/rocket dirt impact shows a few small, randomly-rotated, tumbling debris sprites
with a short arc — not one 3 m orange billboard.

**Evidence (confidence: direction sound).** Dirt currently falls to the stand-in spark
(`ImpactSize = 3 m`, `Projectile.cs:66,515`). Original reference: `Dirt Splash.png` ("not
billboards — rotating randomly").

**Approach.** A small dirt-debris burst in the existing sprite path (`RenderSprites`): a few
sprites, random roll, short ballistic arc, brief life. Count/size/arc are TUNE — record them in
`backlog.md`'s TUNE list on landing.

**Model recommendation.** sonnet — contained sprite-effect work in one file with a stated shape.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C2`, gunfire into dirt; screenshot A/B
against `Dirt Splash.png`. Confirm building/water impacts unchanged.

**⚠ Traps.** `BL-019` (building vs dirt effects identical for HE rockets) is a *separate*
classification/lookup question — do not fold it in; this item is the gun-impact sprite look only.

## B14 ☑ `BL-042` Collider-overlay wireframes hug their geometry

**Goal.** The **C** collision overlay draws every wireframe where its collider actually is — on
world nodes, clutter, and the plane's own boxes alike — so it can be trusted as the acceptance
instrument for B11/B12.

**Evidence (confidence: direction sound).** Seen in the D35 overlay: wireframes sit offset from
their meshes, all on the *same axis*. The user confirmed in flight that collision itself is
correct (the plane flies through the drawn wireframe where the real collider is not) — a
rendering-transform bug in `ColliderOverlay`, not a collision one. A single shared axis points at
one wrong frame (local-vs-world or parent-transform composition), not per-shape noise.

**Approach.** Inspect how `ColliderOverlay` composes each shape's transform. Check the two draw
paths separately before assuming both are wrong: node-backed shapes vs clutter shapes drawn via
`PhysicsServer3D.BodyGetShape*` (no nodes). The plane-collider boxes are a third path again.

**Model recommendation.** sonnet — a contained transform-composition bug in one overlay module.

**Verify.** `./RunGame.ps1 --freecam --chapter=C2 --collision=show`: wireframes hug geometry on
**all three paths** (world nodes, clutter, plane boxes) — check each, not just the one fixed. A
before/after `--screenshot` pair at the same `--pos` makes the offset's disappearance objective.

**⚠ Traps.** (a) Do not verify by eye on one path and declare all three fixed — they are separate
code paths. (b) The colliders themselves are correct; do not "fix" collision to match the
wireframe. (c) Land before B12's playtest read of the overlay.

**Revisit (2026-07-31, `BL-198`).** The user refuted the first pass at the controls: the real
mechanism was the anti-z-fight `Inflate` scaling trimesh vertices about the local origin — a
position-proportional shift (~20 m at a map corner) on world-baked-vertex trimeshes, invisible in
the first pass's near-origin close-ups. Fixed to scale about the faces' AABB centre; all five draw
paths (world nodes, clutter, plane boxes, wreck swap, edge tiles) captured hugging their meshes.
See `docs/HISTORY.md` 2026-07-31.

# Wave C — weapon secondary visuals

## C21 ☐ `BL-015` Rocket smoke trail

**Goal.** Rockets trail the authored thick smoke fading orange→grey — the original's dominant
rocket visual (`Rocket Streak 1..3.png`), with per-type character (HE intermittent white, flak
continuous black, incendiary red-hued, sonic sine).

**Evidence (confidence: direction sound).** A MODEL-body rocket gets only the slim
`RocketExhaustScale = 0.5` exhaust streak (`Projectile.cs:62`). The gap is the deferred
`MODEL_ANIMATION` FLYOUT trail. Read the per-weapon FLYOUT defs in the extraction before building
anything — the trail's shape/textures are authored data, not invention.

**Approach.** Survey what each rocket type's FLYOUT def actually ships (textures, emit cadence,
fade), then implement the trail in the projectile render path — likely a per-round emitter
following the rocket, reusing the existing sprite/puffer machinery rather than a new system.

**Model recommendation.** fable — starts with a data decode and ends in look-critical rendering;
the biggest single visual item in the plan.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1`, fire each rocket type; A/B
screenshots against `Rocket Streak 1..3.png`. Per-type distinctness is the acceptance, not just
"a trail exists".

**⚠ Traps.** (a) The "rockets feel too fast" impression is attributed to the missing trail —
treat `weapons.rocketSpeedScale` as probably-neutral; do not retune speed here. (b) New decodes
land with their `docs/formats/` page, same change.

## C22 ☐ `BL-013` Casing ejection + muzzle puff/light (`BL-137`, `BL-138`)

**Goal.** Firing guns ejects a brass casing (the authored `gunshell` motion: fall + 20.94 rad/s
tumble over 2 s) plus an aft-drifting white puff cluster from the wing mounts; each shot also gets
the authored muzzle smoke puff and a real dynamic light flash.

**Evidence (confidence: traced for the wiring blockers, direction sound for the look).**
`gunshell.zrd.json` is a complete ON_CALL def and the `gunshell` root's child `g1` carries a real
mesh (model 60) — see `BL-090` item 4 and the corrected `docs/formats/weapon-effects.md` footnote.
Two named wiring blockers: `BL-137` — `AnimRuntime.CallAnimation`'s per-anchor already-live gate
(`AnimRuntime.cs:1670-1712`) vs `RUN_TIME 2` means a shared anchor drops nearly every ejection;
`BL-138` — the `muzzle_burst` def's `muzzlepuffer` puff and `muzzle_lt`/`bigmuzzle_lt` lights are
entirely unbuilt (`Projectile.cs:53,202` is a plain quad).

**Approach.** Per-shot anchors: a small transient-anchor pool the way `ProjectilePool` already
pools tracers — not a bare call onto the shared `gunshell` root. Muzzle: a short-lived
aft-drifting puff via the existing `Sprite`/`RenderSprites` path plus an `OmniLight3D` flash using
the def's range/colour values. The white puff cluster's effect def is still unlocated — if it
stays unmatched, a hand-authored stand-in is a recorded TUNE, not silent invention.

**Model recommendation.** fable — three coupled sub-systems (anchor pool, anim-call gate, light
flash) with authored-data fidelity constraints.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo`; A/B against
`C1B IA1 Bloodhawk tracer and ejection.png`/`…ejection2.png` (casing + persisting aft puff
cluster, wing mounts, one wing at a time). Sustained fire must show ejections at gun rate — the
`BL-137` gate is the regression to prove gone.

**⚠ Traps.** (a) Do not shorten `gunshell`'s `RUN_TIME` to fit the already-live gate — authored
data. (b) `shell1`/`shell2` textures are unmatched to `gunshell` (`BL-141`) — do not repurpose
them without checking `rabbit_blur` isn't a real unrelated effect. (c) The design spec is wrong
twice here (calibre gate, underbelly mount) — rejected against captures; do not re-derive from it.
(d) Adjacent to C24/C25's size/look tuning — coordinate, don't duplicate.

## C23 ☑ `BL-014` Weapon lab fires a group's mounts alternately, like flight

**Goal.** `--viewer`'s weapon lab fires a gun group's muzzles in the same alternating order flight
uses, so the lab is a faithful bench for judging muzzle flash, tracers and ejection (C22/C24/C25)
one mount at a time.

**Evidence (confidence: traced).** `WeaponLab.FireVolley` (`WeaponLab.cs:411-421`) spawns from
*every* mount node at once; flight alternates. Recorded in `backlog.md` as a lab-only fidelity nit
precisely so it isn't re-diagnosed as a flight bug — flight is correct.

**Approach.** Reuse flight's mount-alternation logic (or mirror its cursor pattern) in
`FireVolley` rather than inventing a second scheme. Keep an all-mounts option only if it falls out
for free — the default must match flight.

**Model recommendation.** sonnet, low effort — small, single-file, pattern already exists in
flight code.

**Verify.** Weapon lab volley fires one mount per pull, cycling in flight's order; flight
behaviour byte-unchanged (no `FlightController.cs`/`Projectile.cs` edits expected at all).

**⚠ Traps.** Do not touch the flight firing path — it is the reference, not the patient.

## C24 ☐ `BL-011` Muzzle flash: re-measure, retune size/look

**Goal.** The muzzle flash reads like the original's: one compact forward flash — bright yellow
core, orange flame at the base, elongated forward and slightly outboard — firing from one wing at
a time.

**Evidence (confidence: direction sound; magnitude is a cockpit judgement).** `MuzzleSize` now
reads 0.5 m (`Projectile.cs:53`), set by hand in the weapons lab — the user's verdict at the
controls: still not the original; treat 0.5 as a waypoint, and **re-measure the live build before
re-tuning**. Orientation is already plane-local (`BL-139`, landed). References:
`MuzzleFlash1..3.png` and shot 2 of `OriginalScreenshots/C1B IA1 Bloodhawk tracer and ejection.png`.

**Approach.** **Start by asking the user** (their standing instruction, 2026-07-30): before
touching a constant, get their current read on what specifically is off about the flash — size,
shape, colour, duration — so the tuning has a target beyond the screenshots. Then, after C23:
measure the current look in the lab (scripted `--screenshot` beside the reference), adjust
size/texture/shape toward the refs, and hand back for the cockpit A/B
(`./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo`). If C22's puffer/light land
first, judge the *composite* — the flash alone is no longer the whole picture.

**Model recommendation.** sonnet — iterative look-tuning against reference shots; the judgement
gate is the user's, not the model's.

**Verify.** Screenshot A/B against the two references, then the user's at-the-controls verdict —
the item stays ◐ until that comes back. Also re-confirm A10 muzzle *placement* now the flash is
smaller (`./RunGame.ps1 --plane=player_pfighter --chapter=C1 --infinite-ammo --fire`).

**⚠ Traps.** (a) The in-tree constants are an unfinished experiment, not a calibrated baseline —
measure before moving them. (b) Values are TUNE; record the landed numbers in `backlog.md`'s TUNE
list if the A/B is still owed at commit time. (c) Distinct from C22's puffer/light — that adds
authored elements; this tunes the sprite.

## C25 ☐ `BL-012` Tracers: re-measure, retune toward short yellow dashes

**Goal.** Tracers read as small discrete yellow dashes, matching the reference shots — not long
glowing streaks.

**Evidence (confidence: direction sound; magnitude is a cockpit judgement).** Current in-tree
values are a hand experiment: `TracerLength` 3 m, `TracerWidth` `0.0782f * 2`, texture
`tracer_slug` (`Projectile.cs:47-48,134`) — user verdict: still not the original. The additive
bloom (`additive: true`, `Projectile.cs:134`) is its own open question. References:
`C1B IA1 Bloodhawk tracer and ejection.png`/`…ejection2.png`. The position fix
(grow-from-muzzle, `BL-003`) is landed and separate.

**Approach.** Same loop as C24, including its first step: **ask the user before tuning** (their
standing instruction, 2026-07-30) — what specifically reads wrong about today's tracers, and how
they'd call the bloom question. Then measure the live build in the lab, tune length/width/texture
and decide the additive-vs-mix blend against the refs, and hand back for the cockpit A/B
(`./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo`).

**Model recommendation.** sonnet — same shape as C24; run them together in one session.

**Verify.** Screenshot A/B against the two references, then the user's verdict — ◐ until it
returns. Steady-state distant tracers must still read (don't fix the near look by breaking the
far one).

**⚠ Traps.** (a) Do not re-diagnose the behind-the-plane start — `BL-003` fixed it. (b) The bloom
call is look, not correctness — if MIX-blend is tried near the ground, remember the
`softParticles` depth-fade trap (`BL-060`). (c) TUNE discipline as C24.

# Wave D — destruction bugs

## D31 ☐ `BL-023` `kkgate` door dies visibly and loses its collider

**Goal.** Shooting `kkgate`'s propane tank makes the door deactivate, its pieces fly and fade,
and its collider go — matching the original.

**Evidence (confidence: lead-only).** Re-confirmed 2026-07-25 in flight. The headless harness
reports the death sequence *working* (`swap[healthy 0/1, destroyed 1/1]`, `col[off 4, on 12]`,
seven stage effects), so the defect is in what the player sees, not in the sequence's
bookkeeping — diagnose from the live world, not `--damage-test`.

**Approach.** Live-world diagnosis: `./RunGame.ps1 --plane=player_pfighter --chapter=C2 --fire`,
kill the tank, then inspect the door subtree's actual node state (freecam + node lab, `--log=anim`)
against what the headless report claims. Expect a rendering/visibility or wrong-node mapping, not
a dead sequence.

**Model recommendation.** fable — an open diagnosis where the obvious instrument is already known
to mislead.

**Verify.** The live kill shows door pieces flying + fading and clear passage through the gate.
Cross-check the adjacent `BL-009` (SeaHangar doors — a data gap, not this bug) stays untouched.

**⚠ Traps.** (a) The `det == 0` line in the same run is a separate, disproven-as-related bug
(D32) — do not read one as the other. (b) A quiet `--damage-test` proves nothing here; it already
passes.

## D32 ☐ `BL-007` Diagnose the `det == 0` invert error on multi-death sweeps

**Goal.** The mechanism behind `Condition "det == 0" is true. at: invert (basis.cpp:47)` on
C2/C3 multi-death sweeps is identified and either fixed or written up as a bounded disproof.

**Evidence (confidence: lead-only, heavily fenced).** Reproducer:
`--damage-test --damage-hd=25 --chapter=C2` (4 errors, printed *after* the sweep completes; C1
clean; needs several deaths together). Ruled out: the damage/death code itself, `WorldSounds`,
the damage-stage path, `MotionRuntime.Seek` scale-floor (branch tried, failed), managed
`Basis.Inverse()`. **Leading suspect: `Effects/Puffer.cs`'s per-frame `GlobalPosition` sets**
(462/496/536/565) on emitters whose ancestor chain a death swap left with a zero-scale basis —
the only per-frame native global setter that survives `--mute`.

**Approach.** Instrument the suspect: log/guard the ancestor-chain scale at Puffer's
`GlobalPosition` set sites during the reproducer and correlate with the 4 error prints. If
confirmed, fix at the cause (don't leave dead emitters parented under zero-scale chains, or guard
the set) — not by re-flooring scales in `MotionRuntime`.

**Model recommendation.** fable — a diagnosis with four disproven mechanisms already on the
ground; precision matters more than speed.

**Verify.** Against the reproducer only: `--damage-test --damage-hd=25 --chapter=C2` (expect 0
errors) and `--chapter=C3` (expect 0, was 3). `--run-tests` staying quiet is NOT proof — its
world is torn down before the erroring frame (`docs/verification.md` LOG-9).

**⚠ Traps.** (a) Do not re-apply `fix/motion-scale-zero-guard` — tried, builds clean, error
persists. (b) The error does not abort death sequences — that hypothesis is disproven by log
ordering. (c) The `kkgate` symptom (D31) is a separate bug. (d) If A3 re-parented flight puffers,
re-run this reproducer after — same module, possible interaction.
