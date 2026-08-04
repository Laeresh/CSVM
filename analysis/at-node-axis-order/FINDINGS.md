# `AT_NODE` positions are already in the engine's frame — no axis swap (`BL-221`)

`census.py` collects every `AT_NODE`-style position in the install, from both anim front-ends and
all six event kinds that carry one, and tests the two candidate readings against three
instruments. Run it from the repo root:

```
python analysis/at-node-axis-order/census.py            # the three instruments
python analysis/at-node-axis-order/census.py extracted --rejected   # + the dead one
```

## The question (opened `BL-221`, 2026-08-01)

`touchdown_default` calls five `small_yellow_sparks` at `spark_touchdown` + `(0, 8, −2)`,
`(±1.5, 8, 0)`, `(±4, 8, 0)`. Read verbatim in the engine's frame that is a rake **8 m above** the
site (what the pilot saw, `PT-24`); read as Z-up authoring — second component forward, third up —
it is five sources across the wing **8 m ahead**, a leading-edge scrape. The `±1.5`/`±4` spread
matching a wingspan made the second reading plausible enough to be worth settling before `C8`
placed the world-effect template *meshes* through the same code path.

Formally, for an authored triple `(a, b, c)` in the engine's right-handed Y-up / nose-−Z frame:

| | reading |
|---|---|
| **A** verbatim | `(x, y, z) = (a, b, c)` — what the engine implements today |
| **B** Z-up | `(x, y, z) = (a, c, −b)` |

The first component is spanwise under both, so nothing here tests it.

## The verdict (2026-08-04)

**Reading A. The engine is already right; no flip, and no code changed.** All three instruments
agree, and the strongest one needs no judgement about what a "sane" placement is.

## What the install ships

| | positions |
|---|---|
| total `AT_NODE`-style positions | **6,728** (5,935 compiled, 793 reader) |
| exactly zero — both readings agree | 5,910 |
| pure X (`b = c = 0`) — blind, A and B are the same point | 48 |
| **discriminating** | **770** |
| distinct `(anim, kind, host, offset)` authored shapes among those | 539 |

Discriminating rows by event kind: reader `AT_NODE` 423, `CallAnimation` 200, `PufferState` 94,
`LightState` 53. (`Sound`, `SoundNode` and `DetonateWeapon` carry the field too; every one of their
positions in this install is zero.)

## Instrument 1 — paired sign (the decisive one)

`wv_turrets.zrd.json` defines `wvutur*` and `wvctur*`. The two definitions are identical — same
health, same reset, same `destroyit` sequence calling `small_fireball` + `small_yellow_sparks` at
`AT_NODE healthy` — **differing only in the sign of component `b`**:

| def | binds | offset | measured node placement |
|---|---|---|---|
| `wvutur*` | `workersvoyagezep` / `utur*` | `(0, +2, 0)` | 10 placements, local y **+42.7 … +56.5**, parented to `gasbag1…8` |
| `wvctur*` | `workersvoyagezep` / `ctur*` | `(0, −2, 0)` | 21 placements, local y **−81.8 … −30.0**, parent literally named **`underneath`** |

Component `b` flips sign exactly with above-the-hull vs below-the-hull, so **`b` is the vertical
axis** — reading A. Under B the two defs would mean "the topside turrets explode 2 m ahead and the
underside ones 2 m astern", which neither the node names, the parenting, nor anything else in the
data motivates. This test asks only "which component tracks up/down"; it needs no model of what a
plausible effect placement looks like, which is why it carries the verdict.

## Instrument 2 — sibling spread

A host carrying ≥3 distinct offsets is an object with effects laid out **over** it: spread along
it, near-constant across the short axes. Scoring the *spread* (not the offsets) against the host's
own bbox extents is immune to the "effects legitimately sit above their host" bias, because a
constant offset does not move a spread. Restricted to hosts whose name resolves to a single node
per chapter, and deduplicated so one authored shape replicated per instance
(`tankerfreight01…12`) is one vote:

**A 8 · B 1 · tie 0.** Score = worst (spread ÷ host extent) over the two testable axes; lower fits.

| verdict | group | n | A | B | host y / z extent |
|---|---|---|---|---|---|
| A | `steinmann_sink` @`steinmann` | 7 | **0.74** | 3.00 | 55.0 / 222.1 |
| A | `shipsink` @`redcross` | 7 | **0.72** | 2.66 | 62.0 / 230.7 |
| B | `tankerfreight01` @`freight01` | 3 | 2.00 | **0.31** | 2.0 / 12.9 |
| A | `zep_splashes` @`cargozep1` | 29 | **0.36** | 1.36 | 184.0 / 687.8 |
| A | `freight_is_toast` @`hold` | 3 | **0.29** | 0.98 | 7.1 / 24.1 |
| A | `destroy_pwr_station` @`pwr_engines` | 3 | **0.86** | 1.30 | 4.6 / 7.0 |
| A | `bridge_destroy01` @`bridge_truck01` | 3 | **0.26** | 0.51 | 5.9 / 17.6 |
| A | `torpedo_trail` @`a_torpedo` | 3 | **0.13** | 0.31 | 1.6 / 3.9 |
| A | `drop_paratroopers` @`cargozep2` | 7 | **0.09** | 0.24 | 272.2 / 687.8 |

