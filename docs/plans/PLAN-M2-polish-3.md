# Milestone 2 polish run 3

**✅ COMPLETE — 2026-07-22.** Eight of the ten items landed (1, 2, 4, 5, 6, 8, 9, 10); items 3
and 11 are **closed as disproven**, with no code landed and their measurements recorded below so
the dead ends are not re-chased. The last thing holding this plan in `docs/` was the user-owned
weather-zone A/B; the user settled C5 = `zone1` on 2026-07-22 and it is now recorded in
`docs/formats/weather.md` and in `Weather.ResolveZone`. Kept for its evidence and its dead ends,
read as history.

⚠ **What this plan did NOT fix:** the C5 / C1B / C3 ground z-fighting is still open. Three
mechanisms were proposed and all three were measured wrong (item 3, item 11). Do not restart
from this plan's hypotheses — start from `backlog.md`, which carries the surviving measurement:
`NodeOrderBias` (5e-8) sits ~20× below this renderer's depth-resolution floor (~1e-6 of view
distance), and raising the bias constants is measured to fix C1B while making C5 *worse*.

Ten items selected from `backlog.md` on 2026-07-22 against three criteria the user set:
**feasibility, little or no user input required, and a preference for long-running work.**
Items needing a playtest, two controllers, or a fidelity judgement the data cannot settle were
deliberately excluded — they stay in `backlog.md` under "Owed playtests" and "TUNE constants".

Each item is scoped to fit its own session: read its Goal/Evidence/Approach/Verify, then go.
Statuses: ☐ open · ◐ in progress · ☑ done — keep the checklist in sync as items land.

Ground rules carried over from every prior plan here: original-game data drives everything
(read the reader/compiled JSON before writing a handler, never guess a value); `CLAUDE.md` +
`docs/architecture.md`/`docs/formats/` are updated in the **same turn** as each landed item; a
landed item gets a dated entry in `docs/HISTORY.md`; verify against a full 8-chapter
`--freecam --chapter=<X>` regression (zero errors, same mesh/node counts unless the change is
expected to add coverage) plus a targeted screenshot at the location the report came from.
**Read `docs/verification.md` before measuring anything** — several items below hinge on
instruments that mislead.

## Checklist

