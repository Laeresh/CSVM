# Milestone 3 — Polish run 5 (effect reachability, world-render fidelity, the instruments)

**ACTIVE PLAN** (written 2026-08-01). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans.md), when every item lands.

Ten items drawn from `backlog.md` under the **same criteria polish-4 used**: open, M3-scoped, and
judgeable **without the user at the controls or the original game open**. The shape is one
instrument fix that unblocks the rest, two measurements that may close their entries with no code,
four effect/feedback items whose data ships complete, two world-render fidelity fixes with a
measured cause, and one plan-sized mechanic. **Every item was re-verified still-open against both
`docs/HISTORY.md` and the code** — and that pass corrected three backlog claims outright (see the
table below).

**Deliberately excluded: anything blocked on a capture of the original (`CAP-nn`) or a user A/B.**
That rules out the whole TUNE list (`BL-215`/`BL-218`/`BL-223`/`BL-227`/`BL-200`–`BL-202`), the
camera work (`BL-149`/`BL-150`), the flight-model gaps (`BL-092`–`BL-097`, `BL-147`), the armour
layer (`BL-085`/`BL-173`), graze pushback (`BL-172`), the cloud band (`BL-118`), the stall ramp
(`BL-148`), the lens flare (`BL-165`), Doppler (`BL-160`), `BL-142`'s by-eye `IndicatorLowFrac`
retune and `BL-213`'s water-expiry question. Also excluded: future-milestone work (`BL-068` M4 AI,
`BL-067` the configurator, `BL-134` the cutscene player, `BL-181` the menu hub), items blocked on an
open question (`BL-051` on `BL-099`, `BL-050`'s undecided delta semantics, `BL-036` zone selection,
`BL-135`'s same-tick question, `BL-058`), items the user owns by decision (`BL-133`), and the
second-texture-pass feature (`BL-056`) — real and measured, but it would be a **second** plan-sized
item and this run carries one.

## Milestone goal

- The `C` collider overlay survives a destructible dying, so per-surface judgement calls stop being
  blocked on a debug tool that throws.
- Every effect a death or damage sequence calls actually reaches the world — no name that starts,
  logs success, and draws nothing.
- A sea dive plays the water crash the data authors for it, instead of the dirt crash.
- Rounds that pass close enough to be frightening sound like it.
- Authored art the pipeline currently discards — the hand-drawn mip levels, the self-lit model flag —
  is honoured, so distance and night look like the original's rather than like our filter's.
- Two overlapping effect calls each keep their own instance.

**No new TUNE constants beyond the ones the items name, and no magnitude invented where the data is
silent.** Where an item's answer turns out to be "the data does not do this", the disproof is the
deliverable — `B7` in polish-4 is the precedent.

## Decisions (2026-08-01)

| # | Question | Decision |
|---|---|---|
| 1 | Selection criteria for the ten | **Polish-4's, unchanged** — open, M3-scoped, verifiable without the original game. Confirmed by the user before drafting. |
| 2 | How many plan-sized items | **One** (`D10`). `C9` is the second-heaviest and is deliberately the last thing before it. |
| 3 | Is `BL-220`'s missing legend separate work? | **No — same defect.** The throw at `ColliderOverlay.cs:159` preempts `ShowNotice(…, showLegend: true)` at `:168`; the legend itself is present and correct (`LegendClasses`, `:70`, landed as `BL-205`). Fix the lifetime and the legend returns. |
| 4 | Which effects item is the large one | **`BL-225` (per-call instancing)**, not `BL-061` item 2 (the template MESH half). Instancing is upstream of the mesh half — a pooled instance is what makes a per-call mesh possible — so doing the mesh half first would be built on the thing this plan replaces. `BL-061` item 2 stays in `backlog.md`. |
| 5 | `BL-046` scope | **A census first, closure second.** `EffectAnimNames` already binds the damage-stage pair; what has never been enumerated install-wide is the **death** call set. The item may well close as "already closed" — that is a valid outcome. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `BL-220`'s legend is a second, separate regression | The legend is implemented and correct (`ColliderOverlay.cs:70,226,649`); the crash at `:159` simply happens **before** the notice is shown at `:168`. One fix, two symptoms |
| 2 | `BL-046`'s world-effects closure is a "fixed 30-name set" that still excludes the damage-stage sputters | Stale — `EffectAnimNames` is **33 names** and has bound `sputter_black_smoke_obj`/`sputter_fire_smoke_obj` since `BL-021` landed 2026-07-30 (`WorldEffectsFactory.cs:28-49`). The *damage-stage* call set is censused and closed; the **death** call set is not |
| 3 | `BL-052`'s risk is worth 774,152 m² across four C5 pairs | Those four pairs are subface-over-base and the subface fix separates them by 50× the rank step. That figure is now **the area the fix resolved**, not the area at risk — `C8` must re-measure before it may quote any number |
| 4 | `BL-055` is a `TextureArchive.cs:100` change | The box filter is `img.GenerateMipmaps()` at **`TextureArchive.cs:758`**; `:100` is stale. The surrounding comment already records that the alpha/mip chain is treated as load-bearing (`:23`, `:753`) |
| 5 | `BL-059` item 2 needs a new surface classifier | It does not — `ProjectilePool.ClassifySurface(hitBody)` is already called on the collision path by the graze reaction (`FlightController.cs:1633`). `ClassifySurface` at `:928-929` is a two-line hard-coded `Ground` beside it |
| 6 | `gravity.value` is an offset to the arcade `nom_gravity` of 20 | Disproven by the `object-motion-range` census — it is absolute m/s² (a literal −9.8 on 173 events). Relevant to any debris work `B6`/`D10` brushes |
| 7 | Debris magnitude is still a TUNE | No — the speeds are decoded and censused (`analysis/object-motion-range/`). A piece that still looks wrong is momentum or ground-rest, not the launch |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, B4, B5, B6, C8, C9 | Confirm the trace, then implement. |
| **Direction sound, magnitude a judgement call** | B7, D10 | The *what* is settled; the *how much* is TUNE — add it to `backlog.md`'s TUNE list, don't invent it as fact. |
| **Leads only — no mechanism yet; may end in a disproof** | A2, A3 | Budget for investigation. A correct disproof that lands no code is a success. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

The shared evidence the items lean on, measured and re-checked 2026-08-01:

- **`WorldEffectsFactory.cs`** (`CSVM/src/Session/`) is the whole closure surface. `EffectAnimNames`
  (`:28-49`) is **33** bound effect names — rocket/ordnance IMPACT, the 12-entry gun `*_gunhit`
  family, **5** destruction names (`large_30sec_fire`, `great_balls_of_fire`,
  `large_black_smokeball`, `biggun_flying_parts`, `big_splash`), the 2 damage-stage sputters, and the
  3 `touchdown_*` graze reactions. `EffectStageRoots` (`:77-93`) is the anchor-root set the closure
  needs; **a root left out leaves every def anchored on it playing nothing at all** — that is how the
  rocket rings were lost and re-found (D31). `EffectRuntimeTtl` is 32 s (`:54`); `PlayEffectAt` takes
  a per-call `ttl` since C8.
- **`fly_trail1..5` are absent from `EffectStageRoots`** while `carnage_trails` and `carnage_ring`
  are present — the exact gap `BL-061` item 3 names.
- **`FlightController.ClassifySurface`** (`:928-929`) is `=> CrashSurface.Ground;` — a stub with its
  own docstring saying so (`:13-15`, `:547`, `:925-927`). `Crash()` consults it at `:1382`.
- **`ProjectilePool.ClassifySurface`** already answers the same question from a `hitBody` and is
  called on the collision path at `FlightController.cs:1633`.
- **`TextureArchive`** generates its own mips with `img.GenerateMipmaps()` (`:758`), immediately
  after the alpha-softness read that is explicitly documented as needing raw pixels (`:750`).
  Authored `_1`/`_2` levels ship for 52–91 base textures per chapter and are referenced by **no**
  gamez material; measured, they keep 0.35–1.12% of pixels above luminance 128 where the box filter
  keeps 0.00% (`analysis/item9-depth-bias/CBLOCK-LOD.md` §1b).
- **`player.json`'s `warning_shot_*` accumulator** ships complete — `warning_shot_max 2.0`,
  `warning_shot_dissipation 2.0`, `warning_shot_interval 1.0`, `warning_shot_sound
  bullet_warning_sg` (= `snd_bulletpass1-3`, 3D, `RANGE [20,200]`). `SoundDefs.cs:144` names the
  group; nothing calls it.
- **`wait_for_completion`** appears on **56,750** `CallAnimation` events and on no other event kind.
  Distribution: `null` ×53,019, `0` ×3,639, `1` ×32, `2` ×19, `5` ×16, `3` ×9, `4` ×8, `6` ×8 —
  **92 non-null, non-zero events**, small enough to read by hand.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence (traced-to-code / direction-sound-magnitude-
  TUNE / lead-only).
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as each
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

### Wave A — the instruments, and two measurements that may close their own entries

1. ☑ `BL-220` The `C` collider overlay crashes on a freed mesh (and loses its legend to the same throw)
2. ☑ `BL-052` Re-measure zero-separation coplanar pairs now the subface fix has landed
3. ☑ `BL-063` `wait_for_completion` — decode the 92 non-null events, or disprove the index reading

### Wave B — effects and feedback whose data ships complete

4. ☑ `BL-046` Census the install-wide death-effect call set against the world-effects closure
5. ☐ `BL-061` item 3 `biggun_flying_parts` builds no puffer — stage the `fly_trail*` sub-roots
6. ☐ `BL-059` item 2 A sea dive plays the dirt crash — make `ClassifySurface` read the surface tag
7. ☐ `BL-087` Incoming-fire near-miss cue — `bullet_warning_sg` and its shipped accumulator

### Wave C — world-render fidelity with a measured cause

8. ☐ `BL-055` Honour the authored `_1`/`_2` mip levels instead of box-filtering our own
9. ☐ `BL-214` Honour the gamez model `lighting`/`fog` flags world-wide

### Wave D — the plan-sized mechanic

10. ☐ `BL-225` One effect-template instance per call, instead of one shared copy relocated

## Dependency and parallelism notes

`A1` unblocks nothing in code but everything in judgement — the user has stated the `C` overlay is
the better tool than `X` for reading surfaces, and `B6` (water vs dirt), `C9` (night self-lit
geometry) and `D10` all want it working. **Land it first.**

`A2` and `A3` are pure investigations, touch no shared file, and can run alongside anything.

Wave B contention: **`B4`, `B5` and `D10` all edit `WorldEffectsFactory.cs` / the effects
`AnimRuntime` path — never run them in parallel worktrees.** Their order is deliberate: `B4`
enumerates what the closure must contain, `B5` closes one known anchor gap inside it, `D10` then
changes how any of them are instanced. `B6` edits `FlightController` (crash path) and `B7` edits
`Projectile.cs` + the player rigs; those two are contention-free against each other and against B4/B5.

Wave C: `C8` and `C9` are independent in code (`TextureArchive` vs the generated shader/material
path) but **both move goldens**, `C9` heavily (~300 models per chapter, night chapters worst).
Run them sequentially and regenerate the manifest **once**, with the moved shots named (GOLD-1) —
not twice.

`D10` goes last: it is the largest blast radius, it wants `B4`/`B5`'s closure work already in the
tree, and its acceptance test (two rockets, half a second apart, separate targets) is easiest to
read once `A1`'s overlay works.

---

# Wave A — the instruments, and two measurements that may close their own entries

## A1 ☑ `BL-220` The `C` collider overlay crashes on a freed mesh

**Goal.** Pressing `C` toggles the collider wireframes without throwing, in a world where
destructibles have died and swapped their subtrees — and the class legend appears with it.

**Evidence (confidence: traced).** `ObjectDisposedException: 'Godot.MeshInstance3D'` from
`ColliderOverlay.Toggle()`, reached from `_UnhandledKeyInput` at `:134` (user, 2026-08-01, found
while playtesting `PT-24`). The overlay holds `MeshInstance3D` references built once by `Build()`
(`:152-155`) and thereafter only flips visibility: `Toggle` walks `_entries` writing
`e.Draw.Visible = _shown && !e.Shape.Disabled` at **`:159`** with **no validity guard**, while
`SyncEnabled` at `:578` guards the same access with `if (!IsInstanceValid(e.Shape))`. A destructible
dying and swapping its subtree frees the source nodes; the stale reference throws. **The legend is
not a second regression** — `LegendClasses` (`:70`), `BuildLegendText` (`:226`) and the
`RichTextLabel` (`:640-658`) are all present and correct from `BL-205`; the throw at `:159` simply
preempts `ShowNotice(_summary, showLegend: true)` at **`:168`**. `ClassOverlay` avoids the whole
problem by rebuilding on every toggle.

**Approach.** Fix **ownership**, not the write site. Two acceptable shapes: (a) rebuild the wireframe
set on each show, as `ClassOverlay` does — simplest, and the overlay is already a debug-only path
where a walk per keypress is free; or (b) drop entries when their source frees, via a `TreeExiting`
subscription at build time. Prefer (a) unless the rebuild cost measures badly on C5. Restore the
legend by construction — once `Toggle` no longer throws, `:168` runs; do not add legend code.

**Model recommendation.** medium — the diagnosis is done and the fix is a contained ownership
change, but choosing between rebuild and drop-on-free is a real call.

**Verify.** `./RunGame.ps1 --freecam --chapter=C1 --collision=show`: press `C` before any kill
(baseline — must still draw and print the legend), then kill a destructible and press `C` twice
more. **Take the baseline first: an unchanged number is not evidence unless you have seen it able to
fail** — reproduce the throw on the current build before fixing, so you know the repro is real.
`.\RunTests.ps1` green; goldens unchanged (debug-only path).

**⚠ Traps.** (a) **Do not sprinkle `IsInstanceValid` at the write site.** That converts a loud crash
into an overlay silently missing whatever died — which is exactly the thing the user needs the tool
to show. The backlog names this trap explicitly. (b) This is **not** a cosmetic debug nit: the user
reports `C` is the better tool than `ClassOverlay`'s `X` for judging surfaces, so it is on the
critical path for `B6`/`C9`/`D10`. (c) Counting what the overlay tints is not evidence it drew —
check a screenshot or pixel census, not a log line (`docs/verification.md` LOG-2 family).

## A2 ☐ `BL-052` Re-measure zero-separation coplanar pairs

**Goal.** A current, honest number for how much genuine zero-separation coplanar area survives
`SurfaceRankCap` = 5 — and either a re-scoped `BL-052` with real evidence, or its closure.

**Evidence (confidence: lead only — this may end in a disproof).** `BL-052` records that 0.0–4.1% of
built meshes per chapter carry more than 6 (material, priority) groups, so ranks 5+ collapse onto the
same bias; the worst mesh has 32 groups (C4). The structural hole is real and is invisible to any
node-level scheme because both sides share a node. **But the entry's evidence has been overtaken:**
the four C5 pairs it cites (774,152 m² across `g4642`, `g4622`, `g4674`) are all subface-over-base,
and the subface fix now separates them by half a priority level — 50× the rank step, uncollapsible by
the cap. The entry therefore **has no measured example left**, and the standing instruction on it is
"re-measure before acting". Instrument: `analysis/item9-depth-bias/`.

**Approach.** Re-run the depth-bias measurement with subface pairs **excluded**, over all 8 chapters,
and report: how many zero-separation conflicting pairs survive, their total area, and which meshes
they sit in. Write the result into `analysis/item9-depth-bias/` as a dated section. Then take one of
three outcomes: close `BL-052` if nothing meaningful survives; re-scope it to the surviving cases
with their real numbers; or — only if the survivors are large and visible — propose the cap change as
a *separate* future item. **This item does not change `SurfaceRankCap`.**

**Model recommendation.** medium — a re-run of an existing instrument with one filter added; the
judgement is in reading the result honestly, not in building anything.

**Verify.** The measurement is the deliverable. Sanity-check it against the known-good case: the four
formerly-cited C5 pairs must now appear as **separated**, not as survivors — if they still show as
zero-separation, the subface exclusion is wrong and the whole run is untrustworthy. Cross-check one
surviving pair, if any, with a `--freecam` capture at its location before believing it is visible.

**⚠ Traps.** (a) **Do not quote the 774,152 m² figure** — it is now the area the subface fix
resolved, not the area at risk. Three documents repeated the old claim; do not make it four.
(b) Do not fold in `BL-053` (`node_bias` span): the dense conflict-rank scheme that would have fixed
it was **retired by `BL-054`** when the user ruled C1B's z-fighting faithful to the original. (c) Do
not raise `NodeOrderBias` — `BL-054` closed that with a stronger reason than tuning: the measured
"improvement" was 1.4 points from erasing a faithful artifact.

## A3 ☑ `BL-063` `wait_for_completion` — decode or disprove

**Goal.** Either a decoded meaning for `wait_for_completion` with a `docs/formats/` entry, or a
recorded disproof of the index hypothesis — so a field that the extraction decodes faithfully and the
runtime ignores stops being an unknown.

**Evidence (confidence: lead only — this may end in a disproof).** Found 2026-07-22 while fixing the
sequence scheduler. It appears on **56,750 `CallAnimation` events** and on no other event kind;
`null` ×53,019, `0` ×3,639, then `1` ×32, `2` ×19, `5` ×16, `3` ×9, `4` ×8, `6` ×8. **That
distribution reads as an index, not a boolean** — the same family as the `wait_for_raw` connector
slots on `player_plane_destruct`'s per-piece `CallAnimation large_firetrail WithNode pieceN`, whose
0/1/2 are the three `local_lft` `CallObjectConnector` refs. **Nothing in `CSVM/src` references the
field.** Working hypothesis: it selects which of the def's `anim_refs` connectors the call blocks on.

**Approach.** Dump the **92** non-null, non-zero events (small enough to read by hand) with their
owning def and that def's `anim_refs` array. Test the hypothesis the cheap way first: **is each
def's `anim_refs` array long enough to index at the observed value?** A single counter-example
(value 6 on a def with 3 refs) kills the reading outright. If it survives, check whether the
referenced connector is one the scheduler could plausibly block on. Land the answer in
`docs/formats/anim-definitions.md` either way; write code only if the decode is unambiguous **and**
produces an observable difference.

**Model recommendation.** high — it is a decode with a real chance of ending in a disproof, and the
wrong move (wiring a guessed semantic into the scheduler) is exactly this project's most-repeated
trap.

**Verify.** The dump and the analysis are the deliverable; stage the instrument in
`analysis/wait-for-completion/`. If code lands, the sequence scheduler now honours event offsets
correctly (fixed 2026-07-22), so any *timing* change this field causes should be cleaner to see than
it was before — take a `--debug-anim` baseline on a def that carries a non-null value first.
`.\RunTests.ps1` green.

**⚠ Traps.** (a) **This is the `spline_interp` shape** — a faithfully-decoded field the runtime
silently ignores, and that one looked inert right up until it was traced to a 1e29 transform blowup.
Inertness is not proven by absence of a symptom. (b) `CALL_ANIMATION` references animations by **name
string only** — no index form exists anywhere in this data — so do not read the value as an animation
index. (c) 3,639 explicit `0`s alongside 53,019 `null`s means zero and absent are **different
authored states**; a decode that treats them as the same has not explained the data.

---

# Wave B — effects and feedback whose data ships complete

## B4 ☑ `BL-046` Census the death-effect call set against the closure

**Goal.** Every effect name a death sequence calls either renders in the world or is documented as
deliberately excluded with the reason — no name that starts, logs success and draws nothing.

**Evidence (confidence: traced).** The world-effects runtime binds a fixed name set and **an effect
name outside it starts, logs, and draws nothing** (`docs/verification.md` WORLD-12). ⚠ **The backlog
row is stale on the specifics:** `EffectAnimNames` is **33** names, not 30, and the progressive
damage-stage pair (`sputter_black_smoke_obj`/`sputter_fire_smoke_obj`) has been bound since `BL-021`
landed 2026-07-30 (`WorldEffectsFactory.cs:41-45`). The code comment there records that the
install-wide **`DAMAGE_SEQUENCE`** call set is censused and closed — exactly those two plus C4
`train01`'s `b_steamtrail`, excluded because its anim root is the live train subtree rather than a
relocatable template. **What has never been enumerated is the `death` call set**: `:38-40` binds five
destruction names with no census cited beside them. Known still-outside: `b_steamtrail`, and the
template MESH halves (`BL-061` item 2).

**Approach.** Census first, close second. Enumerate every `CALL_ANIMATION` target reachable from a
death sequence across all 8 chapters (`extracted/*/cam_anim/`, `*/mis_anim/`), diff against
`EffectAnimNames`, and stage the instrument in `analysis/death-effect-closure/`. For each name in the
gap, decide one of three: bind it (and add its anchor roots to `EffectStageRoots` — see `B5`, which is
one instance of this); exclude it with a reason written into the `EffectAnimNames` comment block the
way `b_steamtrail` and `random_gun_impact` already are; or record it as needing a different mechanism.
**If the census comes back empty, this item closes as "already closed" and that is the deliverable.**

**Model recommendation.** high — the census design is the item, and a census that quietly misses a
call path manufactures a clean answer.

**Verify.** For every newly-bound name, confirm a **`Puffer` was actually built** — `--effects-test`,
**seeded**, since several variants gate their puffer behind `RANDOM_WEIGHT` and an unseeded run
reports a different set each time. Take the census count as a baseline before and after. 8-chapter
`--freecam` regression: zero errors, unchanged mesh/node counts. `.\RunTests.ps1` green.

**⚠ Traps.** (a) **Confirming a def *started* is not confirming it rendered** — check that a `Puffer`
was built, or the measurement is of a no-op (WORLD-12, and `BL-046`'s own note). (b) A name bound
without its **anchor roots** in `EffectStageRoots` plays nothing at all — that is how the rocket
explosion lost its per-type rings (the comment at `WorldEffectsFactory.cs:70-76` records it). Bind
name and root together. (c) `WORLD-24`: an effect's own `PLAYER_RANGE` gate (500 m on the gun-impact
family) makes a probe flown at normal standoff show nothing — fly close or the census lies.
(d) Do not extend the closure to the template MESH halves here; that is `BL-061` item 2 and stays in
`backlog.md`.

## B5 ☐ `BL-061` item 3 — stage the `fly_trail*` sub-roots

**Goal.** `biggun_flying_parts` — the zeppelin-destruction container — builds its puffers, so a
zeppelin kill smokes instead of firing a bound-but-anchorless effect.

**Evidence (confidence: traced).** `biggun_flying_parts` **is** bound (`WorldEffectsFactory.cs:39`)
and still builds no puffer in `--effects-test`. The reason is the anchor gap: its `spurtpuffer1..5`
ride `fly_trail1..5` sub-trail roots, which are gamez nodes but are **not in `EffectStageRoots`**
(`:77-93` — `carnage_trails` and `carnage_ring` are there; `fly_trail*` are not). This is exactly the
failure mode the block's own comment documents: *"a root left out leaves every def anchored on it
unanchored, so it plays nothing at all"*.

**Approach.** Add the `fly_trail*` / `*_trails` sub-templates to `EffectStageRoots`, confirming first
— per the comment's own standard — that each exists as a single parentless root in **all 8**
chapters. Then re-run the seeded `--effects-test` census and check whether `biggun_flying_parts`
builds its five puffers. If it still does not, the next candidate is the `OPERAND_NODE` call path
rather than a bare point; investigate that before adding anything else to the root list.

**Model recommendation.** medium — a contained addition to a documented list, with a census that
tells you immediately whether it worked.

**Verify.** `--effects-test`, **seeded**, before and after: the census count must rise and
`biggun_flying_parts` must appear among the puffer-building defs. Then a live kill — the zeppelin is
reachable in the chapters `backlog.md` names. 8-chapter `--freecam` regression unchanged. Goldens:
expect **no** movement (these roots are staged, not ambiently rendered) — if one moves, explain it
before regenerating.

**⚠ Traps.** (a) **The census is only reproducible seeded** — several gun `*_gunhit` variants gate
their puffer behind `RANDOM_WEIGHT`, so an unseeded run reports a different set each time and
manufactures its own answer. (b) These effects **share puffer names** (`trailpuffer2` across
`small_fireball`/`great_balls_of_fire`/`large_black_smokeball`). The effects runtime keys emitters
per-def (`DefScopedPufferKeys`, A1 2026-07-31) but the **WORLD** runtime deliberately keeps the
collapsed `(name, host)` key — def-scoping it stacked C5's six `m_crane_go` spark defs on one node and
**moved the c5 golden**. Do not "unify" the two schemes. (c) Adding a root that is *not* parentless in
some chapter changes what `WorldBuilder` skips — check all 8, not just C1.

## B6 ☐ `BL-059` item 2 — the water crash variant

**Goal.** Diving into the sea plays `player_crash_water` — `plane_big_splash` + `large_steam_spray`,
the `destroy_crash` sequence, `snd_exp_water_a` — instead of the dirt crash.

**Evidence (confidence: traced).** `FlightController.ClassifySurface` is
`private static CrashSurface ClassifySurface(string hitName) => CrashSurface.Ground;`
(`:928-929`) — every crash in the game classifies Ground, and the docstring at `:925-927` says so
outright ("Water needs a sea-surface signal the collision system does not yet expose"). **That
docstring is now out of date:** the sea *is* collidable and `water`-tagged, measured 2026-07-30 (a
C1B sea dive logs `CRASH into g28178/col` and plays the dirt crash), and the signal is already on the
`hitBody` the sweep reports — `ProjectilePool.ClassifySurface(hitBody)` is called on the collision
path 250 lines below, at `:1633`, by the graze reaction. `Crash()` consumes the verdict at `:1382-1385`.
Data: `extracted/C1/cam_anim/player-player_crash_water.json`; full decode in `docs/HISTORY.md`
(2026-07-23).

**Approach.** Change `ClassifySurface` to take the struck **body** rather than a name and delegate to
`ProjectilePool.ClassifySurface` — reuse, do not write a third classifier — mapping `water` →
`CrashSurface.Water` and everything else to `Ground`. Wire the water arm through the existing
per-player crash runtime the dirt crash already uses (`BuildFlightCrashRuntime`), and route the sound
to `snd_exp_water_a` where the ground arm plays `snd_exp_ground_a` at `:1384-1385`. Update the three
stale docstrings (`:13-15`, `:547`, `:925-927`) in the same change. **`CrashSurface.Air` stays
unreachable** — it needs a mid-air destruct trigger and is not this item.

**Model recommendation.** high — it touches the crash path, and the classification seam it picks is
the one the whole `BL-059` family inherits.

**Verify.** `./RunGame.ps1 --plane=player_bhawk --chapter=C1B` and dive into the sea: splash and steam
spray, water explosion sound, no dirt burst. Scripted: `--det --crash` at a scripted over-water pose
for the golden surface, plus the same over land as the able-to-fail control — **if the land crash
does not still read as dirt, the classifier is inverted and the water result proves nothing.**
`.\RunTests.ps1` green; 8-chapter `--freecam` regression unchanged.

**⚠ Traps.** (a) **Confirming the def started is not confirming it rendered** — `player_crash_water`
names `plane_big_splash` and `large_steam_spray`; check both resolve inside the world-effects closure
(`B4`'s subject) and that a `Puffer` was built, or this lands as a silent no-op. (b) Do **not** fold
in `BL-059` item 1 (`bounce_sequence` ground-rest) or the air variant — different mechanisms, and the
ground-rest half needs a physics ray. (c) The crash runtime is built with
`SoundHandledElsewhere = true` (the `BL-005` fix); route the water sound the same way the ground boom
is routed, or the "no audio session" warning comes back. (d) Do not re-open the debris magnitude while
in here — it is decoded and censused, not a TUNE. (e) **A3 makes this the first clean
`WAIT_FOR_COMPLETION` timing case:** `player_crash_water`'s flagged `plane_big_splash` call means the
original waits for that callee to finish before starting `large_steam_spray`; CSVM currently starts
the successor immediately. Read
[`anim-definitions.md`](formats/anim-definitions.md#wait_for_completion-blocks-on-the-named-callee),
capture the transition, and do not call the variant faithful merely because both effects appeared.

## B7 ☐ `BL-087` Incoming-fire near-miss cue

**Goal.** A round passing close to a player's aircraft plays `bullet_warning_sg`, rate-limited by the
shipped accumulator — so in 2–4-player splitscreen you can hear how close that was.

**Evidence (confidence: direction sound, magnitude a judgement call).** The whole cue set ships and
nothing calls it: `bullet_warning_sg` (= `snd_bulletpass1-3`, 3D, `RANGE [20,200]`) is bound in
`player.json` as `warning_shot_sound`, with `warning_shot_max 2.0`, `warning_shot_dissipation 2.0`,
`warning_shot_interval 1.0` beside it. `SoundDefs.cs:144` names the group in a comment; there is no
caller in `CSVM/src`. **Only the near-miss half is buildable** — `bullet_hit_sg` needs aircraft
bodies (a plane exists in physics only as a `CastMotion` query shape, `PlaneCollider`, so a
projectile raycast can never strike one) and `window_hit_sg` needs a cockpit view. The near-miss test
does **not** need any of that: it is segment-to-point distance from the round's step against each
`PlayerRig`.

**Approach.** In `ProjectilePool.SimStep`, after the existing ray step, measure each round's swept
segment against every *other* player's rig origin and fire the cue below a threshold. Drive the rate
from the shipped accumulator rather than inventing one: intensity accrues per near miss, dissipates
at `warning_shot_dissipation`, saturates at `warning_shot_max`, and re-triggers no faster than
`warning_shot_interval`. **The pass distance itself is not in the data** — record it as a TUNE in
`backlog.md` with the reading you adopted, and state in the code comment that `RANGE [20,200]` is the
3D falloff range, *not* the trigger radius.

**Model recommendation.** high — the accumulator's units are unlabelled and the trigger distance is
invented; getting the split between "what the data says" and "what I chose" right is the whole item.

**Verify.** `./RunGame.ps1 --players=2 --plane=player_pfighter,player_bhawk --chapter=C1
--infinite-ammo` — fly past a shooting partner and hear the pass; then confirm it does **not** fire on
your own rounds (self-exclusion is the obvious bug). Take an able-to-fail baseline: with the threshold
set to 0, nothing sounds; at an absurd threshold, every round sounds. `.\RunTests.ps1` green; goldens
unchanged (audio-only, and splitscreen is not a golden surface).

**⚠ Traps.** (a) **Do not exclude your own rounds by weapon owner alone** if a player can fly through
their own line of fire — exclude by shooter identity, and say which you did. (b) These are **not**
"referenced by no world data" orphans: `bullet_hit_sg`/`window_hit_sg` sit on 80 shipped defs; only
`snd_warningshot1-3` are true orphans. Do not conflate the four groups. (c) **Do not reach for the
hit cue** by firing `bullet_hit_sg` off our own collision path — that conflates "I was shot" with "I
scraped a wall" and would make both wrong (the same trap `BL-222` records). (d) Single-player hears
none of this until M4 AI shoots back; that is correct, not a failed implementation.

---

# Wave C — world-render fidelity with a measured cause

## C8 ☐ `BL-055` Honour the authored `_1`/`_2` mip levels

**Goal.** Distant city blocks keep the street lights the artist drew into the half- and
quarter-resolution levels, instead of averaging them into the dark with our own box filter.

**Evidence (confidence: traced).** 52–91 base textures per chapter ship hand-authored `_1`/`_2`
levels (`cblock1`, `cblock1_1`, `cblock1_2`), referenced by **no** gamez material, and we generate
our own mips with `img.GenerateMipmaps()` — ⚠ at **`TextureArchive.cs:758`**, not the `:100` the
backlog row cites. Measured: the authored levels keep **0.35–1.12%** of pixels above luminance 128
where a box filter keeps **0.00%** — the artist preserved the street lights at distance and our
filter averages them away. That is precisely the user's observation of the original going "dark with
some points → brighter illuminated street" with distance (2026-07-22). Instrument and numbers:
`analysis/item9-depth-bias/CBLOCK-LOD.md` §1b.

**Approach.** In `TextureArchive`, when loading a base texture, look for its `_1`/`_2` siblings in the
same archive and install them as mip levels 1 and 2, generating only the remaining chain below.
Match by name convention, and **log the count adopted per chapter** so the coverage is visible. Where
no authored level exists, behaviour is unchanged.

**Model recommendation.** high — it changes the texture pipeline for every chapter, and the
alpha/mip ordering around the insertion point is already documented as load-bearing.

**Verify.** Take the luminance measurement as the baseline **first**: re-run
`analysis/item9-depth-bias/CBLOCK-LOD.md`'s §1b instrument on our loaded mip chain before the change
(expect ~0.00% above 128) and after (expect it to approach the authored 0.35–1.12%). A number that
"looks right" without a failing baseline proves nothing. Then a `--det --screenshot` of a C5 city
block at distance, before/after. 8-chapter `--freecam` regression: zero errors, unchanged mesh/node
counts. **Goldens will move** — name the moved shots and explain them; do not regenerate silently.

**⚠ Traps.** (a) **`LastAlphaIsSoft` is read from raw pixels *before* mipmaps** (`:750`, with the
comment saying so) and the surrounding docs record that the alpha channel and mip chain are treated
as the original's, driving the blend/scissor choice and the cutout silhouette (`:23`, `:753`).
Inserting levels must not run before that read or the cutout classification changes underneath you.
(b) The authored levels are referenced by **no material** — do not "fix" that by registering them as
textures in their own right; they are mip levels, and registering them would make them selectable
surfaces. (c) Verify the sibling actually is half/quarter the base's dimensions before installing it —
a name-convention match is a lead, not a guarantee.

## C9 ☐ `BL-214` Honour the gamez model `lighting`/`fog` flags world-wide

**Goal.** Authored self-lit geometry — explosion rings, splash models, effect meshes, lit signage —
reads bright on night maps instead of being dimmed to invisibility by the mission sunlight.

**Evidence (confidence: traced).** Found while landing `BL-203`. Every chapter carries a small
authored set of self-lit models (`lighting: false` — **25–31 per chapter unfogged**, plus **~270–390
more fogged-but-unlit**), but the generated world materials multiply the mission SUNLIGHT
(`csky_world_light`) into everything, so authored self-lit effect geometry dims to invisibility at
night. `BL-203` landed a **narrow** exemption for the projectile-instanced splash models only —
`ProjectilePool.OverrideUnlit`, `Projectile.cs:1124`, called at `:1097`/`:1099`/`:1153`. The general
fix is a per-model term in the shared shader path.

**Approach.** Carry the model's `lighting` (and `fog`) flag through to the generated material as a
per-model uniform, and gate the `csky_world_light` multiply and the fog term on it in the shared
`.gdshaderinc` blocks — the instance-uniform ordering there is already a documented gotcha, so read
`CSVM/shaders/` and `docs/formats/gotchas.md` before adding a uniform. Once the general path exists,
**fold `ProjectilePool.OverrideUnlit` into it** rather than leaving two mechanisms — or state
explicitly why the projectile path must stay special-cased.

**Model recommendation.** high — shared shader path, ~300 models per chapter, and the largest golden
churn in this plan.

**Verify.** Before/after `--det --screenshot` on a **night** chapter with `--effects-test`, per the
backlog's own instruction: the world-effects stage templates (rings, fireballs) render through the
same dimmed path, so check them at night before assuming the flag fixes them. A pixel census
(`--tex-census`, paired with `--no-fog`) is the quantitative form. 8-chapter `--freecam` regression:
zero errors, unchanged mesh/node counts. **Expect golden churn on night chapters** — regenerate with
the moved shots named (GOLD-1), once, together with `C8`.

**⚠ Traps.** (a) The flag moves ~300 models per chapter — a golden that does *not* move on a night
chapter is a reason to check the flag is being read at all, not a relief. (b) The world-effects stage
templates render through the same dimmed path; confirm them under `--effects-test` at night rather
than assuming. (c) `lighting` and `fog` are **two** flags — do not wire one and claim both; the
fogged-but-unlit population (~270–390) is an order of magnitude larger than the unfogged one, so
conflating them changes far more than intended. (d) Do not double-apply with `OverrideUnlit` — decide
which mechanism owns the splash models and say so.

---

# Wave D — the plan-sized mechanic

## D10 ☐ `BL-225` One effect-template instance per call

**Goal.** Two overlapping effect calls each keep their own instance: two rockets landing within one
explosion's run keep their own trails, at their own sites, instead of the first blast's trails jumping
to the second's location.

**Evidence (confidence: direction sound, magnitude a judgement call).** The original instances a
fresh copy of an effect template (`he_trails`, `flame_ball_01`, …) per `CALL_ANIMATION`; we relocate a
**single shared copy**, so overlapping calls collapse onto the last site. Since 2026-08-01 a second
call relocates **and restarts** the template — before that it was skipped by the live-instance guard
and drew nothing — so the second blast is served, but the first blast's in-flight trails move with it.
The same limitation has a second face in `BL-061`: **one live instance per effect def**, so a second
damaged object's sputter restarts the shared def and simultaneous damage-stage smoke collapses onto
the latest object. The gun-impact smoke (C8, 2026-08-01) is this problem at a much higher event rate
and **deliberately does not solve it** — it throttles to one play per 0.1 s per effect name and bounds
each instance to 0.3 s, so consecutive hits reuse the one relocated template on purpose.

**Approach.** A small **pool of template copies per effect root**, cycled per call, with motions and
puffers keyed by instance rather than by `(name, host)`. Size the pool per root from the observed
concurrency, not uniformly, and log the count so exhaustion is visible. **A pool would let C8's
throttle go** — but removing it is a second change: land the pool first, prove it, then remove the
throttle as its own step with its own emitter-count check. Pool size is an invented number: record it
in `backlog.md`'s TUNE list.

**Model recommendation.** high — the largest blast radius in this plan; it changes instancing and
emitter keying at the game's highest event rate, where the failure mode is a leak.

**Verify.** The acceptance test is the backlog's: fire two rockets a half-second apart at separate
targets — both explosions keep their own trails.
`./RunGame.ps1 --plane=player_bhawk --chapter=C1 --fire-rockets`. **Take a baseline that can fail:**
log the live emitter count during and one second after a burst; a leak is the failure mode, and C8's
own verification used exactly this control (TTL 60 s → 2 emitters still growing at t=3 s). Seeded
`--effects-test` census before and after — the count must not fall. 8-chapter `--freecam` regression.
`.\RunTests.ps1` green; **expect and explain any golden movement.**

**⚠ Traps.** (a) **Do not "fix" it by dropping the restart** — that regresses to the second explosion
showing nothing, which is what the pre-2026-08-01 behaviour was. (b) The restart is gated on the call
site having **MOVED**; a poll-idiom call (`If … CallAnimation; Endif; Loop{-1}`) must keep hitting the
live guard, or its callee restarts every frame and never progresses. Whatever replaces the guard must
preserve that. (c) The **WORLD** runtime deliberately keeps the collapsed `(name, host)` key —
def-scoping it stacked C5's six `m_crane_go` spark defs on one node and moved the c5 golden. A pool
must not become a third keying scheme layered on top; decide which runtime it applies to and say so.
(d) Do not extend this into the template MESH half (`BL-061` item 2) — that is downstream of this work
and stays in `backlog.md`. (e) An unbounded pool is a leak with extra steps: every instance needs the
same `ttl` discipline `PlayEffectAt` already applies.