The two clearest are ships. `shipsink` blows up the hospital ship `redcross` at seven points
spanning `c` = −95 … +70 with `b` constantly 0: under A that is 165 m of explosions along a 231 m
hull at deck height; under B it is seven explosions stacked over a 165 m vertical range on a hull
62 m tall. `zep_splashes` is the same shape at zeppelin scale — `b` constant at −200 (the sea below
an airborne zeppelin), `c` spread 250 m along a 688 m hull.

**The one dissent is honest and weak.** `tankerfreightNN` puts three fires at `(0,1,0)`, `(0,3,0)`,
`(0,5,0)` on a crate 2.0 m tall and 12.9 m long. Under A the top two float above the crate; under B
they sit along it at deck level. A blaze climbing a burning cargo stack is a perfectly ordinary
reason to author the first, so this row does not discriminate — it is recorded rather than dropped.

## Instrument 3 — known placement

Hosts whose right answer comes from outside the offset. Full text in the script's output:

| def @ host | offset | A | B |
|---|---|---|---|
| `muzzle_burst_ap` (0.9 m barrel node) | `(0, −0.2, −1)` | 1 m along −Z = **out of the muzzle**, 0.2 m under the bore | 1 m straight **down**, 0.2 m astern |
| `he_ground_effect` @`he_ring` (flat disc, bbox y = 0.10, r = 4.2) | `(0, 12, 0)` | fireball + 2nd ring **12 m over the crater** | 12 m sideways along an axis the disc is symmetric about |
| `cghookup` @`zcrane` (jib modelled y −50.4 … 0) | `(0, −8, 0)` | hook **hangs 8 m below the arm** | hook swings 8 m aft |
| `freighterlite` @`freighterlight2` | `(0, 9.27, 0)` | lamp **9.3 m up the mast** | lamp 9.3 m astern at deck level |
| `splashpuffer2/3` @`waterfall01` (bbox y −0.0 … 175) | `(±11, 8, −8)` | spray **±11 m across the fall, 8 m up its face** | spray 8 m *below* the base |
| `goose_splashleft*`/`right*` | `(±2, 0.2, 0)` | spray 20 cm **above the waterline** | spray 20 cm astern, exactly at emitter height |
| `lightning` @`lstage1` | `(0, 200, 0)` | **200 m up** | 200 m along +Z at ground level |
| `volcano1` @`world1` (the world root) | `(−4480, 390, −6400)` | plume at **altitude 390 m** | plume **6.4 km below sea level** |

The waterfall row is the one already settled outside this census: the `c1-waterfall` golden renders
those three puffers under reading A and is pinned, so a flip would have had to move it.

## ⚠ The instrument that had to be thrown away

The obvious mechanical test — score each reading by how far outside the host's bounding box it
lands — **manufactures its own answer**, and it is kept in the script behind `--rejected` so nobody
rebuilds it. Effects legitimately sit *above* flat hosts, and a flat host has a near-zero vertical
bbox extent, so any per-axis-normalised score rules against whichever reading is vertical no matter
what the data says. Run as written it reported **63 A / 101 B**, headed by
`he_ground_effect`@`he_ring` at `escapeA = 11900` — that is a fireball 12 m over a ground ring whose
box is 8.4 m wide and **0.0 m tall**, i.e. the ratio is a division by the ring's thickness, not a
finding. Recorded in `docs/verification.md` as the transferable rule.

## What this does NOT settle

**Where the offsets are measured *from*.** `touchdown_default`'s five sparks really are authored
8 m above their host, and the host is the def's own template root `spark_touchdown` — a parentless,
meshless staging root that the runtime relocates to the call site. `FlightController.GrazeReaction`
places it at the **contact point** (`graze.siteAtContact`, default true), so the sparks land 8 m
above the contact point, which is exactly what `PT-24` saw. That is the staging-site question, and
this census removes the axis-order alternative from it: `graze.siteAtContact` is now the only
remaining lever.

The def names **two** hosts — the five sparks on `spark_touchdown`, its own `collide_puffer` on
`MAIN_ROOT_NODE` at `(0, −0.5, −2)` — but in our runtime they are the same node: `MAIN_ROOT_NODE`
and `INPUT_NODE` are sentinels for "the node this def was invoked on", which `AnimRuntime.
IsSelfNodeRef` resolves to the anchor, and the anchor is the relocated `spark_touchdown` root.
So both the −0.5 m puffer and the +8 m sparks measure from the staged site. Whether the original
kept them apart — the puffer riding the aircraft while the spark template sits somewhere else — is
a capture question, not a data one.

**`world_velocity`.** `small_yellow_sparks`' `(0, 10, 0)` reads plausibly under both readings and
was never cited here.