1. ☑ `--data-root=` / `CSVM_DATA_ROOT` — let a git worktree run the game **(done 2026-07-22 — verified end-to-end against a real detached worktree with no `extracted/` and no `tools/`: byte-identical render, `docs/HISTORY.md`)**
2. ☑ Weather zones: C5 loads no fog at all; make the zone table data-driven **(done 2026-07-22 — C5's default now resolves `zone2`→`zone1`, byte-identical to the old build's explicit `--sky-zone=zone1`; 8-chapter regression clean; `docs/HISTORY.md`)**
3. ❌ ~~C5 ground z-fighting — coarse/fine draw priority~~ **(2026-07-22: premise DISPROVEN, no code landed — the prescribed fix was implemented and measured to change nothing, 35.77% → 35.79%. `g4683` z-fights its OWN coplanar polygon pairs within one material group. Rescoped as item 11.)**
4. ☑ C4 cloud deck does not follow the plane **(done 2026-07-22 — the deck is now picked structurally: the flat-quad root bucket whose footprint covers ≥50% of the World `area`. C4 resolves 144 tiles @y=1050 covering 100%; the other 7 chapters' deck verdicts and mesh/collider counts are unchanged. Two plan claims corrected: `sky*` would NOT have been safe — `skywal*` skins buildings and terrain inside the world walk — and the deck was never billboarded. `docs/HISTORY.md`)**
5. ☑ One billboard classifier; billboards and clutter lose collision **(done 2026-07-22 — `SceneBuilder.ClassifyBillboard` now serves all four call sites; clutter + every gamez billboard lose collision; the `spruce_destroy` = Spruce Goose misreading corrected in both docs. Net rendering change zero, C5 byte-identical. Found and fixed a live bug on the way: `skywal*` is a BUILDING texture that `IsCloudOrSkyTexture` was exempting, leaving C4's sky-city pods and `g74` (395×135×275 m) flyable-through. One plan cleanup deliberately NOT landed — the `csky_fog_on` alignment; the warning it was meant to remove does not exist and the padding fix enforces nothing. `docs/HISTORY.md`)**
6. ☑ `cblock*` 3D city-block clutter (C2/C5 missing buildings) **(done 2026-07-22 — the premise held in full: C5 gains 79,306 buildings and C2 10,261, both from templates that were previously counted, logged and dropped. Solid decorations now render through `SceneBuilder.SharedMesh` with the world's own materials and collide as a merged trimesh per 1024 m region. Two plan claims corrected: the item's scope is not just `cblock*` — C2's `filmblock*`/`resblock*`/`parklot*` are the same case and are the whole of C2's gain — and "a building needs its real orientation" is true in principle but inert in this data, since every authored basis is identity to within 0.108°. Cost: C5 `--fly` load +3.5 s. `docs/HISTORY.md`)**
7. ⤳ C3 beach z-fighting — **folded into item 11**, same suspected mechanism
8. ☑ `Loop { Count: 0 }` means infinite — C1 traffic drives its route once and stops **(done 2026-07-22 — C1/C2/C3 traffic now loops at exactly its authored route period, no runaway; 8-chapter regression identical. One plan claim corrected: the `zrdr` scope has 703 `Loop` events, not zero — but none with `LOOP_COUNT 0`. `docs/HISTORY.md`)**
9. ☑ `ScriptPlayback` compounds scale — the 1e29 zeppelin transforms **(done 2026-07-22 — the compounding was real and is fixed, but the 1e29 cause was something else: the unread `spline_interp` flag letting uninitialised memory be evaluated as spline coefficients. `docs/HISTORY.md`)**
10. ☑ Tail collision boxes swallow the outboard wings **(done 2026-07-22 — 9 boxes relabelled across 5 aircraft, every `*_rudder*` in the fleet untouched; scripted A/B turns one wingtip graze from `(tail→tail)` into `(wing→rightwing)` at identical vn/damage. Plan claim corrected: the strips are not "outboard", they cross the wing band — the box *centre* test is what works. `docs/HISTORY.md`)**
11. ❌ ~~**Per-polygon within-surface draw-order tie-break**~~ **(2026-07-22: premise DISPROVEN, no code landed — `g4683` has no self-overlapping polygons; the "8 coplanar pairs" came from an AABB test and the real outlines share edges. Implemented properly anyway and measured to change nothing at all three poses. What it DID produce is the first measurement of this renderer's depth-resolution floor (~1e-6 of view distance), which shows `NodeOrderBias` is 20× too small and reframes all three z-fight reports as one cross-node resolution problem. See below.)**

**Track record so far, and what it means for the remaining items.** Of the five items worked
in wave 1, **three had materially wrong evidence in this plan**: item 3's mechanism was wrong
outright, item 9's was an arithmetic coincidence (the real cause was elsewhere), and item 10's
prescribed test would have matched nothing. In both bad cases the *supporting* evidence agreed
while the mechanism did not — agreement on *where* is not agreement on *why*
(`docs/verification.md` rules 6 and 7). **Treat every Evidence section below as a lead to verify,
not a finding to implement. Landing no code with a correct disproof is a success here** — that is
exactly what item 3 delivered, and it prevented a change that would have regressed two chapters.

**Dependency notes (updated after wave 1).** Items 1, 2, 8, 9, 10 are landed; item 3 is closed
as disproven. What remains: **item 6 needs item 5** (5 removes the collision path 6 would
otherwise have to extend). **Item 11 subsumes items 3 and 7** — one mechanism, three or more
z-fight reports. Items 4, 5 and 11 are logically independent but contend on files: 4 and 5 both
edit `WorldBuilder.cs` predicates ~6 lines apart, and 5 and 11 both edit `SceneBuilder.cs`
(5 in the classifier, 11 in `BuildMesh`). Give each a stated file ownership boundary when running
them concurrently.

**⚠ Worktree hazard, learned the hard way 2026-07-22.** `git stash` is **repo-global and shared
across worktrees** — it lives in the common `.git` dir. Three of wave 1's five agents popped each
other's stashes; one built a "baseline" from another agent's in-flight edits and manufactured a
confident result in a chapter its change provably could not touch. **Never use `git stash` in a
worktree session here.** Use a local commit on your branch, or a file copy. Recorded in
`docs/verification.md`.

---

## Corrections to `backlog.md` (verified 2026-07-22, before item selection)

Four backlog entries were checked against the code and data and found **wrong or already
answered**. Apply these corrections to `backlog.md` as part of item 1's turn, so nobody
re-chases them:

- **"World renders into only the upper-left quadrant at the world origin" is NOT A BUG —
  delete the entry.** C1's World area is `left=-12288, top=-12288, right=0, bottom=0`: the
  world origin *is* the map's corner, and all terrain lies at x ≤ 0, z ≤ 0. At
  `--campos=0,30,420 --lookat=0,0,0`, `FrameCamera` derives yaw 0 (`PlaneViewer.cs:1751`), so
  camera-right is exactly world +X. The plane x=0 contains the camera and projects to the exact
  vertical centre line; the line (t,0,0) passes through the lookat target parallel to
  camera-right and projects to the exact horizontal centre line. Terrain fills the upper-left
  quadrant with two hard half-viewport edges — correct projective geometry, no clipping. There
  is no `SubViewport` in 1P at all (`PlaneViewer.cs:1123-1128` early-returns; `SplitScreen`
  clamps to 2–4 at `SplitScreen.cs:104-110`). The void is unfilled because `MapEdgeExtender` is
  deliberately off in plain `--viewer` (`PlaneViewer.cs:629`).
- **"A generic way to find billboard sprites" is largely already done — rewrite the entry.**
  The data-driven classifier landed 2026-07-21: `GameZ.cs:479-482` exposes `ModelType` /
  `FacadeMode`, consumed at `SceneBuilder.cs:446-459` (spherical) and `:468-470` (cylindrical).
  The backlog's specific example — "some face the camera only about X/Y (the harbour refinery
  flames)" — **is the case that already works**. What actually remains is consolidation, which
  is item 5.
- **C4's cloud deck is `Sky1.tif`, not `cloudtrans` — correct the entry.** `srock-cloudtrans`
  skins 35 models of 96–422 vertices with dy 162–533 m: cloud-shrouded *rock terrain*, which
  must stay solid. See item 4.
- **The C5 coarse quad is not an LOD sibling — the open question is answered.** See item 3.

Also fold in, as new backlog entries (they are real but out of scope here):

- **We ignore `zone_id` entirely** — see item 2's Evidence for the per-chapter counts.
- **Partition visibility is a real runtime system we do not implement** — see item 3.
- **`FogState` is a decoded animation event we do not act on** — see item 2.

---

## 1. `--data-root=` / `CSVM_DATA_ROOT`

**Goal:** let a `claude --worktree <name>` session build, run, `--screenshot=` and verify.
Today it cannot: `/extracted/`, `/CrimsonSkiesGame/` and `/tools/` are git-ignored, so a
worktree checkout has none of them, which removes this project's main verification instrument.

**Evidence.** `PlaneViewer.cs:275` hardcodes the root:

```csharp
var projectDir = ProjectSettings.GlobalizePath("res://");
_repoRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));
```

Every derived path — the complete surface, all in `PlaneViewer.cs`, no other file in
`CSVM/src` references `_repoRoot`:

| line | path |
|---|---|
| 260 | field declaration |
| 276 | `extracted/planes.zip` |
| 277 | `extracted/zrdr.zip` |
| 278 | `extracted/soundsh.zip` |
| 279 | `extracted/interp.json` |
| 280 | `extracted/messages.json` |
| 281 | `extracted/rof` |
| 484 | copied into the `StartSession` build closure |
| 489 | `extracted/<chapter>/texture.zip` (unless `--textures=`) |
| 491 | `extracted/<chapter>/gamez.zip` (unless `--gamez=`) |
| 493 | `extracted/<chapter>/<mission>/zrdr.zip` |

Scripts: `RunGame.ps1:34` and `RunDev.ps1:62-65` set `$RepoRoot = $PSScriptRoot` and derive
both the Godot exe (`tools\godot\…`) and the data root from it — those two need the override.
`ExtractAssets.ps1:70-76` and `ExtractRof.ps1:63-65` already take `-Source`/`-Dest`
**parameters**, so extraction is redirectable today and needs no change (it only ever runs once,
in the primary tree). `CleanScratch.ps1:62` is worktree-local and correct as-is.

Arg-parse convention to match (`PlaneViewer.cs:293-351`, one `else if` per flag):

```csharp
else if (arg.StartsWith("--sky-zone=")) { _skyZone = arg["--sky-zone=".Length..]; _skyZoneExplicit = true; }
```

**Approach.** One `_dataRoot` field, defaulting to `_repoRoot`, overridden by `--data-root=`
and then by `CSVM_DATA_ROOT` (flag wins over env var). Repoint the 9 asset paths at
`_dataRoot`; leave anything that is genuinely about the *repo* (nothing today) at `_repoRoot`.
Thread a `-DataRoot` parameter through `RunGame.ps1`/`RunDev.ps1`, keeping the Godot exe on
`$PSScriptRoot`. Add one `docs/cli.md` bullet alongside `--gamez=`/`--textures=`.

**Verify.** From a worktree, `RunGame.ps1 -DataRoot <primary tree>` launches and renders; a
`--screenshot=` there matches one taken in the primary tree byte-for-byte modulo the known
non-determinism in `docs/verification.md`. Without the flag, behaviour is unchanged (run the
8-chapter regression to confirm no path regressed). Apply the `backlog.md` corrections above in
the same turn, and delete the now-landed backlog entry.

---

## 2. Weather zones: C5 loads no fog at all

**Goal:** fix a live bug — **every C5 flight has been rendering with no fog and no sunlight
model** — and make the zone table data-driven instead of a hardcoded pair. Do **not** change
any chapter's default zone (user decision, 2026-07-22): the default stays `zone2`, with a
graceful fallback where that zone does not exist.

**Evidence.** `Weather.cs:166`:

```csharp
foreach (var zone in new[] { "ZONE1", "ZONE2" })
```

But `weather.zrd.json` is **per-mission** (53 files), and the zones it defines are per-chapter:

- C1, C1B, C1C, C2, C2B, C3, C4 → `ZONE1` + `ZONE2`
- **C5 → `ZONE1` + `ZONE3`** (all 8 missions)

Corroborated by the gamez horizon subtree node names: C5 has `zone1`/`zone3`, everyone else
`zone1`/`zone2`. So `Fog("zone2")` (`PlaneViewer.cs:1583`, default `_skyZone = "zone2"` at
`:147`) misses and falls through to `NoFog` (`Weather.cs:44`) — near/far 1e8/1e9, `WorldLight`
1 = fullbright. `--sky-zone=zone3` cannot rescue it, because `ZONE3` is never read.

> **Correction (measured 2026-07-22 while landing this):** "C5's dict is never populated" is
> wrong — the hardcoded loop *does* find `ZONE1`, so `--sky-zone=zone1` already worked on C5 on
> the old build. What was unreachable was `zone3` and the whole default path. The symptom, the
> fix and the fix's shape are unaffected; the useful consequence is that the old build's
> explicit `--sky-zone=zone1` render is the exact expected "after" image for the new default,
> which is how this was verified (byte-identical, 0/921600 px).

C5's two zones, for reference:

| | `ZONE1` | `ZONE3` |
|---|---|---|
| `FOG_COLOR` | `[0,0,0]` | `[16,16,16]` |
| `FOG_RANGES` | 1500 – 2250 | **50 – 250** |
| `CLIP_RANGES` | 5 – 2500 | **5 – 300** |
| `FOG_ALTITUDE` | 9000 – 10000 | 9000 – 10000 (identical) |

Note the identical `FOG_ALTITUDE`: in C1 the zones look like altitude bands (zone1 970–1047 at
the cloud floor, zone2 4000–5000), but in C5 altitude cannot be what selects between them.
`SW_ZONE*` is the software-renderer twin (`SUNLIGHT_ACTIVE 0`, ambient 1.0), not a third option.

**What selects a zone is not in the data.** Searched exhaustively 2026-07-22: mission `zrdr`
(`ia`, `objectives`, `targets`, `dzones`, `aiv`, `location`, `map`, `egen`, `net`,
`startanims`), all 53 mission `.gw` scripts (1,215 statements, zero zone mentions), and the
ROF/DLL string tables (`DEBUGINFO.TXT`'s six zone strings are Danger-Zones **UI widget** names —
`ozonestitle`, `o_radbutzone` — unrelated). The only zone references in the entire install are
chapter-level and mutually inconsistent:

| script | line | implies |
|---|---|---|
| `support\c1\load.gw` | `CameraSetHorizonXZ zone2_cloud_floor` | C1 → zone2 |
| `support\c1\tex_fx.gw` | `FindNode h_zone1scroll` | C1 → zone1 |
| `support\c1b\load.gw` | `CameraSetHorizonXZ moon_reflection` | not zone-named |
| `support\c1b\tex_fx.gw` | `FindNode h_zone1scroll` | C1B → zone1 |
| six other `load.gw` | `CameraSetHorizon horizon` | nothing |

**New decode — `zone_id`.** Every gamez node carries a `zone_id` field, undocumented until now:

| | −1 (always) | zone1 | zone2 | zone3 |
|---|---|---|---|---|
| C1 | 3529 | 2666 | 869 | — |
| C1B | 3500 | 2101 | 2 | — |
| C1C | 4181 | 146 | 1317 | — |
| C2 | 4189 | 766 | 1 | — |
| C2B | 3338 | 149 | 1414 | — |
| C3 | 3759 | 1647 | 2 | — |
| C4 | 5330 | 802 | 2157 | — |
| C5 | 9734 | 1555 | — | 149 |

Both zones span the **whole map** spatially, so these are alternative world variants, not
regions: `-1` renders always, `1`/`2`/`3` only when that zone is active. C1B, C2 and C3 have
1–2 nodes in their second zone and are effectively single-zone; C1C, C2B and C4 are
zone2-dominant; C1 and C5 zone1-dominant. **Nothing in `CSVM/src` reads `zone_id`** (grep is
empty), so we currently render every zone's geometry simultaneously.

**Mid-mission weather change is real but unused.** `FogState` exists as an animation event
kind, with exactly one occurrence install-wide —
`extracted/C1/M04/mis_anim/camera1-mission_intro_animation.json`, `reset_state/events[4]`:
`FogState { name: "drop_fog", color 0.69/0.69/0.69, altitude 10000–11000, range 1000–1500 }`.
It carries fog parameters **inline** and matches neither C1 zone, so it is an ad-hoc third fog
state on the cutscene camera, not a zone selector. Conclusion: the engine can drive fog from
script, but zone selection happens engine-side in the binary — the same shape as the `fire2`
trigger the user already searched the disassembly for without success (`backlog.md`).

**Approach.**
1. Read whatever `ZONE*` / `SW_ZONE*` keys the file actually contains, instead of the hardcoded
   pair. Keep the `SW_` variants out of the selectable set.
2. Keep the `zone2` default. When the requested zone is absent, **fall back to the chapter's
   first available zone and log it once**, rather than silently returning `NoFog`. That is what
   makes C5 correct without choosing C5's zone.
3. `WorldBuilder.BuildHorizon`'s `zone = "zone2"` default (`:206`) needs the same fallback, or
   C5 builds no horizon subtree.
4. Document `zone_id` in `docs/formats/` (new section or the gamez page) with the table above,
   and log the "we ignore `zone_id`" finding to `backlog.md`. Document `FogState` in
   `docs/formats/anim-definitions.md` as decoded-but-unacted-on, with the reason.

**Verify.** `--chapter=C5 --screenshot=` before/after: fog must appear. 8-chapter regression
confirms no other chapter's fog changed (C1–C4 already resolve `zone2` and must be
byte-identical). Log line fires exactly once for C5 and never for the others.

**Then hand the user the comparison task below** — do not pick C5's zone yourself.

---

## 3. C5 ground z-fighting — coarse/fine draw priority

> ### ⚠ EVIDENCE BELOW IS SUPERSEDED — read this box first (measured 2026-07-22)
>
> **The stated cause is wrong, and the fix it prescribes does not fix the bug.** A session
> implemented the coarse/fine draw-priority rule exactly as specified, measured it, and
> reverted it. What the measurements actually show:
>
> **The repro pose's z-fight is `g4683` fighting ITSELF — one node, one mesh, its own
> polygons.** It is not coarse-sheet-vs-partition-ground at all:
>
> | run at the repro pose (`--shots=5 --jitter=0.006`, flip threshold 8/255) | flip rate |
> |---|---|
> | unmodified build | 35.77% |
> | **the coarse/fine node rank this item prescribes** | **35.79% (no effect)** |
> | hide `g4616`+`g4425` (the two sheets nested inside `g4683`) | 35.79% (no effect) |
> | **hide `g4683` alone** | **0.19%** |
> | hide all 7 coarse sheets | 0.19% (identical — `g4683` is the whole effect) |
> | give every POLYGON its own draw-order rank | **21.92%** |
>
> `g4683` (node 1777, 34 polygons over 2048 × 11264) carries **8 pairs of its own polygons
> exactly coplanar at y = 5, same priority 0, overlapping by up to 768 × 512 units** — and
> five of those pairs are the same material (`cblock1.tif`), so `BuildMesh` groups them into
> one surface, where they receive an **identical** depth bias and can never be separated.
> Polygons 0–7 sit at x ∈ [−10240, −9216], z ∈ [−4096, −3072]: exactly where the repro
> camera looks. The fix direction is therefore a **per-polygon within-surface draw-order
> tie-break**, not a node-level rank — `SurfaceRankBias` only separates *(material, priority)*
> groups, and the original resolved equal-priority polygons by their order in the polygon list.
>
> **Also wrong in the evidence below:**
> - **"Generalises to C1 (`a6`), C1C and C4 (`g1612`)" — no.** Across all 8 chapters only
>   C1, C4 and C5 have *any* World-child mesh (1, 4 and 9). **C1C has none at all.** `a6` is
>   the detailed airfield tile (27 polys, 5.15% relative height, its own internal priorities
>   0/1/3); C4's `g1612` is a 477 m **cliff**, not a sheet. Demoting either would be a
>   regression, so the rule is not general — it is C5-only, matching 7 nodes.
> - **The coverage table's numbers did not reproduce.** An independent 64-unit rasterisation
>   against partition-root meshes in the same Y band gives 18.2 / 26.1 / 26.6 / 37.0 / 44.5 /
>   50.7 / 81.4 %, not 0.0–97.8 %. The *conclusion* survives — no sheet is 100% covered, so
>   culling would still leave holes — but the per-sheet figures should not be quoted.
> - **The seven "coarse-only" reference poses were not captured**, because the deliverable
>   presumes the coarse/fine model that the measurements disprove.
>
> What *is* confirmed: World children and partition roots are exactly disjoint (intersection 0
> in all 8 chapters), and the `terrain` node flag is set on every partition root and no World
> child — so "partition-referenced" is directly readable from the data. Coarse/fine coplanar
> overlap is real, but it is not what produces the reported flicker.
>
> **Item 3 stays OPEN.** Re-scope it as "same-material coplanar polygons within one mesh get
> no draw-order tie-break", which is a `SceneBuilder.BuildMesh` change affecting all chapters
> and needs its own regression pass. Nothing was landed.

**Goal:** resolve the long-standing C5 ground z-fight in favour of the detailed city surface,
**without** removing ground the player would otherwise fall through.

**Evidence.** The backlog's open question was "is the coarse quad a stray LOD tile, and which
surface should win?" Both halves are now answered statically.

**It is not an LOD tile.** `SceneBuilder.cs:132-134` keeps only the highest-detail level of
each LOD group:

```csharp
if (node.Kind == "Lod" && node.LodRangeMin != 0f)
    return null;
```

Neither surface sits under an `Lod` node. `world1`'s 105 direct children and the 471
partition-referenced roots are **exactly disjoint** (intersection = 0), and `WorldBuilder`
builds both sets unconditionally (`WorldBuilder.cs:155`, `:157-159`). We draw both because the
original selected between them at runtime — and the interp language confirms that system
exists: **`WorldPartitionSetActive`, 25 uses**.

**But the coarse sheets are load-bearing ground.** Rasterising each C5 coarse sheet on a
64-unit grid and asking what fraction a fine partition tile actually covers:

| node | idx | polys | footprint | covered by fine tiles |
|---|---|---|---|---|
| `g4632` | 1878 | 21 | 1792 × 6144 | 97.8% |
| `g4683` | 1777 | 34 | 2048 × 11264 | 78.4% |
| `g4425` | 2222 | 10 | 1024 × 1536 | 33.3% |
| `g4631` | 1879 | 9 | 1280 × 1024 | 20.0% |
| `g4616` | 1894 | 5 | 2048 × 1024 | 12.5% |
| `g4428` | 2224 | 12 | 1024 × 1280 | 8.8% |
| `g14550` | 2298 | 4 | 1024 × 1280 | **0.0%** |
| **total** | | | | **72.0%** |

**Hiding them would leave 28% of their footprint with no ground at all.** So culling is off the
table. (Beware: a naive "large flat quad" filter also catches the `fvol*` **fog volumes** — 10
in C1, 14 in C5, at altitude, not ground. Exclude them by name.)

`zone_id` does **not** explain the pair: coarse sheets and fine tiles are all `zone_id=1`.

**Approach.** Fix it as a **draw-priority** problem, not a visibility one (user decision,
2026-07-22). Give `world1`-child ground sheets a distinctly lower priority rank than
partition-referenced ground, so they lose every depth tie rather than having rank fall out of
draw order. Where fine ground exists (72%, including the whole repro area) the detailed city
wins, matching `OriginalScreenshots/C5 IA1 Terrain*.png`; where it does not (28%) the coarse
sheet is unopposed and still draws. Generalises to C1 (`a6`), C1C and C4 (`g1612`) as a rule —
"world-children ground ranks below partition ground" — not a C5 special case.

**Do not simply raise the bias constants** (`backlog.md`, and the C5 diagnosis in
`docs/HISTORY.md` 2026-07-21): a global bump makes the coarse quad win, which is backwards.

**Log partition visibility as future work** in `backlog.md`: the faithful port is to implement
`WorldPartitionSetActive` — show the coarse sheet only in cells with no resident fine tile.
Bigger (cell-resident tracking, pop risk, 8-chapter regression) and deferred to Milestone 3,
but now motivated by hard evidence rather than a hunch.

**Verify.** Repro pose `--viewer --chapter=C5 --sky-zone=zone2
--campos=-9533.178,76.319,-3367.413 --lookat=-9451.281,28.148,-3398.597` with
`--shots=5 --jitter=0.006` — a **sub-pixel** dither; the 0.15° default moves the camera far too
much to isolate depth flips. Baseline is 8.42% of pixels flipping; target is ≈0%. Then confirm
**no new holes**: the seven poses below must all still show ground.

**Deliverable for the user (requested 2026-07-22):** capture the seven coarse-only regions so
they can be A/B'd against the original — these are the areas where *only* the coarse sheet
exists, i.e. exactly what the original would have shown from its partition fallback. Write them
to `.scratch/` with descriptive names:

| sheet | bare | camera |
|---|---|---|
| `g14550` | 100.0% | `--campos=-4640,185,-10652 --lookat=-4640,5,-10912` |
| `g4428` | 91.2% | `--campos=-5600,185,-11356 --lookat=-5600,5,-11616` |
| `g4616` | 87.5% | `--campos=-9248,185,-12572 --lookat=-9248,5,-12832` |
| `g4631` | 80.0% | `--campos=-2592,185,-11548 --lookat=-2592,5,-11808` |
| `g4425` | 66.7% | `--campos=-8736,185,-11548 --lookat=-8736,5,-11808` |
| `g4683` | 21.6% | `--campos=-9120,218.5,-11036 --lookat=-9120,38.5,-11296` |
| `g4632` | 2.2% | `--campos=-12512,185,-10396 --lookat=-12512,5,-10656` |

All with `--chapter=C5`. Since item 2 landed, plain `--chapter=C5` already resolves to `zone1`
(C5 has no zone2) and the shots are fogged and comparable — passing `--sky-zone=zone1`
explicitly is equivalent and harmless.

---

## 4. C4 cloud deck does not follow the plane

**Goal:** C4's cloud deck should follow the camera like C1's, and stop being billboarded as a
sprite.

**Evidence.** The follow mechanism works (`PlaneViewer.cs:762` re-anchors `rig.Deck` to the
camera X/Z each frame, fed by `WorldBuilder.CloudDeck`), but the deck is selected by texture
prefix:

```csharp
// WorldBuilder.cs:77-78
private bool IsCloudLayerDeckNode(GameZNode n) => MeshUsesTexture(n,
    tex => tex.StartsWith("cloudlayer", StringComparison.OrdinalIgnoreCase));
```

consumed at `:193`, published only if non-empty (`:161-165`).

**The backlog's hypothesis (`cloudtrans`) is wrong.** C4 does ship `srock-cloudtrans.png`, but
it skins 35 models of 96–422 vertices with dy 162–533 m — cloud-shrouded **rock terrain**,
rooted partly under `world1`. It must stay solid.

**C4's actual deck is `Sky1.tif`**: 144 models, 144 parentless partition-referenced nodes
`g1720..g1863`, each a single 4-vertex flat 1024×1024 quad at **y = 1050** — bit-for-bit the
same signature as C1's `cloudlayer.tif` deck (144 nodes, 1024×1024, y = 960).

Deck inventory across all 8 chapters (large horizontal single-quad tiles):

| chapter | deck texture | tiles | size | altitude |
|---|---|---|---|---|
| C1 | `cloudlayer.tif` | 144 | 1024² | 960 |
| C1C | `cloudlayer.tif` | 144 | 1024² | 960 |
| C2B | `cloudlayer.tif` | 144 | 1024² | 960 |
| **C4** | **`Sky1.tif`** | **144** | **1024²** | **1050** |
| C1B, C2, C3, C5 | — none — | | | |

**The trap:** `Sky1.tif` is the **skydome** in C1/C1B/C1C/C2/C2B/C3 (2 nodes, under
`horizon/zone2`) and the **deck** in C4 (144 parentless nodes under `world1`). Widening the
predicate to `sky*` is only safe because `Build` skips the `horizon` subtree — an implicit
dependency, not a stated one.

**Approach.** Replace the texture-prefix test with a **structural** one: a flat single-quad
~1024² tile at a constant high altitude, reached through the world walk. Keep
`IsCloudSpriteTexture` (`WorldBuilder.cs:41-43`) complementary — whatever the deck predicate
matches must be excluded there, or C4's 144 deck tiles start billboarding. `IsCloudOrSkyTexture`
and `MapEdgeExtender.cs:341` already cover `sky*` for ground-tile exclusion and need no change.

C4's weather does define the band (`CLOUD_COVER TOP 1100 / BOTTOM 1000`), so the `HasCloudBand`
guard is not a blocker.

**Verify.** `--fly --chapter=C4`: the deck stays overhead as the plane translates, and does not
spin to face the camera. 8-chapter regression — C1/C1C/C2B decks must be unchanged, and
C1B/C2/C3/C5 must still resolve no deck (not a newly-matched skydome).

---

## 5. One billboard classifier; billboards and clutter lose collision

**Goal:** collapse the remaining per-case billboard heuristics onto the data-driven classifier
that already exists, and **remove clutter tree collision** (user decision, 2026-07-22).

**Evidence — the documented justification for tree collision is a misreading.**
`Clutter.cs:33-35` and `docs/formats/clutter.md:54` both say:

> "Trees are hittable, like the original (`spruce_destroy` anims exist; destruction is
> weapons-era behavior)."

