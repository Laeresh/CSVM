# Milestone 3 polish, run 3 — make the landed combat feedback actually read

**ACTIVE PLAN** (written 2026-07-31). It sits in `docs/`, which by this repo's convention makes it
a live plan; CLAUDE.md's "Current status" names it. Move it to `docs/plans/` with a `COMPLETE`
banner, and add its row to [`plans.md`](plans.md), when every item lands.

Eleven items, all from the user's 2026-07-31 cockpit pass over the PLAN-m3-polish-2 landings
(PT-05..PT-12, triaged into `backlog.md` `BL-203`–`BL-211` + the extended `BL-016`/`BL-199` the
same day). The unifying finding: **several polish-2 items landed mechanically correct but
perceptually near-nil** — the effect builds, the scripted verification passes, and the player sees
nothing, or the wrong thing. This run's job is to close the gap between "the log says it fired" and
"the player sees it", plus two behaviour bugs (the spiderweb kills the plane; kkgate debris never
fades) and two rocket follow-ups.

Out of scope, deliberately: everything blocked on an owed capture (`playtest.md` §0), the
PT-01..PT-04 judgement calls (they need the user, not code), explosive radius / armour
(`BL-086`/`BL-085`), and any change to damage *numbers*.

## Milestone goal

- Every gun impact is *visible at normal flight speed*: building ricochet, dirt debris, water
  splash — and every water surface in C2 classifies as water (`BL-203`, `BL-204`, `BL-186`).
- A damaged destructible visibly smokes and burns through its stages, in flight and in the damage
  lab (`BL-199`).
- The C3 spiderweb never crashes the plane; kkgate debris fades out and the gate is flyable after
  the blast (`BL-206`, `BL-207`).
- The muzzle flash reads as its authored texture, the eject puffs read as round smoke, and the
  user can tune tracers from config (`BL-208`, `BL-209`, `BL-210`).
- Rocket explosions use their authored per-type rings and the rocket sound gap is at least
  surveyed (`BL-016`, `BL-211`).

**No new combat mechanics, no damage-number changes, no re-derivation from the design spec.** This
run is presentation and collision-participation only; anything that changes what damage *does*
goes back to the backlog.

## ⚠ Read this before implementing anything

