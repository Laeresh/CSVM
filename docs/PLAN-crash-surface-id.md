# Crash and touchdown selection — the original's surface-id table

**ACTIVE PLAN** (written 2026-08-11). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans.md`](plans/plans.md), when every item lands.

The recreation picks a crash choreography by classifying the struck collider's **texture name** into
`water` / `buildings` / `default` and mapping that onto one of two defs. The original does something
structurally different: it indexes a **vector of `"player_crash_" + <surface name>`** with the struck
material's **surface type id** — a signed dword the extractor already gives us as `soil` — and falls
back to slot 0 whenever that lookup fails. This plan replaces our mechanism with that one, for both
the crash family and the `touchdown_*` graze family, which the original drives from the same table.

**Out of scope: the weapons IMPACT classification.** `Projectile.ClassifySurface`'s
`water`/`buildings`/`default` read is a *different name space* — the per-chapter `zrdr.zbd` IMPACT
tables are keyed by name (`default`, `fault`, `water`, `quicksand`, `player`, `buildings`), and
`buildings` is not a surface-registry name at all. That classifier stays exactly as it is. This plan
touches what picks a **crash/touchdown def**, and nothing else. Also out of scope: `BL-060`'s
"improve on the original" bespoke breakup — this is a faithfulness plan.

## Milestone goal

- A struck collider carries the original's **numeric surface id**, not just our texture-derived class.
- Crash def selection is the original's cascade: `vector[id]`, falling back to `vector[0]` on a null
  material, a negative id, an out-of-range id, or a slot naming a def that does not exist.
- `touchdown_*` graze selection runs off the same table, as it does in the original.
- The mid-air destruct is modelled as the original models it — a canned destruct that plays **no**
  `player_crash_*` def, handing off to the impact def on ground contact.
- `player_crash_default` is understood and implemented as the **fallback arm** — and is what an
  ordinary terrain or building crash plays, as in the original.
- `player_crash_dirt` plays only where a material is `dirt`(13)-tagged, as in the original.
- The dead `CrashSurface.Air` enum value is gone.

**This plan does not route geometry to a def because our current build does.** The original plays
`_dirt` on 3–9 materials per chapter and `_default` on essentially everything else; we have that
backwards. Correcting it is the point, and if the result reads worse at the controls, that is a
finding to bring to the user and a candidate for `BL-060` — not a licence to keep the unfaithful
mapping unmarked.

## Decisions (2026-08-11)

| # | Question | Decision |
|---|---|---|
| 1 | Is `player_crash_default` the air/no-impact variant? | **No — it is the fallback arm.** Decoded: `FUN_0048b920` `0x0048bac5`–`0x0048bb00`. The `BL-059` reading is disproven. |
| 2 | Is the material `soil` field a MechWarrior-3 leftover? | **No — it is the engine's surface type id.** Proven 1:1 over 8 chapters / 4,715 materials by `analysis/surface-classification/soil_id_probe.py`. |
| 3 | Do we keep the texture-name classifier? | **Yes, for weapons IMPACT only.** Two independent name spaces; conflating them is what this plan exists to undo. |
| 4 | What if faithful selection makes the sea stop splashing? | **Measure first (A1), then decide with the user.** Do not silently keep the unfaithful path, and do not silently ship a regression. |
| 5 | Is `dirt` a real registry name? | **Yes — id 13**, from the soils list in `ZBD/zrdr.zbd` `0xe631c`. But it is the *exception*, not the default: see Decision 6. |
| 6 | Which def does ordinary terrain get? | **`player_crash_default`.** Id 0 is ~98 % of materials and `fire`/`airstrip`/`buildings`/`dzone` have no def, so all fall back to slot 0. Our build plays `_dirt` there. This is the plan's biggest behavioural change and A1 gates it. |

## ⚠ Read this before implementing anything

| # | The wrong claim | How it died |
|---|---|---|
| 1 | `player_crash_default` is the no-impact/air destruct, needing a mid-air trigger | `FUN_0048b920`'s cascade: it is `vector[0]`, reached by a null material, `id < 0`, an out-of-range id, or an empty slot. The mid-air destruct is `FUN_004b82d0` and plays no `player_crash_*` def at all. |
| 2 | The crash def is chosen by a three-way branch on what was struck | It is an array index with scale 4 (`MOV EDI,[ECX+EDX*4]`, `0x0048bada`) into a vector built by string concatenation in `FUN_00476250`. |
| 3 | The material `soil` enum is a MechWarrior-3 leftover with no meaning here | It is the dword at material offset `0x20`, bulk-read from the `.gamez` and never fixed up (`FUN_00559e20`), and the engine writes registry indices into it directly (`FUN_004e5590` writes `3` = `quicksand`). |
| 4 | `dirt` is a built-in surface | `dirt` is not a compiled-in registry name, and appears nowhere in the shipped data as a standalone token — only inside anim names. |

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A2, A3, B11, B12 | Confirm the trace, then implement. |
| **Traced, but its consequence for our build is unmeasured** | A1, C21 | The mechanism is settled; whether faithful == playable is not. Measure before writing code. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

Measured by `analysis/surface-classification/soil_id_probe.py` over all eight chapters (4,715
materials). Label → surface type id, identical in every chapter, no label ever taking two ids:

| label | `Default` | `Water` | `Fire` | `Grass` | `Mech` | `Silt` | `NoSlip` |
|---|---|---|---|---|---|---|---|
| id | 0 | 1 | 5 | 8 | 11 | 12 | 13 |

The full registry, with the level-supplied half recovered from `ZBD/zrdr.zbd` `0xe631c` by
`soils_list.py` (ids ≥ 6 are appended by `FUN_0055b7b0`, sole caller the `LoadSoils` script command
at `0x005ba4ae`):

| id | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| name | `default` | `water` | `seafloor` | `quicksand` | `lava` | `fire` | `player` | `enemy` | `airstrip` | `opensesame` | `death` | `buildings` | `dzone` | `dirt` |
| a material uses it | ✓ | ✓ | | | | ✓ | | | ✓ | | | ✓ | ✓ | ✓ |
| a `player_crash_*` def exists | ✓ | ✓ | | | | | | | | | | | | ✓ |

The eight level names fill exactly slots 6–13 and 13 is the highest id any shipped material carries
— list and measurement close on each other with nothing left over. Cross-checks: `dzone` at 12 is
carried by exactly one material per chapter (a danger-zone marker); `buildings` at 11 is also a
weapon `IMPACT` key; `player`/`enemy` at 6/7 appear on no world material.

**The headline consequence.** Reading the last two rows together: ids `fire`(5), `airstrip`(8),
`buildings`(11) and `dzone`(12) have **no def of their own**, so they fall back to slot 0 — and so
does id 0, which is ~98 % of every chapter's materials. **In the original, the ordinary ground crash
is `player_crash_default`.** `player_crash_dirt` plays only on the 3–9 explicitly `dirt`-tagged
materials per chapter. **Our build has this backwards**, playing `_dirt` as the default ground crash
for all terrain and buildings alike.

Per-chapter counts of non-`Default` materials are tiny — 1–3 `water`, 2 `fire`, 3–9 `dirt`, exactly
1 `dzone`, against 270–685 materials per chapter. **That smallness is the whole risk in this plan**
(A1).

The install ships exactly three defs per family: `player_crash_default`/`_dirt`/`_water` and
`touchdown_default`/`_dirt`/`_water`. Full write-up and every address:
[`analysis/surface-classification/FINDINGS.md`](../analysis/surface-classification/FINDINGS.md),
2026-08-11 section.

## Ground rules

- **Original-game data drives everything.** Read the reader/compiled JSON before writing a handler;
  never guess a value. Inventing content is the trap this project falls into most often.
- **Evidence is a lead to verify, not a finding to implement.** Confirm every claim against the
  data/code before building on it; **a correct disproof that lands no code is a success here**, not a
  failure. Mark each item's Evidence with its confidence.
- **`PROJECT_CONTEXT.md` + `docs/architecture.md` / `docs/formats/` are updated in the same turn** as
  each landed item; a landed item gets its record in the landing commit's message (`docs/HISTORY.md`
  is frozen — never append) and is **deleted** from `backlog.md` (not marked FIXED there).
- **Read `docs/verification.md` before measuring anything** — the instruments here mislead; cite the
  rule that bites per item.
- **Verify against a full 8-chapter `--freecam --chapter=<X>` regression** (zero errors, same
  mesh/node counts unless the change is meant to add coverage) plus a targeted capture.
- **Read the module's entry in `docs/architecture.md` before modifying it.** Dead ends are recorded
  there precisely so they are not re-chased.

## Checklist

Statuses: ☐ open · ◐ in progress · ☑ done · ❌ closed/disproven.

### Wave A — settle what faithful actually costs

1. ☐ A1 Measure which geometry carries which surface id — especially: is the flyable sea `soil=Water`?
2. ☐ A2 Carry the surface id from `materials.json` through to the collider
3. ☐ A3 Build the surface-name registry from the decoded list

### Wave B — the selection mechanism

11. ☐ B11 Replace crash-def selection with the original's indexed cascade
12. ☐ B12 Route `touchdown_*` graze selection through the same table

### Wave C — the destruct model and the cleanup

21. ☐ C21 Model the mid-air destruct as the original does (no `player_crash_*`, handoff on contact)
22. ☐ C22 Retire `CrashSurface`, and close `BL-059`

## Dependency and parallelism notes

**A1 gates the whole plan** — B11 as specified changes the def played by *every ordinary ground
crash* (`_dirt` → `_default`) and may also kill the sea splash, both shipped playtested behaviours.
The user decides what to do with those before any code is written. A2 and A3 are independent of A1
and of each other, and can run alongside it. B11 → B12 is a chain (B12 reuses B11's table). C21 is
independent of Wave B. C22 is last.

**File contention:** A2 and B11 both touch `SceneBuilder`; B11 and B12 both touch
`FlightController`. Do not run those pairs in parallel worktrees.

---

# Wave A — settle what faithful actually costs

## A1 ☐ Measure which geometry carries which surface id

**Goal.** Know, before changing any behaviour, what each chapter's *flyable* surfaces resolve to
under the original's rule — specifically: where the `dirt`(13) geometry actually is, and whether
diving into the sea reaches a `water`(1) material at all.

**Evidence (confidence: traced mechanism, unmeasured consequence).** Two shipped behaviours are at
risk, and both are playtested.

- **The ground crash.** The original plays `_dirt` only for id 13 — **3–9 materials per chapter**.
  Everything else on the ground is id 0 (or 5/8/11/12, which have no def) and resolves
  `player_crash_default`. Our build plays `_dirt` for all terrain *and* buildings.
- **The sea.** The original selects `player_crash_water` only for id 1 — **1–3 materials per
  chapter**. Our water crash fires off the *texture-name* classifier, which finds hundreds of water
  polygons per chapter (`FINDINGS.md`: `water` is 1.99–32.55 % of collidable area). Those two
  populations are not the same size, and nothing yet shows they overlap where a player can crash.
  The sea dive playing `player_crash_water` landed 2026-08-02, commit `60e9eff`.

**Approach.** Extend `soil_id_probe.py` (or add a sibling) to join materials → polygons → meshes:
for each chapter, report per surface id the polygon count, triangulated area, and the names of the
meshes carrying it. Reuse `class_area_share.py`'s area maths (strip order for `tri_strips`, fan
otherwise — matching `EmitPolygon`). Then answer in prose: (a) which mesh(es) carry id 1, and is any
of them the open water the player flies over; (b) which carry id 13, and is that geometry anywhere a
player would plausibly crash; (c) how much of each chapter's collidable area resolves to slot 0.

**Model recommendation.** medium — mechanical data joining over an established script, but the
write-up is a judgement call about what the numbers mean for playability.

**Verify.** The report names actual meshes, cross-checked against the known landmarks
`docs/formats/weapon-effects.md` already cites (C1's `g306` hangar ≈ `(-4258, 172, -6405)`, C2's
`nycity`). A claim that "the sea is id 1" must name the mesh and its area.

**⚠ Traps.** ⚠ **This item can end the plan as written.** Faithful selection means the ordinary
ground crash becomes `_default`, and — if the sea is not id 1 — no splash on a sea dive. Both are
visible regressions against passed playtests. Those are decisions for the user, not something to
implement around, and specifically not something to "fix" by keeping the texture-name path for water
only. Bring the numbers back and ask. ⚠ Do not measure water coverage with the *texture-name*
classifier and call it the surface-id answer — that conflation is the exact mistake this plan exists
to undo. ⚠ "The original is uglier here" is a legitimate finding, not a reason to quietly diverge;
`BL-060` exists precisely to hold deliberate improvements on the original, and anything kept for
looks belongs there with a note, not here unmarked.

## A2 ☐ Carry the surface id from `materials.json` through to the collider

**Goal.** Every collidable body can report the original's numeric surface id for the geometry that
was struck.

**Evidence (confidence: traced).** `GameZ.ParseMaterials` (`CSVM/src/Mech3/GameZ.cs:565`) reads
`Textured`/`Colored` bodies and **ignores `soil` entirely** — no occurrence of "soil" exists anywhere
in `CSVM/src`. The field is present on every material in the extracted JSON. `SceneBuilder` already
splits colliders per surface class (`CollidersForMesh`, naming them `col`/`col_water`/
`col_buildings`) and stamps `SceneBuilder.SurfaceMeta`, so the per-polygon split machinery this needs
already exists — it is currently keyed on the texture-derived class rather than the material id.

**Approach.** Add `SoilId` to `GameZMaterial` and parse it in `ParseMaterials` (map the label through
the table above; treat an unknown label as an error, not a silent 0 — a new label means the extractor
changed). Carry it into the collider split so a struck body can answer "what surface id am I". Prefer
extending the existing per-class split over adding a second parallel split; the ids and the classes
are different name spaces but they partition the same polygons.

**Model recommendation.** medium — mechanical plumbing through known seams, but it touches
`SceneBuilder`'s collider construction, which is high blast radius.

**Verify.** `--collision=show` overlay unchanged in shape; an 8-chapter `--freecam --det` regression
with **identical** collider counts (this item adds metadata, not geometry — a moved count is a bug).
A `--det` probe at a known water mesh logs the expected id.

**⚠ Traps.** ⚠ Collider counts must not move here. If they do, the split changed, and that is a
different (and much larger) change than this item. ⚠ Do not reuse `SurfaceMeta`'s string for this —
a numeric id stored as `"water"` re-creates the conflation.

## A3 ☐ Build the surface-name registry from the decoded list

**Goal.** Given a surface id, produce the name the original would produce, so `"player_crash_" + name`
resolves the same def the original resolves.

**Evidence (confidence: traced).** Ids 0–5 are compiled into `crimson.exe` (registry `0x00637b10`/
`0x00637b14`). Ids ≥ 6 are appended by `FUN_0055b7b0`, whose sole caller is the `LoadSoils
<filename>` script command (`FUN_005b80a0`, call at `0x005ba4ae`) — the filename is script data, not
an exe literal, so the list was read out of the shipped install: **`ZBD/zrdr.zbd` at `0xe631c`**,
eight contiguous names, `player enemy airstrip opensesame death buildings dzone dirt`, giving ids
6–13. `soils_list.py` reproduces the derivation and prints the table. The eight names fill exactly
the slots the material census uses, with 13 the highest id observed — see the plan's data section
for the three independent cross-checks.

**Approach.** Encode the six compiled-in names plus the eight from the soils list, with the append
rule's semantics: names are appended in file order, and the match test is case-insensitive over the
registry name's length accepting a trailing NUL *or digit* (`0x0055b874`), so a hypothetical `water2`
collapses onto `water`. No shipped name hits that case, so implementing the digit rule is optional —
but if it is skipped, say so in a comment rather than leaving the difference silent. Prefer reading
the list from `zrdr.zbd` at load over baking the fourteen strings into the source, if that is cheap;
baking them in is acceptable **with the provenance comment**, since they are script data.

**Model recommendation.** medium — the decode is done and written up; this is faithful transcription
of a settled table, with one judgement call (read-at-load vs bake-in).

**Verify.** The registry reproduces the full 14-entry table above. Cross-check against the census:
every id a shipped material carries (0, 1, 5, 8, 11, 12, 13) resolves to a name, and no id above 13
is ever produced.

**⚠ Traps.** ⚠ **Do not hardcode 13 for `dirt` without its provenance.** Nothing in the exe fixes
ids ≥ 6 — they come from a script-loaded file, and this table is what *this install* declares. ⚠ Do
not "tidy" the unused names away. `seafloor`/`quicksand`/`lava` (2/3/4) and `player`/`enemy`/
`opensesame`/`death` (6/7/9/10) carry no material in this install, but they occupy slots — dropping
them renumbers everything below and silently corrupts the whole table.

---

# Wave B — the selection mechanism

## B11 ☐ Replace crash-def selection with the original's indexed cascade

**Goal.** The def that plays on a crash is `vector[id]` for the struck material's id, with the
original's exact fallback behaviour — and `player_crash_default` is reached the way the original
reaches it.

**Evidence (confidence: traced).** `FUN_0048b920`, `0x0048bac5`–`0x0048bb00`: material null, or
`id < 0` (signed test, `JL` at `0x0048bab5`), or `id >= length` (unsigned, `JNC` at `0x0048bad2`), or
an empty slot → `vector[0]`; empty vector or empty slot 0 → the bare anim name `player`; otherwise
`vector[id]`. Today's code is `FlightController.ClassifySurface`
(`CSVM/src/Flight/FlightController.cs:1241-1244`), a two-way map off `ProjectilePool.ClassifySurface`,
with `EffectCatalogue.CrashDefNames` (`:68`) a hardcoded two-element array.

**Approach.** Replace `CrashDefNames` with the id-indexed vector built in A3, and
`ClassifySurface`'s two-way map with the cascade. `WorldEffectsFactory.BuildFlightCrashRuntime`
currently binds exactly two closures because the surface is only known at impact — it now binds one
per resolvable name. Keep the headless `--crash` force resolving slot 0 (it passes no struck body, so
the original's null-material arm gives `player_crash_default` — which is what it already does, for
the right reason this time).

**Model recommendation.** high — the cascade's arms are individually cheap and collectively easy to
get subtly wrong, and this is the item the whole plan exists for.

**Verify.** `--crash` still plays the slot-0 def. A real dive into whatever A1 identifies as id 1
plays `player_crash_water`. An 8-chapter `--freecam --det` regression, zero errors. Goldens: expect
movement only if a def actually changes for a captured shot — take the baseline first, and justify
each moved hash before re-pinning.

**⚠ Traps.** ⚠ The bare-`player` last-resort arm is real in the original; do not drop it as dead code
without checking it is unreachable in *our* build, and say which. ⚠ Do not preserve today's behaviour
by special-casing water — if A1 showed the sea is not id 1, that is a decision already taken at A1,
not something to re-litigate here.

## B12 ☐ Route `touchdown_*` graze selection through the same table

**Goal.** The survivable-scrape reaction picks its `touchdown_*` def by the same id lookup.

**Evidence (confidence: traced).** `FUN_0048d2c0` at `0x0048d425`–`0x0048d460` repeats `FUN_0048b920`'s
test byte-for-byte against a *global* vector (`DAT_0071c2e8`/`DAT_0071c2ec`) built with the
`"touchdown_"` prefix (`0x006274ec`) by `FUN_004735b0` — the same registry, the same index space.
Today `FlightController.GrazeReaction` (`CSVM/src/Flight/FlightController.cs:1833-1838`) picks from
`ProjectilePool.ClassifySurface`.

**Approach.** Reuse B11's table with the `touchdown_` prefix. One shared helper, two prefixes —
mirroring how the original has one registry and two vectors.

**Model recommendation.** medium — B11 does the thinking; this is the second application of it.

**Verify.** A wingtip scrape on the geometry A1 identified for each id plays the expected def; a
scrape on ordinary terrain plays whatever slot 0 resolves to.

**⚠ Traps.** ⚠ The `touchdown_` vector is a **global** in the original, built at level init, while the
crash vector is per-plane, built at plane setup. That difference is real; if it turns out not to
matter for us, say so explicitly rather than silently collapsing them.

---

# Wave C — the destruct model and the cleanup

## C21 ☐ Model the mid-air destruct as the original does

**Goal.** A plane destroyed in mid-air plays the original's canned destruct — **not** a
`player_crash_*` def — and the impact def plays when it reaches the ground.

**Evidence (confidence: traced mechanism, unmeasured fit).** `FUN_00498bf0` forks at `0x00498fc2` on a
signed field of the death event: negative → `FUN_004b82d0`, which plays a canned destruct anim and
sets the byte `this+0x91f`; non-negative → a synthetic impact record whose surface id is that same
field, driving `FUN_0048b920`. The handshake is in `FUN_0048b920` at `0x0048bb0a`–`0x0048bb30`: with
`0x91f` set, a resolved bare-`player` handle suppresses the anim, otherwise the running mid-air anim
at `this+0x6d8` is released before the crash anim starts. *Inferred, and flagged as such by the
decode:* that `FUN_004b82d0` is "the mid-air destruct" rests on it being the negative-arm sibling; what
sets `*(event+8)` upstream was not traced.

**Approach.** Nothing in M3 shoots the player down, so this item has **no trigger today** — it is the
model, not a feature. Land it as the shape the destruct path will have when the player becomes
killable, and as the reason `BL-059`'s "wire an air variant" framing is retired. Do not invent a
trigger to exercise it.

**Model recommendation.** medium — mostly a modelling and documentation item; the code surface is small.

**Verify.** No behavioural verification is possible without a trigger. The check is that
`docs/architecture.md` and the crash-rig comments describe the two-stage model correctly, and that
nothing in the build claims `player_crash_default` is the air variant.

**⚠ Traps.** ⚠ **Do not build a mid-air destruct trigger inside this plan.** That is a feature
(player damage) that M3 does not have; smuggling it in here is scope creep with a playtest attached.
⚠ The `this+0x19f ∈ {0,4}` condition inside `FUN_004b82d0` is undecoded — do not model it as though
it were understood.

## C22 ☐ Retire `CrashSurface`, and close `BL-059`

**Goal.** No enum in the build asserts a three-way surface model, and the backlog no longer carries a
disproven item.

**Evidence (confidence: traced).** `CrashSurface` (`CSVM/src/Flight/FlightController.cs:19`) has an
`Air` member that is unreachable by construction and, per this plan, describes a mechanism the
original does not have. Its doc comment (`:13-18`) states the disproven reading in prose.

**Approach.** Delete the enum once B11 lands (the id is the model). Rewrite the doc comments at
`FlightController.cs:13-18`, `:702-710`, `:1233-1240` and `EffectCatalogue.cs:65-68` to describe the
table. **Delete** `BL-059` from `backlog.md` per this repo's convention, recording the outcome in the
landing commit message. Check `BL-060`'s crash notes for anything the new mechanism invalidates.

**Model recommendation.** medium — mechanical, but the comments are load-bearing documentation and
a careless rewrite re-seeds the wrong reading.

**Verify.** `git log --grep=BL-059` tells the story cold; `backlog.md` no longer defines the id; no
occurrence of `CrashSurface` survives; `RunTests.ps1` green.

**⚠ Traps.** ⚠ Do not delete `BL-059` before B11 lands — a deleted item with no landed mechanism
loses the finding. ⚠ Grep for restatements of the "air variant" caveat elsewhere in the docs before
closing; this reading has been restated in code comments, not just the backlog.