The actual strings in the data are:

```
extracted/C2/M01 → "..\data\common\zrdr\planes\spruce_destroy1.zrd"
extracted/C5/M03 → "..\data\common\zrdr\planes\spruce_destroy2.zrd"
```

They are in the **`planes\`** directory. `spruce_destroy` is the **Spruce Goose** — Howard
Hughes' flying boat, the C2/M01 mission object, whose siblings in that folder are
`sprucegoose-fly_the_goose`, `free_the_goose`, `goose_cooked`, `goose_down_lwing`,
`goose_down_tail`, `spruce_enginedest`. **There is no spruce-tree animation anywhere in the
install.** Both doc claims must be corrected, not just the code.

**Current collision sites:**

| site | file:line |
|---|---|
| world geometry | `SceneBuilder.cs:196-213` (`AttachCollision`), called from `:165-166` |
| clutter trees/bushes | `Clutter.cs:527-547`, added at `:156-157` |
| map-edge clutter | `MapEdgeExtender.cs:268-276` |
| enabled by | `PlaneViewer.cs:523`, `:552` (`collision: _fly`) |

Exemption plumbing is `SceneBuilder.cs:135-136` (`collisionSkip`, subtree-inherited), whose
world-geometry caller is:

```csharp
// WorldBuilder.cs:65-71
private bool NoCollisionNode(GameZNode n) =>
    MeshUsesTexture(n, IsCloudOrSkyTexture) || IsFlareSpriteNode(n);