Polish-2 claims the cockpit pass killed — do not re-derive them:

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "BL-021 landed: stage smoke/fire renders in flight" (polish-2 A2, verified via a built `Puffer`) | PT-06: *absolutely no difference* between stages, in flight and in the damage lab. A built `Puffer` is not a visible one. |
| 2 | "kkgate pieces fly, **fade** and deactivate" (polish-2 D31's scripted verification) | PT-08: no fadeout at the controls — a piece that vanishes at deactivate passes a frame-sparse capture as "faded" (verification.md SHOT-17). |
| 3 | "the BL-041 quorum vote fixed C2's water classification" | PT-05: turquoise water splashes, blue water doesn't — a texture-name coverage gap the vote can't fix. |
| 4 | "BL-018 landed: dirt impacts show a tumbling-debris burst" | PT-05: dirt still reads as the old flame sprite, just smaller. |
| 5 | "three 120° quads + per-shot roll ≈ the reference's 3-lobed burst" (polish-2 C24) | PT-11: an unreadable red disc — three *centred* additive quads saturate into a blob; the texture must anchor at its left edge. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to a stated mechanism, confirm on-site** | C21 (BL-208), C22 (BL-209), C23 (BL-210), A4 (BL-205) | Confirm the code lead, then implement. |
| **Direction sound, mechanism to find** | A1 (BL-199), A2 (BL-203/186), A3 (BL-204), B11 (BL-206), B12 (BL-207), D31 (BL-016) | The symptom is user-verified; the mechanism needs the diagnosis. Magnitudes are TUNE. |
| **Leads only — starts with a survey/question** | D32 (BL-211) | May end in a question back to the user, not code. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

**⚠ Scripted-launch hazard.** Every ad-hoc Godot run goes through `.\RunProbe.ps1` — a bare launch
scribbles over the calling terminal (verification.md SHELL-10).

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **Acceptance for every look item is pixels, then the user's eye** — a mid-effect screenshot with
  the effect visibly present (or a `--tex-census` count on its texture), never "the def started" or
  "the puffer built". That is the exact instrument failure this plan exists to fix.
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

### Wave A — feedback that landed imperceptible

1. ☑ `BL-199` Damage-stage smoke/fire is actually visible, and sputters (landed 2026-07-31; cockpit re-test = `PT-13`)
2. ☐ `BL-203` Gun-impact visuals read on every surface class (building/dirt/water; folds `BL-186`)
3. ☑ `BL-204` C2 blue-water areas classify as water (landed 2026-07-31; cockpit re-test = `PT-14`)
4. ☑ `BL-205` Collider overlay: colour→surface-class legend (landed 2026-07-31)

### Wave B — destructible behaviour

11. ☑ `BL-206` The C3 spiderweb never damages the plane (landed 2026-07-31 via the gamez `intersect_surface` flag; cockpit re-test = `PT-15`)
12. ☑ `BL-207` kkgate debris fades out; ~~prototype colliders-off-at-fade-start~~ (collider half closed by B11; fade rendering landed 2026-07-31 — cockpit re-test = `PT-16`)

### Wave C — weapon visuals round 3

21. ☑ `BL-208` Muzzle flash: anchor the texture's left edge at the muzzle, full texture readable (landed 2026-07-31)
22. ☑ `BL-209` Eject/muzzle smoke puffs read as round smoke101/102/103 puffs, not stripes (disproven 2026-07-31 — fixed by C21, not the traced billboard lead)
23. ☐ `BL-210` Tracer length/width/brightness as config knobs + distance-visibility floor

### Wave D — rockets

31. ☐ `BL-016` Rocket explosion look: wire the authored per-type rings (`ring_ap/he/sonic`)
32. ☐ `BL-211` Rocket sound: survey authored bindings vs playback, get the user's specifics

## Dependency and parallelism notes

**File contention is the whole story here.** C21, C22, C23 and A2 all touch `Projectile.cs` —
never run two of them in parallel worktrees; suggested order C21 → C22 → C23 → A2 (the three
mechanism fixes first, then A2's per-class look tuning on top). A1, B11 and B12 all sit in the
`AnimRuntime`/world-effects seam — run A1 before B12 (both may end in the effect/opacity runtime)
and keep B11 separate (it is collision-participation, probably `WorldBuilder`/collision-layer
side). A3 is `SceneBuilder`/analysis-side and independent; A4 is `ColliderOverlay`-only and
independent; D31 is `EffectSink`/world-effects (after A1 to avoid the same seam); D32 is a survey
and can run anytime. Parallel-safe starting set: C21, A1, B11, A3, A4.

---

# Wave A — feedback that landed imperceptible

## A1 ☐ `BL-199` Damage-stage smoke/fire is actually visible, and sputters

**Goal.** Guns-only fire holding a destructible in its 60 %/30 % damage stages shows smoke, then
fire smoke, visibly anchored on the object — in real flight and in the damage lab — and the effect
sputters on/off for as long as the stage holds, per the authored `puffit` loop.

**Evidence (confidence: direction sound; the mechanism half is traced).** PT-06 (2026-07-31): the
user saw *no difference whatsoever* between stages, flight and damage lab both — so `BL-021`'s
landed routing produces nothing the eye can find. The traced half is `BL-199`: the `puffit` loop's
50 % `RANDOM_WEIGHT` off/on cycle degenerates because `HandlePufferState`'s `_puffers.ContainsKey`
re-assert guard treats a `SustainEnd`'ed emitter as still running, so the loop's `PUFFER_STATE 1`
never revives it — at most one burst per stage. But the user saw *not even one burst*, so first
establish what actually renders today (a mid-stage screenshot + `--tex-census` on the stage smoke
texture), then fix the revive, then judge size/visibility.

**Approach.** (1) Reproduce in the damage lab (`--damage=` on a destructible or the scripted
damage path) and capture mid-stage — if even the first burst is missing there, the routing claim
itself needs re-verification before touching the sputter. (2) Fix the revive per `BL-199`'s stated
shape: "re-assert revives a stopped emitter" semantics or key removal on `SustainEnd` — do NOT
just delete the guard (the poll idiom re-asserts running emitters every pass; rebuilding each pass
stacks emitters). (3) Judge visibility: if the authored puffs are simply tiny, the size is TUNE —
record it.

**Model recommendation.** fable — the instrument lied once here already; precision over speed.

**Verify.** Mid-stage screenshots with visible smoke pixels at both stages (lab + live flight,
`./RunGame.ps1 --plane=player_pfighter --chapter=C1 --fire`), a sputter visible across a multi-
second capture (several `--shots` frames, not one), then the user's re-test. Census respecting
`BL-061`'s traps: shared `(name, host)` puffer keys, seeded-only `RANDOM_WEIGHT`.

**⚠ Traps.** (a) WORLD-12: a started def that draws nothing measures as success — acceptance is
pixels. (b) The `--run-tests` suite and headless paths prove nothing here. (c) The 32 s
`EffectRuntimeTtl` caps a fixed sustained emitter — check it isn't the second killer once the
revive works.

## A2 ☐ `BL-203` Gun-impact visuals read on every surface class

**Goal.** At normal flight speed, without frame-stepping: a building hit shows a visible
ricochet/spark, a dirt hit a visible tumbling-debris burst, a water hit a visible splash — matching
`Water Splash.png` / `Dirt Splash.png` scale.

**Evidence (confidence: direction sound, user-measured).** PT-05 (2026-07-31): buildings show
*nothing* (no ricochet, no flame); dirt shows the old flame sprite smaller — `BL-018`'s debris
burst does not read; the water ripple is visible only paused + frame-stepped. `BL-186`'s
measurement stands: `splash1.flt` covers 0–12 px at 300 m. The classification + sound path is live
(correctly-tagged water plays the splash sound), so this is the look layer — but check the
building class end-to-end first: "nothing at all" may be a build failure, not a size problem.

**Approach.** Per class: (1) buildings — trace what the `buildings` IMPACT binding actually
instances in a live hit and whether it renders at all; (2) dirt — find why the debris burst reads
as the flame sprite (is the flame drawn *over* the debris? are the debris sprites sub-pixel?);
(3) water — scale the splash toward `Water Splash.png` (small white `Splash0N` burst walking
across the surface). All magnitudes TUNE, recorded on landing. Folds `BL-186` — delete both
entries together.

**Model recommendation.** fable — three sub-diagnoses across the impact path, each with a known
instrument trap.

**Verify.** Close-range scripted impact captures per class (inside RANGE — see traps), each
screenshot showing the effect plainly, A/B'd beside the reference PNGs; then
`./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire --infinite-ammo` for the user. 8-chapter
freecam regression untouched.

**⚠ Traps.** (a) METHOD-18: gun rounds expire silently at RANGE = 1000 m — probe from well inside
the slant range or the instrument measures nothing. (b) `--tex-census` counts are lower bounds;
pair with `--no-fog`. (c) Do not add a sea collider — it exists (`BL-017` disproof). (d) `BL-019`
(HE-rocket building-vs-dirt lookup) stays separate. (e) Contends on `Projectile.cs` with Wave C —
run after C21–C23.

## A3 ☑ `BL-204` C2 blue-water areas classify as water

**Goal.** Every visible water surface in C2 answers gunfire with the splash + sound; the collider
overlay shows it water-classed.

**Evidence (confidence: direction sound, user-observed).** PT-05: turquoise-textured C2 water
splashes and sounds; blue-textured water does nothing. `BL-041`'s quorum vote landed and measured
well install-wide, so the likely gap is name-pattern coverage — the blue water textures never
classify `water` on any polygon, and no vote can rescue a class with zero votes.

**Approach.** Identify the blue areas' texture names (`--tex-census` at a blue-water spot, or the
`analysis/surface-classification/` census), check them against the classifier's name patterns,
extend the patterns, and re-run the analysis census before/after — measured, not eyeballed.

**Model recommendation.** sonnet — a measured pattern-coverage fix with the instrument already
built.

**Verify.** `analysis/surface-classification/` census before/after (the blue textures move to
`water`, nothing else drifts), then `./RunGame.ps1 --plane=player_bhawk --chapter=C2 --fire
--infinite-ammo` over both water looks + the **C** overlay colours agreeing.

**⚠ Traps.** (a) `BL-041`'s standing rule: never widen name patterns without measuring — the
census is the gate. (b) The `soil` field is a dead end. (c) The overlay draws true since `BL-198`
— trust it as the instrument.

## A4 ☑ `BL-205` Collider overlay: colour→surface-class legend

**Goal.** With the **C** overlay up, a small on-screen legend maps wireframe colour → surface
class, so the overlay is readable without memorising the palette.

**Evidence (confidence: traced — it's a feature request).** PT-05, user request. The palette
already exists in `ColliderOverlay`; it just isn't self-describing.

**Approach.** A compact corner label list rendered by `ColliderOverlay` (or its host overlay UI)
from the same colour table the wireframes use — one source of truth, so a palette change can't
desync the legend. Show only in modes where **C** works (flight/freecam/anim-lab).

**Model recommendation.** sonnet, low effort — contained single-module UI.

**Verify.** Toggle **C** in flight and freecam: legend present, colours matching the drawn
wireframes; off when the overlay is off. Goldens untouched (overlay is opt-in).

**⚠ Traps.** In `--viewer`, C is the mesh lab's cull cycler, not the overlay — don't wire the
legend into viewer mode.

# Wave B — destructible behaviour

## B11 ☑ `BL-206` The spiderweb never damages the plane

**Goal.** A full-speed dead-centre run into the C3 tikicave web never crashes the plane: the web
detects the plane (contact or close approach), starts its authored 0.7 s fade, and the plane flies
through unharmed — matching the original, where the web fades *after contact* and no damage ever
occurs.

**Evidence (confidence: direction sound, user A/B'd).** PT-07 (2026-07-31): flying at full speed
into the cave crashes against the web; in the original it never does
(`OriginalScreenshots/Videos/C3 Spiderweb.mp4`). The authored numbers are confirmed — range
2500 m² = 50 m in `spiderweb_gone`, 0.7 s fade — and the `BL-183` range gate is correct. The
defect is collision participation: the web stays crash-solid through approach and mid-fade, and at
cruise speed the plane reaches it before the fade finishes. The user's read: the web's collider
exists to *detect* the plane, not to harm it.

**Approach.** Make the web's collider trigger-only with respect to the plane's crash/graze query
(collision-layer/mask scoping, the same discipline B11-polish-2 described for the sea), while
keeping it as the proximity/contact trigger for the fade. Check the def first for an authored
contact trigger vs pure range — if the data distinguishes them, honour it. Dropping colliders at
fade *start* instead of end is the fallback shape if trigger-only proves wrong for
guns/rockets.

**Model recommendation.** fable — collision-layer scoping interacts with the crash system; a
wrong mask turns web contact into a silent no-op for projectiles too.

**Verify.** Scripted full-speed dead-centre run (`--pos`/`--direction`/`--hold` into the cave)
survives and passes through; the fade still triggers; guns still able to hit whatever the original
allows. Then `./RunGame.ps1 --chapter=C3 --plane=player_bhawk` for the user. 8-chapter freecam
regression for collider-count drift.

**⚠ Traps.** (a) Do not revert the `EXECUTION_BY_RANGE` gate or the fade — only crash
participation is wrong. (b) Do not special-case by name; if this generalises (a class of
trigger-only animated colliders), find the data signal. (c) The mid-fade clip was *predicted* in
PT-07's brief and the original confirms the pass-through — this is the "new backlog entry" that
brief anticipated, not a revert of either landed mechanism.

## B12 ☑ `BL-207` kkgate debris fades out; ~~colliders-off-at-fade-start prototype~~

**Scope cut by B11's landing (2026-07-31):** the pieces' `pt*` nodes are gamez `intersect_surface`
false, so they build no colliders at all now that the flag is honoured — the prototype's A/B would
compare two builds that both no longer collide. Only the fade-rendering half below remains.

**Goal.** After the propane-tank kill, the gate's 12 pieces visibly turn transparent over their
authored ride and the gate is flyable shortly after the blast; the user gets a feel A/B between
colliders dropping at fade end (today) vs fade start.

**Evidence (confidence: direction sound, user-refuted verification).** PT-08 (2026-07-31): break-
away and tumble look good, but the pieces never fade — and D31-polish-2's scripted verification
claimed they did (wrong-claim table #2). The ~3 s ride is authored (`genx12` over the pieces).
The user's lead: the generated world shader may lack a runtime turn-transparent path for these
opaque-pass piece materials — the spiderweb's landed fade may run through a different
material/opacity path. Also check the def's event ordering (are fly/fade sequential or
simultaneous?) before blaming the shader.

**Approach.** (1) Diagnose: instrument one piece's opacity over the ride in a live kill
(`--log=anim` + multi-frame `--shots` capture) — does the fade op fire and the material ignore it,
or never fire? If it's the opaque-pass material, route faded pieces through the same
transparency-capable path the spiderweb fade uses (or flip the material to transparent when a fade
touches it). (2) Prototype: a flag or small change dropping the pieces' colliders at fade start;
hand both builds to the user for the feel A/B before adopting either.

**Model recommendation.** fable — shader/material seam diagnosis with a refuted instrument on the
ground.

**Verify.** A mid-fade frame showing a piece at visibly partial alpha (not just gone-at-the-end —
SHOT-17), a post-fade fly-through with no CRASH, then the user's feel verdict on the collider
prototype. `./RunGame.ps1 --plane=player_pfighter --chapter=C2 --fire`.

**⚠ Traps.** (a) SHOT-17: deactivation masquerades as a fade in frame-sparse captures — sample
mid-fade alpha across several frames. (b) Do not change `genx12`'s authored timings; 3 s is data.
(c) The collider-at-fade-end behaviour is the data's call — the prototype is an A/B candidate,
not a unilateral change; the user decides. (d) A1 may end in the same effect/opacity runtime —
coordinate, don't collide.

# Wave C — weapon visuals round 3

## C21 ☐ `BL-208` Muzzle flash: anchor the texture's left edge at the muzzle

**Goal.** Each of the three flash quads reads as the full authored flash texture, rooted at the
muzzle and extending outward, so the 120° triad and the per-shot roll are visible — no more
saturated red disc.

**Evidence (confidence: traced lead, confirm on-site).** PT-11 (2026-07-31): "way too blurry, the
texture is not even recognisable — just a red semi-transparent circle", rotation invisible. Code:
all three quads are *centred* on `muzzle.Origin` (`Projectile.cs` `Spawn`, the
`muzzleSprites.Add(new Sprite { Pos = muzzle.Origin, … })` triad) — three centred additive quads
overlap into a saturated blob and half of each texture is buried. User direction: the flash
texture is authored with the flash rooted at its **left edge** — the quad must anchor left-edge-at-
muzzle and extend outward along its rolled direction.

**Approach.** Offset each quad's position by half its width along its rolled local +X (or adjust
the quad mesh origin), so UV x=0 sits at the muzzle. Verify one lobe first with an enlarged
single-quad screenshot (texture fully legible), then restore the triad. If the blob persists after
anchoring, check the resolved texture actually is `{ammo}_muzzle1` and not a fallback.

**Model recommendation.** sonnet — a contained quad-anchoring fix with the mechanism in hand.

**Verify.** Weapon-lab `--screenshot` beside `MuzzleFlash1..3.png`: the lobed texture legible, the
triad and rotation visible across consecutive `--shots` frames. Then the user's A/B
(`./RunGame.ps1 --plane=player_bhawk --chapter=C1 --infinite-ammo --fire`). Item stays ◐ until it
returns.

**⚠ Traps.** (a) The per-ammo texture axis and triad scheme are settled (`BL-201`) — don't rehunt
textures or re-litigate the 120° scheme. (b) `MuzzleSize` stays TUNE after the fix. (c) Same
`Projectile.cs` — serial with C22/C23/A2.

## C22 ☐ `BL-209` Eject/muzzle smoke puffs read as round smoke, not stripes

**Goal.** The casing's white puff cluster and the muzzle smoke read as round soft smoke puffs from
every viewing angle — like `smoke101/102/103.png` at varying alpha — never as elongated white
stripes.

**Evidence (confidence: traced lead, confirm on-site).** PT-10 (2026-07-31): casing + cluster
confirmed working, but the puffs are "very elongated rectangles… like two white stripes with
different transparency". Code leads: the smoke pool is one MultiMesh on the single texture
`smoke101` with `billboard: false` (`Projectile.cs` `_Ready`) — a non-billboarded quad seen
edge-on is a stripe — and the pool never cycles the three authored smoke textures or varies alpha
per puff.

**Approach.** Billboard the smoke pool (or orient per-puff toward the camera each frame), cycle
`smoke101/102/103` across puffs (one MultiMesh per texture, the same split the muzzle pool already
uses), and vary per-puff alpha. Check the quad mesh is square for this pool — a shared non-square
mesh would also read stripe-like.

**Model recommendation.** sonnet — contained render-path fix, pattern (per-texture MultiMesh
split) already in the file.

**Verify.** Screenshots of a sustained-fire cluster from three angles (behind, abeam, above):
round puffs in all three. A/B beside `C1B IA1 Bloodhawk tracer and ejection.png`. Then the user's
re-test; `BL-200`'s TUNE values get re-judged only after this lands.

**⚠ Traps.** (a) Fix rendering before re-tuning `BL-200`'s cluster values — the values were judged
through the broken look. (b) The muzzle *flash* pools are deliberately non-billboarded (they roll
in the firing plane) — scope the billboard change to the smoke pool only. (c) Serial with
C21/C23/A2 on `Projectile.cs`.

## C23 ☐ `BL-210` Tracer config knobs + distance-visibility floor

**Goal.** `weapons.tracerLength` / `weapons.tracerWidth` / `weapons.tracerBrightness` exist in
`config.json` (defaults = today's values, byte-identical), so the user tunes the look at the
controls; distant rounds stay visible as flecks farther out, as in the original screenshots.

**Evidence (confidence: traced — it's a requested affordance).** PT-12 (2026-07-31): colour is
right; the user explicitly wants the knobs to retune length/width themselves, and tracers should
read from farther away. Current constants: `TracerLength` 1.0, `TracerWidth` 0.10,
`TracerBrightness` 3.0 (`Projectile.cs:50-56`).

**Approach.** Route the three constants through `Config` with the current values as defaults (the
config/startup-profile pattern in `src/Utils/`). For distance visibility, investigate a minimum
apparent-size floor (clamp the drawn width/length at range so a far round still covers ≥~1 px)
— magnitude TUNE, recorded on landing.

**Model recommendation.** sonnet, low effort — a plumbing item plus one bounded look experiment.

**Verify.** Defaults: goldens hash-identical, `RunTests.ps1` green. Knobs: set a non-default in
`config.json`, screenshot shows the change. Distance floor: two screenshots of the same distant
burst, with/without, the far flecks visibly surviving. Then hand the knobs to the user.

**⚠ Traps.** (a) Defaults must not move a single golden hash. (b) Don't resurrect the tail
artifact — `TextureRepeat` stays off (`BL-202`). (c) Serial with C21/C22/A2 on `Projectile.cs`.

# Wave D — rockets

## D31 ☐ `BL-016` Rocket explosion look: the authored per-type rings

**Goal.** Rocket explosions read like the original's, per type — starting from the authored ring
textures the user identified (`ring_ap.png`, `ring_he.png`, `ring_sonic.png`).

**Evidence (confidence: direction sound; the data survey is the first step).** PT-09
(2026-07-31): trails now pass, explosions are "still a lot different" from the original. The ring
textures exist in the texture archives and correspond per type — find where the explosion effect
defs author them (the explosion goes through the D32 world-effects puffer / `EffectSink`) before
building anything: the rings are data, and the current explosion may simply not build the
ring-bearing part of its def.

**Approach.** (1) Survey each rocket type's explosion effect def for the ring (and anything else
unbuilt — flash, ring, smoke column layering); (2) wire what's authored through the existing
effect path; (3) A/B per type against original captures. Speed of the effect playback is part of
`BL-016`'s original complaint — check the def timings are honoured, don't retune them.

**Model recommendation.** fable — a data decode ending in look-critical rendering, same shape as
polish-2's C21.

**Verify.** Per-type explosion screenshots A/B'd against the original references; the ring visibly
present where authored. `./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets` for the
user. New decodes land with their `docs/formats/` page.

**⚠ Traps.** (a) The rings are *leads to locate in data*, not licence to hand-paint an explosion —
if no def references them, that's a finding to record, and the stand-in stays TUNE. (b) Respect
`BL-061`'s effect-census traps when counting what builds. (c) Explosion *sound* belongs to D32 —
don't fold it in silently.

## D32 ☐ `BL-211` Rocket sound: survey + user specifics

**Goal.** The gap between the authored rocket fire/flyout sound and what we play is named — what
the data binds, what we select, and what the user hears as wrong — ending either in a contained
fix or a precise question/capture request back to the user.

**Evidence (confidence: lead-only).** PT-09 (2026-07-31): "create a backlog item for rocket
sound" — no further detail. Nothing is diagnosed yet.

**Approach.** Survey first: the rocket weapons' FIRE/FLYOUT/IMPACT sound bindings in the extracted
data vs what `Projectile`/the sound path actually plays (selection, looping, attenuation). If a
concrete mismatch falls out (wrong sample, missing flyout loop), fix it; otherwise write up the
survey and ask the user what specifically reads wrong (launch bark, flyout loop, explosion?) or
request a capture. A question back is a valid landing.

**Model recommendation.** sonnet — a bounded survey with an escalation path.

**Verify.** The survey table (binding vs playback per rocket type) recorded; any fix A/B'd by the
user with `./RunGame.ps1 --plane=player_bhawk --chapter=C1` (F fires).

**⚠ Traps.** (a) Don't tune mix levels by model judgement — sound magnitudes are the user's call.
(b) Explosion sound may belong to `BL-016`'s effect def rather than the weapon binding — check
both before declaring a gap.