private bool IsFlareSpriteNode(GameZNode n) =>
    n.Polygons.Count == 1 && MeshUsesTexture(n, IsFlareTexture);
```

That poly-count-plus-texture-name heuristic is the last per-case rule: a `CylindricalY` tree
card with a tree texture, or any multi-poly spherical facade, is fully solid today.

**Approach.**
1. Extract one public classifier — e.g. `BillboardKind Classify(GameZMesh)` returning
   None/Spherical/CylindricalY/CylindricalX — from the currently-private
   `SceneBuilder.IsGlowSpriteMesh` (`:446-459`) and `GetCylindricalAxis` (`:468-470`). The
   pivot-recentre rule at `SceneBuilder.cs:327-352` keys off both and must follow it.
2. Replace `IsFlareSpriteNode` with a `Classify(...) != None` test, so **every** gamez billboard
   is collision-exempt. Note the seam: `NoCollisionNode` is node-level while the classifier is
   mesh-level — resolve via `n.MeshIndex`.
3. **Remove the clutter collider outright** (`Clutter.cs:156-157`/`:527-547` and
   `MapEdgeExtender.cs:268-276`) — not behind a flag; `git revert` is the escape hatch.
4. Leave `WingLights.IsFlare` (`WingLights.cs:25-31`) and `BillboardMoon`
   (`WorldBuilder.cs:243-284`) alone — both are documented deliberate exceptions, not debt.
5. Correct `docs/formats/clutter.md:54` and the `Clutter`/`WorldBuilder` bullets in
   `docs/architecture.md`, which currently assert the opposite.

**Small cleanups to fold in** (same files, both verified 2026-07-22):
- **`csky_fog_on` index mismatch.** `SceneBuilder.cs:762,776,778` declares `node_bias`(0),
  `csky_fog_on`(1), `csky_opacity`(2); `Clutter.cs:426` declares `csky_fog_on`(0). Godot merges
  the mapping per *instance*, and these families never share a `GeometryInstance3D`, so it is
  **latent, not live** — but it is exactly the trap that caused the unfogged-hilltops incident
  (`docs/architecture.md:7`). Adopt one shared ordered preamble so the invariant is enforced
  rather than remembered. Related: `PlaneViewer.cs:1182` omits `csky_opacity` from
  `InstanceShaderParams`, so a duplicated splitscreen deck loses any animated
  `OBJECT_OPACITY_STATE` opacity — arguably the more real defect of the two.
- **`WorldBuilder.DisableFog` is dead** — defined at `:342-348`, its only call site commented
  out at `:220`. Delete both, preserve the rationale comment, and fix
  `docs/architecture.md:15`, which describes it as disabled by an early `return` (it is not).

**Verify.** Collider count drops in `--fly` (C1 baseline was 2670 → 2566 when the flare
exemption landed; expect a further drop). Fly into a tree — pass through. Fly into a building —
still solid. 8-chapter regression on mesh/node counts. Shader-warning check: the C5
`csky_fog_on` warning at `instance_uniforms.cpp:62` should be gone.

---

## 6. `cblock*` 3D city-block clutter

**Goal:** C2 and C5 are visibly missing building clutter (user-confirmed 2026-07-22). Build the
non-sprite decoration path. **City-block buildings keep collision** (user decision,
2026-07-22) — they are real 3D meshes, not cards, so item 5's exemption must not catch them.

**Evidence.** The skip site is `Clutter.cs:184-192`, whose predicate is `SpriteInfo`
(`:270-286`):

```csharp
if (tex == null || mesh.Vertices.Count == 0
    || mesh.Polygons.Count != 1 || mesh.Vertices.Count != 4)
    return null;
if (max.Z - min.Z > 0.1f * Mathf.Max(max.X - min.X, max.Y - min.Y))
    return null;
```

with the reason stated verbatim at `:267-269`: *"3D decorations (C2's filmblock buildings,
7 polys / 64 m deep) don't fit the billboard path and are skipped."* Skips are deduped and
logged once per template at `:207-219`; a template with zero sprite decorations yields no
`Template` at all (`:220`).

What the templates carry: `interp.json` gives only **names** (`Clutter.cs:74-95`, tokens after
`AddClutterTemplates` in `support\<chapter>\adjust.gw`). The template root is a parentless
unreferenced `Object3d` found by name (`:226-238`); the ground node gives the terrain texture to
decorate and the tiling period (`:251-265`); decorations are `cbNNa.flt` 3D building meshes each
carrying a local translation (`:204`).

Why the pipeline rejects them: rendering is billboard-only — one `MultiMeshInstance3D` per kind
with a Y-axis-billboard shader (`:414-420`, `render_mode skip_vertex_transform`), and `Kind`
stores only `Width`/`Height` (`:103`), a flat card's extents. A 64 m-deep building has no
meaningful width/height, must not spin toward the camera, and needs a real orientation — which
the template supplies but `:204-205` discards, keeping only X/Z.

**Approach.** Route non-sprite decorations to a second path rather than the skip list:
- `Kind` (`:99-106`) carries a built mesh/subtree, not quad extents.
- Keep the full `deco.Local` basis (`:204-205`), not just the origin.
- Render with real `SceneBuilder`-built geometry and the world shader, not the billboard shader.
- Collision: per-mesh trimesh (`:527-547` currently assumes crossed quads) — and this survives
  item 5's removal, since these are meshes, not cards.
- `MapEdgeExtender.cs:226-277` consumes `KindExport` (`Clutter.cs:52-59`) — that contract
  changes with `Kind`.

**Verify.** `--freecam --chapter=C2` and `--chapter=C5`: buildings appear on the matching
terrain, upright and non-spinning, at the authored tiling period. The "skipped N non-sprite
decoration(s)" log line disappears for `cblock*`. Fly into one — solid. 8-chapter regression:
no other chapter gains or loses clutter. Watch draw-call count — this adds real geometry, so
check `--perf` for a frame-time regression.

---

## 7. C3 beach z-fighting — the beach must draw over the water

**Scope corrected by the user, 2026-07-22.** This item was originally written as "C3 clutter
planted in the sea", to be fixed with a `y > waterLevel` guard dropping submerged palms.
**That was wrong, and the guard would have deleted correct content.** The palms are visible in
the original: they are *supposed* to be there. What is wrong is that the **water is winning the
depth fight against the beach**, so the sand those palms stand on vanishes and they read as
growing out of the sea. The palms are a *symptom*, not the fault.

This therefore **merges with the C3 beach z-fighting entry in `backlog.md`** — same geometry,
same camera pose, one bug. Do not treat them as two items.

**Goal:** make the beach draw over the water at C3's shoreline. The palms then stand on visible
sand with no clutter change at all.

**Evidence.** The coplanar band is real and measured. C3 registers exactly one clutter template
(`AddClutterTemplates cliff1_sandtrans`, node idx 3232; ground node 3240 → texture
`cliff1_sandtrans.tif`, period 128 m; 6 `palmtree1.flt` decorations → `palm1.tif`).
**`cliff1_sandtrans.tif` appears on 102 world polygons, 86 of them perfectly flat at exactly
Y = 0.0**, plus 5 sloping −6→0; only 11 rise above zero. And Y = 0.0 **is** the C3 sea plane —
the exactly-Y=0 polygons there are `wtr00000.tif` ×1565, `shore2` ×1027, `shore1` ×487,
`cliff1_watertrans2` ×177, `sand128` ×140 and `cliff1_sandtrans` ×86, **all coplanar**.
`wtr00000.tif` is C3 material 220 with `"soil": "Water"`.

So ~84% of the palm-bearing sand band is exactly coplanar with the sea surface, which is why the
symptom is so widespread rather than a few stray trees.

**The target surface is not in doubt** — user-confirmed twice (2026-07-22): the beach wins. That
makes this the one z-fight case in the backlog where the resolution is known up front, unlike the
C5 ground case (item 3), where "which surface should win" was itself the blocking question.

**Approach.** Same class of fix as item 3 — make the right surface win a depth tie, rather than
changing what is drawn. Land item 3 first and reuse its mechanism if it generalises: both are
"coplanar surfaces sharing a draw priority, resolved by draw order". Here the pairing is by
*material/soil* (sand over `soil: "Water"`) rather than by world-child vs partition, so it may
need its own rank rule. Establish which one C3 actually needs before writing code — the polygons
are coplanar to the *bit*, so tie-break order is the whole game.

**Do not raise the bias constants globally** (`backlog.md`, and the C5 diagnosis in
`docs/HISTORY.md` 2026-07-21) — the same caution as item 3.

**Also still open on this pose, and genuinely separate:** the `.scratch/c3_whatisthis.png` frame
shows hard polygon-stepped edges at the sand/surf boundary and one clean triangular wedge. A
single still cannot separate depth flips from a static alpha-cutoff artifact, so measure with
`--shots=5 --jitter=0.006` (**sub-pixel**; the 0.15° default moves the camera far too much to
isolate depth flips) before assuming the mottling and the hard edges are the same fault.

**Verify.** `--viewer --chapter=C3 --campos=-6151.614,136.079,-3198.714
--lookat=-6150.76,135.796,-3199.151` with `--shots=5 --jitter=0.006`: pixel-flip rate at the
shoreline drops to ≈0, the beach is continuously visible, and the palms stand on sand.
**Clutter instance count must be unchanged** — if it moves, something dropped palms, which is
exactly the failure this rewrite exists to prevent. 8-chapter regression for the depth change.

---

## 8. `Loop { Count: 0 }` means infinite

**Goal:** C1's police/mafia/traffic cars drive their route once and stop; the original loops
them.

**Evidence.** `AnimRuntime.cs:1495`, `:1527-1536`:

```csharp
private int _loopsLeft = -2;  // -2 = no loop seen yet
...
case "Loop":
    if (_loopsLeft == -2)
        _loopsLeft = (int)(ev.Data.Num("value") ?? CountOf(ev) ?? -1f);
    if (_loopsLeft == 0) { _done = true; break; }
    if (_loopsLeft > 0) _loopsLeft--;
```

**Full install survey** (12,746 `mis_anim` + 3,368 `cam_anim` files; chapter/mission `zrdr`
scopes contain zero `Loop` events):

| `Loop.Count` | events | defs |
|---|---|---|
| `0` | **26** | **25** |
| `-1` | 2,919 | — |
| positive N | 530 | — |

**Every `Count: 0` is a ground-vehicle route animation** — nothing else in the install ships it:
- `C3/M02` — `studebaker1..9` / `stude_move1..9` (9)
- `C2/cam_anim` — `studebaker1..10` / `studebaker*_move`, `studebaker2_go` (10)
- `C1/cam_anim` — `black_car1`, `car_go_home`, `car_loop1`, `mafia`, `police_car` (×2),
  `truck1` (6)

**The risk the backlog flags — a `Count: 0` def that must terminate — does not exist.** No door,
one-shot, gate, hangar, bomb or explosion def carries it. Every `Count: 0` `Loop` is the **last
event of its sequence** (verified positionally on all 26), so "infinite" cleanly means "replay
the route". The shape is uniform:
`ObjectRotateState → ObjectMotionFromTo ×N → [StopSequence] → Loop{0}`, and
`ObjectMotionFromTo` carries explicit from/to, so a replay re-seats the car at the route start.
All are `activation: OnStartup`. The two degenerate two-event cases (`C2/cam_anim/studebaker1`,
`C1/cam_anim/mafia-mafia_move1`) cannot hang: `_iterScheduledTime` (`:1496-1498`) yields on a
zero-time iteration, and `ObjectMotionFromTo` schedules time regardless. The one name that reads
terminal, `car_go_home_start`, is a 23-event waypoint drive with two `PufferState` dust bursts —
structurally identical to its looping sibling `car_loop1_start`.

**Approach.** One condition at `:1530-1534`. Blast radius is 25 defs in C1/C2/C3 only; C1B, C1C,
C2B, C4 and C5 have none.

**Verify.** `--freecam --chapter=C1 --debug-anim`: the traffic defs stay live past their first
pass instead of reporting done. Watch for a runaway — confirm no def loops with zero elapsed
time per iteration. 8-chapter regression for animation counts. Update the `AnimRuntime.cs`
bullet at `docs/architecture.md:28`.

---

## 9. `ScriptPlayback` compounds scale

**Goal:** fix a confirmed latent defect that matches the reported 1e29 zeppelin transforms.

**Evidence.** Four of five transform writers are rest-based and cannot compound: `RestOf`
caches the authored pose (`AnimRuntime.cs:1794-1799`); `PoseTranslate`/`PoseRotate`/`PoseScale`
(`:1807-1829`) all write from `rest` (and `relative` is false in all 1,143 uses); `SpinMotion`
(`:1357-1371`) rotates from a stored `_rest`, and rotation preserves magnitude; `FromToMotion`
(`:1416-1441`) orthonormalises before scaling (`:1436`).

**The one non-rest-based writer is `ScriptPlayback.Seek` (`:1288-1296`):**

```csharp
var basis = Target.Transform.Basis;          // 1288 — CURRENT pose, not a stored rest
var origin = Target.Transform.Origin;
if (frame.Rotate != null) basis = new Basis(frame.Rotate.At(dt));   // 1291 — rebuilds, safe
if (frame.Translate != null) origin = frame.Translate.At(dt);
if (frame.Scale != null) basis = basis.Scaled(frame.Scale.At(dt));  // 1295 — MULTIPLIES IN
Target.Transform = new Transform3D(basis, origin);
```

When a frame carries `scale` but **no** `rotate`, line 1291 does not fire and line 1295
multiplies scale into an already-scaled basis every frame. `SiFrame.Parse`
(`CompiledAnim.cs:357-359`) sets each channel purely on JSON presence, so this is data-driven.

**The shape exists and the numbers match.** Across all 1,090 `.zan.json` (76,845 frames),
**207 frames have `scale` with `rotate == null`**, in 12 scripts — 143 of them the C1 zeppelin
gasbag scripts (`lkgasbag01` 48, `lkgasbag02` 42, `lkgasbag05` 38, `lkgasbag04` 13,
`lkgasbag03` 2), the rest parachute `stamp` scripts. **`lkgasbag02` carries
`scale.base.x = 1.52`**, and 1.52^150 ≈ 1e27 — ~2.5 s at 60 fps reaches exactly the reported
magnitude. These nodes are on the `hk_zep` chain
(`lkgasbag01/02 → zfronthalf → rock_zeppelin → noserotate → hk_zep`), which explains why one
`rock_zeppelin` instance blows up while siblings stay sane, and why C1/IA1 does not reproduce
(its interp script deactivates `hk_zep`).

**Honest gap:** I could not statically show this fires at bootstrap on M04. Their dispatcher
`hk_zep-lockleargoesdown.json` is `activation: OnCall` and is **not** in M04's start-anim
transitive closure (43 anims from `pzep_engines_start`, `train_on_track`, `pure_panic`,
`mission_intro_animation`). The one bootstrap-reachable SI dispatch on the reported chain
(`scene1 → ObjectMotionSiScript{piratezep}`) has **0** scale-without-rotate frames. So either
there is a path I missed, or a fifth writer exists.

**Approach.** Store a rest like every sibling runner does, so scale is applied to the authored
basis rather than the live one. Then close the M04 symptom with one runtime step: run
`--freecam --chapter=C1 --mission=M04 --debug-anim` and read off which node first grows — that
separates "`ScriptPlayback` via a path I missed" from a fifth writer. Note
`docs/verification.md:113`: the motion log prints **position only**, so a scale blowup shows up
indirectly.

**Secondary drift, worth noting but not the 1e27 cause:** `SpinMotion` captures
`_rest = target.Transform.Basis` from the **current** pose at construction (`:1342`), and the
idempotence guard at `:462` matches only on identical `(rate, runTime)`. `zeppelin_rocksleft`'s
five events use five different rate/runtime pairs on `rock_zeppelin`, so a looping call re-seeds
rest from an already-rocked pose and compounds the rotation. Bounded (orthonormal), so it cannot
produce 1e27, but it is a real defect — fix or log it.

**Verify.** M04 with `--debug-anim`: no `sound: … silenced — its host node's world pose is
degenerate` line (that message is the current symptom, `WorldSounds`), and `gasbag3`'s reported
origin stays finite. 8-chapter `--debug-anim` regression for pose sanity. If the runtime step
shows the blowup persists, **say so and keep the backlog entry open** rather than declaring it
fixed on the strength of the static finding.

### Outcome (2026-07-22) — the honest gap was the whole story

The "honest gap" flagged above was the real signal, and the runtime step it prescribed is what
found the bug. Both halves are recorded because the near-miss is instructive:

- **The static diagnosis was wrong about the mechanism, right about the files.** `ScriptPlayback`
  really did compound scale, and that is fixed — but it never produced the 1e29. The cause was
  **`SiScript.SplineInterp`, parsed since the SI-script reader landed and read by nothing.** The
  15 scripts that set `spline_interp: false` carry *uninitialised memory* in their coefficient
  blocks, and we evaluated it. C1/M04's `piratezep.zan` decodes a scale constant term of
  `(0.0, 4.259e27, 4.611e27)`: a singular basis, inherited by the whole
  `piratezep → … → rock_zeppelin → gasbagN → engineN → spin` chain. **12 of the plan's 12 flagged
  scripts are among those 15**, which is why the file-level evidence looked so convincing.
- **What settled it was an exact value, not a magnitude.** The 1.52^150 ≈ 1e27 arithmetic fits the
  observation and is a coincidence; the spline constant `4.6109513952913965e27` is *bit-identical*
  to the blown-up node's reported world X. Logged as a transferable rule in `docs/verification.md`.
- **The instrument's crash was the evidence.** The first probe threw out of `Basis.get_Scale()`,
  proving the corruption already existed inside `ScriptPlayback`'s **constructor during
  bootstrap** — which rules out per-frame accumulation outright.
- **The prescribed fix shape needed one correction.** "Store a rest like every sibling runner
  does" would regress `piratezep`, which sets its orientation in frame 0 and then ships 47
  translate-only frames: an absent channel means "hold the last value written", not "return to
  rest". The landed form keeps rotation, scale and origin as three separate running components
  seeded from the rest pose, which is non-compounding *and* preserves hold semantics.
- **`SpinMotion` was logged, not fixed** — see `backlog.md` for why both candidate fixes risk a
  visible regression to cure an invisible one.

---

## 10. Tail collision boxes swallow the outboard wings

**Goal:** a Bloodhawk wingtip strike should score as wing damage, not tail damage.

**Evidence.** `PlaneCollider.cs:63` sets `TailStartFrac = 0.7f`, and `:102-106` classifies
everything aft of it **at full span**, with the swallow acknowledged as intended:

```csharp
// tail = everything aft of tailStartZ (full width: fins, stabilizers, twin
// booms — outboard pieces sit in both tail and wing, harmless); ...
var tail = ClipAxis(tris, 2, tailStartZ, keepGreater: true);
```

One clip, on **axis 2 (z)** only — the wing (`:107-108`) and fuselage (`:109-112`) paths *do*
clip on axis 0. The refinement pass splits the slab but propagates the name verbatim
(`:184-187`), so the Bloodhawk's two flat 4.9 × 0.4 outboard strips (measured,
`docs/architecture.md:70`) — geometrically the swept **wing** trailing edge — stay named
`"tail"`. Then `PlaneDamage.cs:45-50`:

```csharp
"wing" or "canard" => localImpact.X < 0f ? "leftwing" : "rightwing",
"tail" => "tail",
```

`"tail"` is the only arm that ignores `localImpact` entirely. Clipping a hangar corner with a
wingtip 4 m off-centre subtracts HP from `tail`.

Distinct from the accepted canard-tip limit already noted in `docs/HISTORY.md`.

**Approach.** Rename outboard tail pieces **at split time** (`PlaneCollider.cs:106`/`:184`)
rather than side-splitting in `PlaneDamage` — the half-span is known at the collider, and
`MapStruckPart` would otherwise need a widened signature. That is the structurally right seam.

**Verify.** `--viewer --damage` with the mesh lab's collider-box overlay (M): the outboard
strips are named `wing`, the centre section `tail`. Then a scripted graze on a wingtip logs
`(leftwing)`/`(rightwing)`. Check all 11 aircraft for collider-name regressions — twin-boom
designs are the risk case, since their booms genuinely *are* tail at outboard |x|.

---

## Task for the user — weather zone A/B (requested 2026-07-22) — ☑ **C5 ANSWERED**

> **Answered for C5 on 2026-07-22: `zone1`.** The user flew C5/IA1 in the original and can see
> across the city — impossible under `zone3`'s 50–250 m fog and 300 m clip. The remake already
> renders `zone1` there (the `zone2` default matches nothing and falls back to the file's first
> zone), and that fallback is stable rather than lucky: **all 8 C5 missions list `ZONE1` before
> `ZONE3`**. Recorded in `docs/formats/weather.md` and `Weather.ResolveZone`, so nobody
> re-opens it or "fixes" the fallback toward `zone3`. **C1–C4 remain open** — see `backlog.md`;
> they all define `zone2` and resolve to themselves, so they render *a* correct-shaped answer
> either way and the A/B is a fidelity question, not a bug.

**Not schedulable work; this needs the original game.** Item 2 leaves the default at `zone2`
deliberately, because the data does not say which zone a mission flies (full negative result in
item 2's Evidence). Settling it needs the original.

**What to compare.** For each chapter, run the original and the remake from the same spawn
point and compare fog density, fog colour and visible draw distance:

| chapter | zones | note |
|---|---|---|
| C1 | zone1 / zone2 | `load.gw` names `zone2_cloud_floor` but `tex_fx.gw` names `h_zone1scroll` — the one chapter with conflicting evidence |
| C1B | zone1 / zone2 | zone2 has 2 nodes — effectively single-zone |
| C1C | zone1 / zone2 | zone2-dominant (1317 vs 146 nodes) |
| C2 | zone1 / zone2 | zone2 has 1 node — effectively single-zone |
| C2B | zone1 / zone2 | zone2-dominant (1414 vs 149) |
| C3 | zone1 / zone2 | zone2 has 2 nodes — effectively single-zone |
| C4 | zone1 / zone2 | zone2-dominant (2157 vs 802) |
| **C5** | **zone1 / zone3** | **no zone2 at all** — the open case |

**C5 is the one that matters most.** Its two candidates are far apart: `zone1` fogs at
1500–2250 with a 2500 clip; `zone3` fogs at **50–250** with a **300** clip, which would cull the
whole city into a 300 m bubble. Your own `OriginalScreenshots/C5 IA1 Terrain2.png` (horizon view
across the city) and `Terrain3.png` (overhead of the city ground) look impossible under zone3 —
but a still cannot settle it, and neither pose is matched to a remake camera.

**Cheapest decisive test:** in the original, fly C5 IA1 and note whether you can see the far side
of the city. If yes → `zone1`. If visibility collapses at ~250 m → `zone3`. Then
`--chapter=C5 --sky-zone=<answer>` in the remake and compare.

**Also worth answering while you are there** (both feed the fog work already in `backlog.md`):
does fog ever visibly *change* during a mission? The engine can do it — `FogState` is a real
animation event — but the data uses it exactly once, on C1/M04's intro cutscene camera
(`drop_fog`, range 1000–1500, altitude 10000–11000, matching neither C1 zone). If you see fog
shift mid-mission anywhere else, that is evidence the zone is switched engine-side, which is
currently our best guess for how zone selection works at all.

---

## 11. Per-polygon within-surface draw-order tie-break

> ### ❌ CLOSED 2026-07-22 — premise disproven, nothing landed. Read this box first.
>
> This is the **third** wrong mechanism for the same bug (item 3 was the first, this item's own
> Evidence the second). It was implemented in full, measured, and reverted.
>
> **1. `g4683` does not fight itself.** The "8 pairs of its own polygons exactly coplanar at
> y = 5, overlapping by up to 768 × 512 units" figure is an **AABB** overlap. Clipping the real
> outlines (true polygon ∩ polygon area) gives **zero** overlap for every pair. Its polygons 3
> and 4 — the worst-overlapping bounding boxes in the mesh — **share the edge
> (−9600,−3712)→(−10240,−4096) exactly** and lie on opposite sides of it. They are tiled ground.
> Install-wide the same substitution inflates the count of genuinely conflicting polygons from
> 0.6–1.6% to 9–26% (`docs/verification.md` rule 9).
>
> **2. The fix was built anyway, correctly, and moves nothing.** Per-polygon rank delivered in
> UV2.x, restricted by a Sutherland-Hodgman area test to polygons with a genuine coplanar
> overlapping sibling in the same surface, applied as a **pushback on the loser** so nothing is
> ever pulled toward the camera. Provably active (270 polygons in C5). Measured:
>
> | pose (`--shots=5 --jitter=0.006`) | before | after |
> |---|---|---|
> | C5 `g4683` repro | 35.96% | **35.96%** |
> | C3 beach | 0.41% | **0.41%** |
> | C1B | 30.95% | **31.00%** |
>
> 8-chapter regression: identical node/mesh counts, zero errors, no visible change (the largest
> genuine pixel delta, C3's 15,864 px, is invisible stipple at the shoreline; C2B's apparent
> 24,148 px is precipitation noise — same-build-vs-same-build gives 24,117).
>
> **3. The "35.77% → 21.92%" result that motivated this item was a step-size artifact.** A
> *blanket* per-polygon ramp is just a global bias bump. At 2e-6 it takes the C5 pose to
> **0.37%** — by floating the coarse sheet in front of the detailed city, i.e. the known-wrong
> surface wins (`docs/verification.md` rule 4). At 2e-7 it reaches only 33.20%. The 21.92% was a
> ramp too small to win the depth test, read as a noise floor.
>
> **4. What this item actually produced — the first measurement of the depth-resolution floor.**
> That ramp brackets it: **≈1e-6 of view distance** is the smallest bias that separates two
> coplanar surfaces at this view. `SurfaceRankBias` (2e-6) sits at the floor; **`NodeOrderBias`
> (5e-8) is twenty times below it**, so the cross-node tie-break is inoperative for nodes closer
> than ~40 indices — and has been since it was written.
>
> **5. The real mechanism, for all three poses: cross-node, and a resolution problem rather than
> an ordering one.** Nine *different* World-child nodes stack coplanar `cblock*` ground at y = 5
> in the C5 repro footprint (1777 `g4683`, 1799, 1800, 1801, 1813, 1814, 1822, 1823, 1837). The
> priority −10 members separate cleanly; the priority-0 members sit 36–60 indices apart
> (1.8–3.0e-6, straddling the floor) and 1822/1823 sit 1 index apart (5e-8, hopeless) — which is
> why the flicker is partial. Confirming control: `NodeOrderBias` 5e-8 → 2e-6 takes **C1B from
> 30.95% to 2.35%**. It is a diagnosis, **not a landable fix** — the same constant takes C5 to
> **41.69% (worse)**, because the node span is thousands and any step beating the floor covers
> tens of priority levels.
>
> **6. C3's recorded pose does not reproduce a flicker at all** (0.41%, beach visible, palms on
> sand). Either it is a *static* wrong-winner, which a jitter-flip metric cannot see, or the
> pose is wrong. **Ask the user for a fresh capture before spending another session on it.**
>
> **Next step, if this is picked up again:** make the cross-node bias **dense over the nodes that
> actually conflict** instead of uniform over all of them — e.g. rank only coplanar-overlapping
> node groups against each other and spend the available range on them. That is a scene-graph
> analysis in `WorldBuilder`/`SceneBuilder`, not a constant, and it needs the resolution floor
> above as its budget. Do **not** re-try a within-surface tie-break, and do **not** raise a
> global constant.

**Added 2026-07-22, replacing items 3 and 7.** Both were written around mechanisms that turned
out to be wrong (item 3's coarse/fine model was implemented and measured to change nothing;
item 7's clutter-placement model was corrected by the user). Item 3's investigation found what
looks like the real one, and it plausibly covers several open z-fight reports at once.

**Goal:** make coplanar polygons *within a single mesh surface* resolve by their draw order, the
way the original does, instead of sharing one depth bias and fighting.

**Evidence** (measured 2026-07-22 by item 3's investigation, and the one part of that item that
survived):

- `SceneBuilder.BuildMesh` groups polygons by **(material, priority, sidedness)**. Every polygon
  in a group lands in one surface with **one shared depth bias**, so no node-level tie-break can
  separate them.
- C5's `g4683` (node 1777) carries **8 pairs of its own polygons exactly coplanar at y=5,
  priority 0**, overlapping by up to 768×512 units; five of those pairs are on the same material
  (`cblock1.tif`), so they land in the same surface. Polygons 0–7 sit exactly where the C5 repro
  camera looks.
- Hiding `g4683` alone collapses the repro flicker to the same 0.19% as hiding all seven coarse
  sheets — it is that single mesh fighting itself, not two nodes fighting each other.
- **A per-polygon draw-order rank moved the repro from 35.77% to 21.92%** — the only intervention
  that moved it without deleting geometry.

**Established format fact this rests on** (`CLAUDE.md`, "Format gotchas"): polygons carry a signed
draw priority, and **equal priorities resolve by draw order, later wins** — polygon list order
within a mesh. We honour that *across* nodes but not *within* a surface. That is the gap.

**Approach.** Give each polygon within a surface a rank derived from its position in the polygon
list, and fold that into the existing depth-bias path so later polygons win. The existing
node-level bias must keep working — this is an additional, finer term, not a replacement.

**Before writing code, establish the noise floor at the repro pose.** Item 3 measured that most
of the 35.77% baseline is **grazing-angle mipmap/aniso resampling, not depth flips** — so 21.92%
may already be at or near the floor, i.e. the per-polygon fix may have *solved* it. Run the same
build twice at that pose with `--shots=5 --jitter=0.006` and get the floor first, or you cannot
tell "fixed" from "improved" (`docs/verification.md` rules 2 and 7).

**Then test the generalisation, which is the real prize.** The same shape may explain:
- **C3's beach z-fight** (old item 7) — `--campos=-6151.614,136.079,-3198.714
  --lookat=-6150.76,135.796,-3199.151`. 86 of `cliff1_sandtrans`'s 102 polygons sit at exactly
  Y = 0.0, coplanar with the sea. **The beach must win** (user-confirmed twice). The palms are
  correct content and must NOT be removed — **clutter instance count must not change.**
- **C1B's z-fight** — `--campos=-7698.844,48.763,-5797.924 --lookat=-7749.957,-20.093,-5849.367`.
- The **runway `lite*`/`ltout*`** state-variant quads in `backlog.md`, which still z-tie.

Report which of these it fixes and which it does not. A fix for one that regresses another is
worse than no fix.

**Verify.** Same-build noise floor first, then before/after at all three poses above, all with
`--shots=5 --jitter=0.006` (**sub-pixel**; the 0.15° default moves the camera far too much).
Full 8-chapter regression — this touches every mesh in the game, so mesh/node counts must be
identical and no chapter may gain new z-fighting. Watch `--perf`: a per-polygon term in the hot
build path could cost load time.

**If it does not work, say so and land nothing.** That is what item 3 did, and it was the right
outcome.
