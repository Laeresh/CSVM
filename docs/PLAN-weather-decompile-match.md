# Weather decompile match — runtime zones, cloud deck, sky dome, fog volumes

**ACTIVE PLAN** (written 2026-08-09). It sits in `docs/`, which by this repo's convention makes it
a live plan; PROJECT_CONTEXT.md's "Current status" names it. Move it to `docs/plans/` with a
`COMPLETE` banner, and add its row to [`plans/plans.md`](plans/plans.md), when every item lands.

This plan lands the 2026-08-09 Ghidra decompile of `crimson.exe`'s weather engine in the remake:
the runtime camera-zone state machine (ZONE1 below the deck / ZONE2 above / ZONE3 inside a fog
volume), the `zone_id` visibility gate it drives, the cloud deck's true two-object mechanism (a
world-fixed authored floor above, the zone-1 sky dome's own scrolling ceiling below), and C5's
fog-volume interior whiteout. It supersedes the parts of `PLAN-overcast-match` (`A7`, `B11`,
`B12`) that modelled the deck as one engine-moved mesh and the zone as a fixed per-mission
selection — those were behaviourally right and mechanically wrong, and the differences are
visible (an ~87 m floor error in C1/C1C, a 2.3× fog-range error below the deck).

Out of scope, deliberately: precipitation (decoded and rendered, `weather.md` item 5), `WIND`
consumption (parked with the static clutter field, `BL-273`), water's unmodulated rendering
(`BL-304`), directionally-moonlit night sprites (`BL-325`), and the sky→tops transition-depth
mystery (explicitly evidence-free for placement causes — see `fogvol.md`). Matching the
original's exact `rand()` scatter sequence is also out: the fixed seed `0x9b3a9ce2` is recorded
in `fogvol.md`, but determinism-by-property (`--det`) already serves the project's need.

## Milestone goal

- The deck chapters (C1/C1C/C2B/C4) render below-deck flight with **ZONE1's** fog, clip and
  sunlight, and above-deck flight with **ZONE2's**, switched invisibly inside the whiteout core.
- The cloud deck is two objects, as authored: the 144 world-fixed tiles at their **authored
  altitude** above; the zone-1 dome's **scrolling ceiling cap** (~396 m over the camera, C1)
  below.
- World content honours the gamez `zone_id` gate per camera state, as the original culls it.
- C5's fog volumes whiteout on entry and hand off to **ZONE3** fog inside.
- Every changed look is A/B'd against original footage or a decompiled constant, not tuned by eye.

**No new weather behaviour may be invented — every change in this plan traces to a decompiled
function or an authored value.** Where the decompile gives a mechanism but the render chain needs
a judgement call (blend order, shader plumbing), the call is marked TUNE and recorded.

## Decisions (2026-08-09)

| # | Question | Decision |
|---|---|---|
| 1 | Switch altitude: band centre (A7's deck-regime flip) or centre − THICKNESS/2 (binary's state edge)? | **Centre − THICKNESS/2 for the zone state**, keeping the deck-regime flip at the centre — both sit inside the opaque core, and the binary computes them as two separate thresholds (`0071c2d0` vs the A7 flip). Do not unify them. |
| 2 | Above-deck floor altitude: band centre or authored tile altitude? | **Authored tile altitude** (C1/C1C/C2B 960, C4 1050). A7's "exactly on the centre" was C4's coincidence — C4 authors centre = tile altitude. |
| 3 | Below-deck ceiling: relocated deck mesh (current) or the zone-1 dome's own cap? | **Zone-1 dome cap** (`h_zone1scroll` + skirt), camera-anchored, UV-scrolled. The relocated-deck trick was the remake's reconstruction of behaviour the dome geometry provides authored. |
| 4 | How much `zone_id` culling fidelity? | **Full gate** (mission nodes included — the original hides zone-1 targets above the deck), behind one switch (`--no-zone-cull`) so any regression can be isolated at the controls. |
| 5 | `--sky-zone` semantics? | Becomes a **state override** (forces camera state 1/2/3, empty dome and all) for inspection and repro poses; default is the runtime state machine. `WeatherState.ResolveZone`'s file-fallback logic is kept for missions with no matching zone. |
| 6 | C5 whiteout implementation surface? | Screen-space blend driven from `WeatherRig.Tick` (same channel the band whiteout uses), NOT per-volume fog meshes — the original computes one camera-space density and blends the frame. |

## ⚠ Read this before implementing anything

Claims disproven by the decompile — do not re-derive them:

| # | The wrong claim | How it died |
|---|---|---|
| 1 | "Which zone a mission flies" is a fixed per-mission selection made engine-side | `FUN_0042ee40`/`FUN_00472ea0`: the camera switches states 1/2/3 per frame; a deck chapter flies both zones in one flight |
| 2 | The gamez `zone_id` census is "not admissible; runtime meaning unknown" (`weather.md` ⚠, retired by A1) | `FUN_0056c430`: a node draws iff its `zone_id` ∈ camera zone set `{0, state}` or is −1. The "mission content unbuilt whichever zone is chosen" objection dissolved — the gate is per-frame |
| 3 | `fogvol.zrd` `fog_zone` is "an index into something still unidentified" | `FUN_0044e010` stores `value != 0`; it arms the in-volume whiteout + ZONE3 state. C5 (=1, only ZONE3 author) and C1 (=0, no ZONE3) agree |
| 4 | The above-deck floor sits "exactly on the band centre" (A7) | Tiles are world-fixed `zone_id 2` geometry at their authored altitude; C4's centre *equals* its tile altitude (1050), which is the coincidence that made the pin look right. C1: authored 960 vs centre-pin 1047 |
| 5 | The below-deck ceiling is the deck mesh, engine-carried at cam+400 (A7) | It is `horizon/zone1`'s `h_zone1scroll` — camera-anchored dome geometry with an authored cap centre at **+396.4 m** (C1 model 768), which is A7's measured "~400 m" to instrument precision |
| 6 | C1's `h_zone1scroll` UV-scroll script line is dead décor on an unflown zone | Zone 1 is flown below the deck; the scroll (0.07/s, `tex_fx.gw`) is the drifting below-deck ceiling |
| 7 | `VIEWING_RANGE` is unused | The original scales fog ranges and far clip by its per-detail `FOG_SCALE`/`CLIP_SCALE`; HIGH authors 1.0 everywhere, so *ignoring it stays correct at full detail* — the claim was half right |

Confidence roll-up:

| Confidence | Items | What that means for you |
|---|---|---|
| **Traced to an exact mechanism in code, with the data that proves it** | A1, A2, B11, B12, B13, C21, C22 | Confirm the trace (function addresses in "What the data actually ships"), then implement. |
| **Direction sound, magnitude a judgement call** | B14, B15, D32 | The *what* is settled (dome ceiling, unscaled vertical anchor, flicker exists); blend order, scale plumbing and curve constants are TUNE — record them in `backlog.md`'s TUNE list. |
| **Leads only — no mechanism yet** | D31 | Budget for investigation; may end in a disproof. |

**⚠ Worktree hazard.** `git stash` is repo-global and shared across worktrees — never use it in a
worktree session here; use a local commit or a file copy.

## What the data actually ships

All decompile facts are reproducible from `crimson.exe` in Ghidra at these addresses (the
ghidra-mcp plugin's REST API on `127.0.0.1:8089` serves them; decompiled sources from the
2026-08-09 pass are in that session's scratchpad, but the addresses are the durable citation).

**The state machine** — `FUN_0042ee40` (per-frame, called from `FUN_0042f480`):
state 1 default; state 2 when camera altitude ≥ band centre − THICKNESS/2 (precomputed at world
init `FUN_004735b0` into `0071c2d0`; the check sits inside the CLOUD_COVER-exists gate); state 3
when inside any `fvol` volume and `fog_zone` set. On change `FUN_00472ea0` applies the zone: world
fog range × detail `FOG_SCALE`, fog altitude pair, fog colour (world+0x38 block), camera clip
from `CLIP_RANGES` (× `CLIP_SCALE`, second camera far clamped ≥ 6), full `SUNLIGHT_*` onto the
gamez `sunlight` node. Hardware indexes `ZONE1–3`; software `SW_ZONE1–3` (memcpy-inherited).

**The visibility gate** — `FUN_004d62d0` arms the camera with zones `{count=2, 0, state}`
(`zone_set` +0x1e0, packed list +0x1e4); the walk (`FUN_004d66c0` HW / `FUN_004d6420` SW) loads
it into `DAT_00a070a8`; `FUN_0056c430(node_zone_id)` draws a node iff `zone_id` is −1, the list
is empty/wildcarded, or `zone_id` ∈ list. A geometric point-in-zone fallback (`FUN_004c7630`)
runs only when no explicit state was armed — the remake never needs it.

**The zone pairs** (extracted weather files, checked 2026-08-09) — what switching changes:

| chapter | ZONE1 `FOG_RANGES`/clip far | ZONE2 | Δ below deck today |
|---|---|---|---|
| C1 | 1000–1750 / 2050 | 1000–4000 / 4500 | fog wall 2.3× too far |
| C1C | 1000–1750 / 2050 | 1000–4000 / 4300 | same |
| C2B | 1000–1700 / 2050 | 1000–4000 / 4300 | same |
| C4 | 500–4500 / 4800 | 1000–4500 / 4800 | haze onset 500 m late |

`FOG_COLOR` and `SUNLIGHT_*` are identical within each pair in C1 (spot-checked); B11 verifies
the rest before touching lighting. Non-deck chapters author their band out of reach (C1B/C3
10000–11000, C2 19024–20124, C5 9950–10150) — state 2 never fires there.

**The deck** (C1 gamez, `extracted/C1/gamez/nodes.json` + `models.json`):
144 flat tiles at y=960, all `zone_id 2`; `cloudparent` facades `zone_id 2`; `fvol*` `zone_id 2`;
mission targets `zone_id 1`. `horizon` container `zone_id −1`; `horizon/zone1` children
`h_zone1scroll` (model 768: 12-sided, Y-levels −2000 / 270.7 / **396.4 cap centre** / 789 /
1835 / 2519.4 / 2792.8) and `o28` (skirt, −2000…270.7, bottom cap −864.7); `horizon/zone2`
children incl. `h_zone2scroll` (0…1646.4), moon, stars, skirt `g1155`. The cap-centre 396.4 =
A7's measured "ceiling ~400 m above the camera", which pins the original's horizon anchor at
metric scale ~1.0 vertically.

**Fog volumes** (`FUN_0044e010` loader, `FUN_0044e6f0` evaluator): `fog_zone` bool; approach
ramp over `fog_fade_dist` (default 400) before the wall; interior decay over
`interior_fog_fade_dist` (default 20) from full at the wall; union `a + b − a·b`; whiteout colour
`fog_color` (default = `CLOUD_COVER TOP_COLOR`); inside-any-volume ⇒ state 3. Loader defaults
equal the "vestigial" C1B/C2/C3 file values byte-for-byte (C2's checked). Load bracketed by
`srand(0x9b3a9ce2)` … `srand(time())`.

**The whiteout band** (`FUN_0042ee40` + per-object `FUN_005782d0`): colour lerps
BOTTOM_COLOR→TOP_COLOR by camera altitude fraction; opacity 0→1 from BOTTOM to core bottom, 1
through the core, 1→0 to TOP; per-object version pads by object radius. A log2/atan remap
blended by a rand-driven drifting parameter animates the in-cloud flicker (D32).

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

### Wave A — Decode landing & plumbing

1. ☑ Land the decompile decode in the format docs; retire the dead caveats
2. ☑ Camera weather-state plumbing (state 1/2/3 in `WeatherState`, no visual change)

### Wave B — Deck chapters: zones, deck, sky

11. ☑ Per-state fog + clip + sunlight switching (ZONE1 below / ZONE2 above)
12. ☑ `zone_id` visibility gate switched with the camera state
13. ☑ Above-deck floor at the authored tile altitude
14. ☐ Below-deck ceiling = the zone-1 dome (`h_zone1scroll`), scrolling, camera-anchored
15. ☐ Horizon anchor vertical-scale audit

### Wave C — C5 fog volumes

21. ☐ In-volume whiteout (approach/interior ramps, union, `fog_color`)
22. ☐ ZONE3 fog while inside a volume

### Wave D — Residuals & polish

31. ☐ Re-baseline the overcast-match fog instruments under zone-correct fog; deck fog-flag check
32. ☐ In-band turbulence flicker (optional TUNE)

## Dependency and parallelism notes

A1 is docs-only and independent. A2 blocks all of B and C22 (everything reads the state). Within
Wave B: B11 and B12 are independent consumers of A2 but **both edit `WeatherRig.cs` — run them
sequentially, not in parallel worktrees**. B13 and B14 are two halves of the deck-regime rewrite
and land together or in B13→B14 order (both touch `WeatherRig.Tick`/`DeckRegime` and
`WorldBuilder`'s deck handling). B15 reads B14's result. C21 needs only A2's plumbing hooks and
can run parallel to Wave B (different files: `FogVolumes.cs`/`FogVolumeClutter.cs` + a shader
global); C22 needs A2 + C21. D31 runs only after B11 lands (its whole point is re-measuring under
zone-correct fog). D32 is independent, last. Golden-image warning: B11–B14 and C21–C22 will move
pixels in `analysis/goldens` scenes — expect to re-bless goldens once per wave, not per item.

---

# Wave A — Decode landing & plumbing

## A1 ☑ Land the decompile decode in the format docs; retire the dead caveats

**Landed (2026-08-09).** `weather.md`: the "`zone_id` census is not admissible" ⚠ is struck and
replaced with a dated correction citing `FUN_0056c430`/`FUN_0042ee40`/`FUN_004d62d0` — the gate
is per-frame camera state, not a static per-mission partition, so the old "leaves mission content
unbuilt" objection dissolves. The CLOUD_COVER "band's MIDPOINT is load-bearing twice over"
paragraph gets a dated correction: above the deck is the authored world-fixed tiles at their own
altitude (C1/C1C/C2B 960, C4 1050; C4's "exactly on the centre" was its own coincidence), below
the deck is `horizon/zone1`'s camera-anchored, UV-scrolled dome (C1's cap centre 396.4 = A7's
measured "~400 m"). The "C1 zone1 970/1047 identity belongs to a zone C1 never flies" ⚠ is
un-retired as a flown-zone fact — `zone1` *is* flown, below the deck. `gamez.md` gets a short
`zone_id` runtime-semantics entry next to the existing `zone_id == -1` note.
`PROJECT_CONTEXT.md`'s "Current status" now names this plan, Wave A, A1 landed/A2 next.

**Survey results (new — only C1 was measured before this pass):** deck tiles / `cloudparent` /
`fvol*` / mission-content `zone_id` census across C1/C1C/C2B/C4, and the `horizon/zone1`+`zone2`
subtree census across all eight chapters, tabulated in `weather.md`'s new "The deck census"
section. C1's `2`/`2`/`2`/`1` tile/facade/volume/mission-content pattern holds in C1C and C4.
**Two findings that contradict the C1 pattern, recorded rather than smoothed over:**
- **C2B ships zero `cloudparent` nodes and its `fvol*` volumes are `zone_id −1`** (always
  visible), not `2` like the other three deck chapters — its fog volumes are never camera-state
  culled at all. A `B12` gate implementation must not assume every deck chapter's `fvol*`
  population is gated.
- **The "~396 m authored cap centre" is C1-specific and does not transfer.** C1's `h_zone1scroll`
  is a two-piece dome (upward cap + separate `o28` skirt); C1C's and C2B's zone-1 subtrees are
  each a **single** combined mesh (C1C: exactly one node, `g1164` — not the `g1163`–`g1166` range
  this plan's B14 section names; C2B: `g1166`, sharing C1C's identical vertex data) with no
  `h_zone1scroll` node at all and a strongly negative whole-mesh `bbox_mid.y` (skirt-dominated, no
  separate cap pulling it up). C4's sole zone-1 node is even named `h_zone2scroll` — a
  leftover/reused name. B14 will need a per-chapter ceiling-distance measurement, not C1's
  constant.

**Original approach (kept for reference).**

**Goal.** The format docs state the decompiled truth — runtime zone states, the `zone_id` gate,
the two-object deck, `fog_zone` — and every caveat the decompile killed is rewritten as a dated
correction, so no future session re-derives the disproven readings.

**Evidence (confidence: traced).** The 2026-08-09 decompile pass, function addresses in "What the
data actually ships". Already landed on this worktree (commit `f9f9c67`): the zone-state section,
`SW_ZONE`/`VIEWING_RANGE` notes and the whiteout-lerp upgrade in `weather.md`; the `fog_zone`
resolution, engine defaults, seed and evaluator mechanics in `fogvol.md`.

**Approach.** Remaining: (a) `weather.md` — retire the "`zone_id` census is not admissible" ⚠ with
the `FUN_0056c430` decode; correct A7's floor/ceiling model in the CLOUD_COVER section (authored
tile altitude; `h_zone1scroll` cap 396.4); un-retire the "C1 zone1 970/1047 `FOG_ALTITUDE`
identity" as a *flown-zone* fact. (b) `gamez.md` (or `world-structure.md`) — a short `zone_id`
runtime-semantics entry. (c) survey the deck tiles' `zone_id` and the `horizon/zone1` cap heights
across C1C/C2B/C4 (only C1 is measured) and tabulate. (d) `PROJECT_CONTEXT.md` "Current status"
points at this plan.

**Model recommendation.** medium — documentation with a small data survey; the hard thinking is
already in the decode.

**Verify.** `git grep` the retired phrases ("not admissible", "runtime meaning is unknown",
"still unidentified") return only historical/corrected uses; the C1C/C2B/C4 survey numbers are in
the doc; docs build has no dangling anchors (the `fogvol.md` table already links the new section).

**⚠ Traps.** Do not delete the disproven text — this repo's convention is dated corrections with
the original kept struck/quoted (see `weather.md`'s A3/A6 precedent). The A7 *behavioural* decode
stays credited: it measured the right thing and named the right altitudes; only the mechanism is
replaced.

## A2 ☑ Camera weather-state plumbing (state 1/2/3, no visual change)

**Landed (2026-08-09).** `WeatherState.CameraWeatherState(cameraPosition, fogZoneArmed, volumes)`
(`CSVM/src/Flight/Weather.cs`) computes the binary's per-frame camera zone exactly as
`FUN_0042ee40` does: 1 default; 2 when `HasCloudBand` and the camera's altitude is at/above the
new `CloudCoreBottom` property (`CloudBandCentre − CloudThickness/2` — a THIRD spelling next to
`CloudBandCentre`, the deck-regime flip, and `CloudBottom`, the visual floor; never unified, per
Decision 1); 3 when `fogZoneArmed` and the camera is inside any of `volumes` via the existing
`FogVolumeBox.Contains` exact half-space test (not the AABB), and state 3 wins over state 2 on
overlap, matching the binary's assignment order.

`FogVolumeSpec` gets one new member, `FogZoneArmed` (`FogZone != 0` — `FUN_0044e010`'s "stores
`value != 0`"), true only for C5. `WeatherRig` gets `SetFogVolumes(volumes, fogZoneArmed)`, called
from `GameSession` right beside the existing `FogVolumeSpec.VolumesOf`/`Load` call it already made
for the ambient cloud field — no new load path, just handing the same data to a second consumer.
`Tick` calls `CameraWeatherState` once per rig per frame and publishes the result onto a new
`PlayerRig.CameraWeatherState` field (default 1), logged only on a change at debug verbosity. Per
rig, not per session, for the same reason as the deck regime: splitscreen panes can disagree.

Ships dark exactly as scoped: nothing reads `PlayerRig.CameraWeatherState` yet, and `--sky-zone`
is untouched (Decision 5's state-override semantics are B-wave's to add). New tests in
`CSVM.Tests/CameraWeatherStateTests.cs` pin the threshold edges (just-below/at/just-above
`CloudCoreBottom`), a control assertion that a bug reading `CloudBandCentre` or `CloudBottom`
instead would fail even though the edge tests pass, a no-`CLOUD_COVER` mission pinned to state 1
at any altitude, state-3 armed/disarmed/in/out combinations, and the state-3-over-state-2
precedence case — against a new hand-authored fixture (`CSVM.Tests/fixtures/zrdr/weather.json`
+ `fixtures/weather-no-cloud/weather.json`, invented values per the fixtures' own rule, not copied
from an extraction).

**Verify.** `.\RunTests.ps1` full green: 749 unit tests (0 skipped, run with `CSVM_DATA_ROOT` set),
29/29 engine suites, and all 13 goldens hash-identical to the committed manifest — confirming zero
visual change, since nothing consumes the new state yet.

**Original approach (kept for reference).**

**Goal.** `WeatherState` exposes a per-frame `CameraWeatherZone` (1, 2, or 3) computed exactly as
the binary does; `WeatherRig.Tick` publishes it; nothing consumes it yet. Ships dark.

**Evidence (confidence: traced).** `FUN_0042ee40`: state 1 default; 2 iff CLOUD_COVER exists and
camera altitude ≥ `CloudBandCentre − Thickness/2`; 3 iff `fog_zone` and camera inside any `fvol`
volume (the containment test already exists — `FogVolumeBox.Contains`, exact half-space planes).

**Approach.** Add the state computation to `Weather.cs` beside `CloudBandCentre` (one spelling for
the core-bottom threshold, shared with B11); thread camera position from `WeatherRig.Tick`; reuse
`FogVolumes`' volume set for the state-3 test (only armed when the chapter's `fog_zone` is set —
today that is C5 only). Extend `--sky-zone` parsing per Decision 5 but keep behaviour unchanged
this item. Unit tests: threshold edges (at, just-below, just-above core bottom), no-band chapters
pinned to state 1, C5 in/out of a street volume.

**Model recommendation.** medium — mechanical against a written spec; the spec is the decompile.

**Verify.** `CSVM.Tests` green including the new state tests; 8-chapter freecam regression
byte-identical (`--det` pixmd5 at one pinned pose per chapter) since nothing consumes the state.

**⚠ Traps.** The state-2 threshold is centre − THICKNESS/2, **not** the band centre and not
`BOTTOM` — three nearby altitudes that all sit within 80 m of each other in C1. Name the constant
for what it is (core bottom). The fvol test must use the *authored shape* containment, not the
AABB (A2 of overcast-match already corrected that once).

# Wave B — Deck chapters: zones, deck, sky

## B11 ☑ Per-state fog + clip + sunlight switching (ZONE1 below / ZONE2 above)

**Landed (2026-08-09).** `WeatherRig.Tick` hands rig 0's `PlayerRig.CameraWeatherState` to a new
nested `FogStateTrigger`, which resolves `WeatherState.ZoneForState(state)` (state *n* → `zone<n>`
through `ResolveZone`'s file fallback) and answers non-null **only on a state edge**; where that
edge also changes the live zone, the extracted `ApplyFogGlobals` rewrites `csky_fog_color` /
`csky_fog_range` / `csky_fog_alt` / `csky_world_light` for the new zone, through the same
sRGB→linear path `Build` always used. `SetupWeather` splits into that writer plus
`SetupWhiteoutAndPrecip`, so a zone change never rebuilds an overlay. The DOME is untouched:
`_activeZone` stays the one zone `Build` resolved (`PreferPopulatedHorizonZone` still owns it), so
below the deck a deck chapter now flies ZONE1's fog under ZONE2's sky — `B14`'s to reconcile.
**Far clip stays the remake's own** (fog hides distance): recorded as a kept divergence in
`architecture.md` and `weather.md`, and `C22` inherits it for ZONE3's 300 m.

**⚠ The `SUNLIGHT_*` survey (step 1) DISPROVES the plan's own "identical within each pair"
assumption, so this item is fog + world light, not fog alone.** All 24 missions of C1/C1C/C2B/C4
diffed key-by-key (script in the session scratchpad; regenerate by walking
`extracted/{C1,C1C,C2B,C4}/*/zrdr/weather.zrd.json`): **6 differ**, 18 are identical.

| mission | what differs between `ZONE1` and `ZONE2` | `WorldLight` (A + D·0.46) |
|---|---|---|
| C1 M02 | ambient .20/.25, diffuse 1.5/1.2, bicolored 1/0, both colour triples | 0.89 → 0.80 |
| C1 M05 | same shape as M02 | 0.89 → 0.80 |
| C1C M01 | ambient .60/.30, `COLOR_AMBIENT` (.5,.5,1)/(.3,.3,1) | 1.00 → 0.76 |
| C1C MP1 | diffuse 2.0/0.4, `COLOR_AMBIENT` (.5,.5,1)/(1,1,1) | 1.00 → 0.68 |
| C1C MP3 | same as MP1 | 1.00 → 0.68 |
| C2B M04 | ambient .20/.60, diffuse 2.0/0.4 | 1.00 → 0.78 |

`WorldLightFactor` therefore rides the state — it is read off `ZoneFog.WorldLight`, so routing it
was free once the fog zone moved. **No re-tuning:** `SunIncidence`/`MinWorldLight` are untouched
(the ⚠ trap), and every mission the calibration was measured on (C1/IA1, C1C/IA1, C2B/IA1, all of
C4) is in the identical 18, so `CAP-11`'s numbers still stand as taken. A below-deck brightness
residual remains `D31`'s to measure.

**Verify — goldens.** `.\RunTests.ps1`: 756 units, 29/29 engine suites, **6 goldens moved and 7 did
not**, and the moved set is exactly the predicted footprint (GOLD-5). *Not re-blessed — the
orchestrator re-blesses once per wave.*

| moved | old → new |
|---|---|
| `c1-waterfall` | `c81a000a0b39ddf6cb7efa92a8ff7c72` → `14259e6e51b27c8fbc119a01a8ab9f11` |
| `c1c-rain` | `ab24ff1615815683fa848ae20d470e9d` → `609dc242ad7e62919ce1e18926837d21` |
| `c2b-rain` | `a092c1cace4681603b41c575e9bd3767` → `1a511f632ebbd6b82e4c8911dd05ea3f` |
| `c4-snow` | `00e3ffb482d7e6ed66287c351fdea97a` → `acca488c414077645f3cab3e3f13f14d` |
| `c1-flight` | `9a5369f6febd59369c9218f8cae50066` → `bdab1b39d4bffb007cb04a33f2651278` |
| `c1-destroy-effects` | `dfcad9ed412ffec851e7968a1e6d638b` → `6e584eee1c7cfe1684b4bcf90d179b97` |

Every mover is a deck chapter below its band (C1 y 60/250/flight, C1C 700, C2B 200, C4 958 — all
under their own `CloudCoreBottom`), and **no deckless chapter moved at all**: `c1b-night-sea`,
`c2-city`, `c3-island`, `c5-city-night`, plus `viewer-bhawk` and `empty-stage`. `c1-crash` is the
one deck-chapter golden that did NOT move, and it is inert by construction (DIAG-10): its camera
looks steeply down at the crash site with the whole frame inside 1000 m, so the ramp is 0 under
both zones, no horizon or dome is in shot, and C1/IA1's world light is identical between the pair.

**Verify — the fog wall at the C1 river pose** (`.scratch/b11/`, instrument `fogwall.py`,
montage `AB-river-fogwall.png`). `--freecam --chapter=C1 --det --mute --pos=-7325,192,-3829
--direction=-0.997,-0.1,0.070` (`y 192`, the corrected overlay altitude), before =
`--sky-zone=zone2` (which IS the pre-item behaviour, Decision 5), after = default. Both logs quote
the zone they rendered (METHOD-15/METHOD-6): before `weather [zone2] … 1000–4000`, after the same
build line plus `camera state 1 -> fog zone 'zone1' — fog 1000-1750 m, altitude 970-1047 m`.

The measurement avoids SHOT-23's pitch/horizon half entirely by never using scene geometry: a
third `--no-fog` render at the same pose gives every pixel's unfogged colour, both fogged frames
are **linearised** (the shader lerps ALBEDO in linear space — reading φ off the sRGB bytes instead
returns a systematically low 3.06 where the truth is 4.00), and φ is the least-squares projection
onto the fog colour. Two predictions, both able to fail:

- φ_after / φ_before = 3000/750 = **4.000** wherever neither ramp clamps. Measured, binned on the
  before frame's own distance ruler `d = 1000 + 3000·φ_before`: **4.004** (1150–1300 m), **3.962**
  (1300–1450), **3.970** (1450–1600), then falling away — 3.781, **2.825**, 2.807 — from the
  1600–1750 bin on, which is the after ramp clamping.
- Inverting the slope, `far_after = 1000 + 3000/3.989` = **1752 m** against ZONE1's authored
  **1750** (the before frame's own far is 4000 by construction of the ruler). The 2.3× error the
  plan quantified is closed to 0.1 %.

Qualitatively against `OriginalScreenshots/C1 IA1 Fog river.png`: the far ridge that stands clearly
at ~3.5–4 km in the before frame is gone in the after frame, and the original shows no such ridge —
terrain dissolves just past the mid-ground there too.

**Verify — the invariants** (METHOD-12), all pixel-exact on this build:

| check | pose | result |
|---|---|---|
| above-deck unchanged | C1 `-7323,1192,-3829` dir `0,0,-1` (the literal pinned above-deck pose, `PLAN-overcast-match` B12/A4) | state-driven vs `--sky-zone=zone2` **pixmd5 identical**, 0 px differ |
| able-to-fail control | same pose, `--sky-zone=zone1` | **489,117 px (53 % of frame) differ** — the identity above is a real pass, not a dead check (METHOD-9/10) |
| C2 unchanged | `-5722,186,-3457` dir `-0.438,-0.15,-0.899` (the `c2-city` golden's pose) | state-driven vs `--sky-zone=zone1` **pixmd5 identical**; the log carries **no** `camera state -> fog zone` line at all, so not one global was rewritten — identical *by construction*, corroborated by the unmoved golden |

**Non-deck chapters cost nothing** (step 4), and that is structural rather than measured: their
`CLOUD_COVER` is authored out of reach (C1B/C3 10000–11000, C2 19024–20124, C5 9950–10150), so the
state never leaves 1; `ZoneForState(1)` = `zone1`, which is exactly what each of their static
resolutions already produced (C1B/C2/C3 via `PreferPopulatedHorizonZone`'s bare-marker correction,
C5 via the file fallback), so the trigger — seeded with `Build`'s own `_activeZone` — finds nothing
to change and never writes. Their four goldens confirm it.

**Tests** (`CSVM.Tests/FogZoneStateTests.cs`, 7 new): the state→zone mapping on a deck chapter
(C1/IA1 1→zone1 far 1750, 2→zone2 far 4000) and on C5 (3→zone3, 2→zone1 by fallback); the
one-`ZONE1` fixture answering `zone1` for states 1/2/3 — the case that would otherwise fall to
`NoFog`, fullbright; the trigger applying once and staying silent for 60 further frames at the same
state; a state change that resolves to the live zone writing nothing while still reporting the
fallback exactly once; `--sky-zone`'s `stateDriven: false` keeping every state silent; and the
`SUNLIGHT_*` finding pinned from the extraction (C1/IA1 equal, C1/M02 not).

**Original approach (kept for reference).**

**Goal.** In the deck chapters, below-deck flight wears ZONE1's fog ranges/altitude/colour (C1:
far 1750, not 4000) and above-deck flight wears ZONE2's, switched invisibly inside the whiteout
core. Non-deck chapters are pixel-unchanged.

**Evidence (confidence: traced).** `FUN_00472ea0` applies the zone block on state change; the
zone-pair table in "What the data actually ships" quantifies the below-deck delta. C1's pair has
identical `FOG_COLOR`/`SUNLIGHT_*`; the other three pairs must be diffed before deciding whether
sunlight switching is a no-op in practice (if identical everywhere, `WorldLight` never changes
and the item is fog+clip only).

**Approach.** `WeatherRig` resolves the *zone for the current state* (state→`ZONE<n>` key, then
`ResolveZone`'s existing fallback) and re-applies the shader fog globals when the state flips.
`PreferPopulatedHorizonZone` keeps owning the *dome* choice per B14 — fog zone and dome zone are
allowed to differ only through that rule's documented cases. Diff all four chapters' pair
`SUNLIGHT_*`; if any differ, route `WorldLightFactor` through the state too. Far-clip stays the
remake's own (fog hides distance, per `weather.md`'s CLIP_RANGES note) — record that as a kept
divergence.

**Model recommendation.** high — touches the calibrated fog chain (`csky_atmosphere.gdshaderinc`
globals) that three plans of measurement sit on; blast radius is every deck chapter's look.

**Verify.** Below-deck A/B at the C1 river pose (`y 192` per the corrected overlay reading — not
934): fog wall must close in to ~1750 m; compare against `Screenshots/C1 IA1 Fog river.png`'s
measured plateau. Above-deck pinned poses from B12 of overcast-match must be unchanged (they were
shot in state 2, which keeps zone2). C2 (no reachable band) byte-identical. Full 8-chapter
regression; re-bless goldens once with the wave.

**⚠ Traps.** Do NOT re-tune `k`/`WorldLight` in this item even if below-deck brightness now reads
differently — lighting calibration (`CAP-11`) was measured per-zone-correctly only where the pair
is identical; a brightness residual here is D31's to measure, not B11's to absorb. The switch
must be state-edge-triggered, not recomputed-per-frame-from-scratch, or the C26 rim-annulus fog
saturation (which reads the fog globals) will flicker at the boundary.

## B12 ☑ `zone_id` visibility gate switched with the camera state

**Landed (2026-08-09).** `GameZNode.ZoneId` reads the field; `Mech3/ZoneGate.cs` holds the rule
(`Draws(zoneId, state)` — `−1`/`0` always, otherwise `zoneId == state`) and its three shared visual
layers (bits 13–15, immediately below `SplitScreen`'s per-player band, which every cull mask the
engine builds starts with fully open). `SceneBuilder.BuildSubtree(…, zoneGate: true)` stamps each
built mesh with **its own node's** `zone_id` layer — never inherited down the subtree, because the
data disagrees with that reading (C1/C2/C3/C4 each ship `flaglite1`/`flaglite2` as `zone_id −1`
children of a `zone_id 1` parent, and `FUN_0056c430` is called per node as the walk descends);
`WeatherRig.Tick` then narrows each rig camera's mask to that camera's own state.

**Visibility now has exactly one owner, and it is a CULL MASK, not `Node3D.Visible`.** That is a
requirement, not a style: splitscreen panes sit in different states at the same instant, and the
`Visible` flag on this very content is already read and written by `AnimRuntime`'s `NodeActive`
condition and its uncovered-`destroyed` sweep, by `WorldBuilder.HideUnplacedEntities`/
`RestorePlacedEntities`, and by `DamageVisuals` — a visibility-based gate would have answered all
of their questions with "the camera is above the deck". Nothing in the objective/AI path reads
render visibility, so the trap the item warned about is closed by construction rather than by
audit. The two exceptions are the per-rig camera-anchored copies (deck, dome), which take
`Draws` against `Node3D.Visible` on the rig's OWN copy because a per-player visual layer and a
zone layer cannot share one instance (a cull mask ORs its bits). `WeatherRig.DeckRegime` lost its
`CloudsVisible` member outright — A7's altitude gate over the two ambient cloud populations was
this gate's `zone_id 2` special case — and kept its placement/lit-variant halves untouched for
B13/B14.

**Per-chapter, from the data, never from a chapter name.** `WorldBuilder.FogVolumeZoneIdOf` gives
the `fvol` sprite field the zone its own volumes author, and refuses to guess (`−1`) if they ever
disagree: C1/C1C/C4 `2`, C5 `1`, **C2B `−1`**. `WorldBuilder.CloudDeckZoneId` does the same for the
deck tiles (144/144 on `zone_id 2` in all four deck chapters). `HorizonZone` gained `ZoneId`.

**Build census** (`zone gate: … mesh instance(s) on zone 1 / 2 / 3`, printed per world build so
that "the gate stopped stamping" cannot read like "this chapter authors no zoned content"):
C1 **1343 / 626 / 0** (deck `2`, fvol `2`), C5 **958 / 0 / 134** (deck `−1`, fvol `1`).

**⚠ One scoped deferral, and it is deliberate: the DOME is not gated yet.** The deck IS
(`zone_id 2` → culled below the deck), which is what the item asked for and what the probes below
measure. But `BuildHorizon` still builds only the flown zone's dome, so gating it would leave a
below-deck camera with *no sky at all* — measured: the first cut of this item rendered the C1 river
pose against Godot's clear-colour gradient. The original always has a dome for the state it is in
(below the deck it draws `horizon/zone1`, which B14 will build). So `WeatherRig` gates the dome
only once the world holds more than one (`_builtDomeZones <= 1`); the `_domeZoneId` field, the
census that feeds it and the `Draws` call are all live, and **B14 turns the swap on by building the
second dome, with no new rule to write**.

**Verify — probes** (`.scratch/b12/`, all `--freecam --det --mute --no-fog`, markers
`--tex-override=cloud1.tif=00ff00 --tex-override=cloud2.tif=00ff00
--tex-override=cloudlayer.tif=ff0000`; `--no-zone-cull` is the baseline side of every pair, which
is exact rather than approximate — it restores the pre-item picture pixel-for-pixel).

⚠ The first run also overrode `Sky1.tif` and had to be thrown away: `Sky1.tif` is C4's DECK texture
*and* C1/C1C/C2B's zone2 SKYDOME wall, so it counted the (ungated) dome as deck and reported
121,157 "deck" px in a frame with no deck in it (SHOT-13 — one texture, one question).

| # | pose | gate ON | `--no-zone-cull` |
|---|---|---|---|
| a | C1 below deck `-7325,192,-3829` | **green 0, red 0** | green 0, **red 363,480** (39.4 % of frame) |
| b | C1 above deck `-7323,1192,-3829` dir `0,0,-1` | green 414,691, red 10,593 | green 414,691, red 10,593 — **0 px differ** |
| c | C2B below deck `-7325,192,-3829` | **green 123,989** (13.5 %), red 0 | **green 0**, red 366,458 |
| d | (d is the right-hand column of every row) | — | — |

- **(a)** is the item's own prediction met: below the deck the tiles, the 626 `cloudparent`
  facades and the 22k `fvol` sprites are all `zone_id 2` and all gone. Green reads 0 on the
  ungated side too, and that is a fact about the POSE, not a dead check — the opaque deck ceiling
  at cam+135 m occludes the sprite field from underneath (it is the same sheet seen from the other
  side). Red is the number that moves, and it moves to zero.
- **(b)** is the invariant (METHOD-12): above the deck the state is 2, zone 2 draws, and the gate
  is pixel-exact against the ungated build. The zone-1 ground world IS culled there — it is simply
  behind the deck at this pose, which is precisely why the original can afford to cull it.
- **(c) is the C2B exception rendered.** Its nine `fvol` volumes are `zone_id −1`, so the field is
  never culled; the gate makes it *more* visible, because the zone-2 deck that was occluding it is
  gone. An implementation that assumed "deck chapter ⇒ fvol zone 2" would have reported 0 here and
  passed every other check.
- **Mission content**, at the `c1-destroy-effects` golden's own pose (`-6350,250,-4144` dir
  `1,-0.1,0`, which frames `ap_radiotwr` — `zone_id 1`): `--sky-zone=zone1` (state 1) shows the
  airfield, its runways, hangars and the radio tower; `--sky-zone=zone2` (state 2, same fog, same
  dome, gate the only difference) removes **529,642 px / 57.5 %** of the frame — the entire ground
  world, since C1 authors its terrain `zone_id 1` too. That is the original's own above-deck cull,
  and it is invisible in play only because the overcast is opaque.
- **C5's `zone_id 3` city** (134 mesh instances) is culled at state 1, as the armed set `{0, 1}`
  requires. Unfogged A/B: **28.3 %** of frame at `-2000,120,-2000`, 1.90 % from `-5120,900,-5120`,
  0.23 % at the `c5-city-night` golden's pose — where C5 `zone1`'s own 1500–2250 m fog then takes
  it to **7 px**, which is why that golden did not move (DIAG-17: reached but invisible, not never
  reached).

**Verify — tests.** `.\RunTests.ps1`: **774 units** (0 skipped, `CSVM_DATA_ROOT` set), **29/29**
engine suites, engine errors clean. New `CSVM.Tests/ZoneGateTests.cs` (12 cases): the rule at every
(zone, state) pair; one layer per zone and none for `−1`/`0`/out-of-range; the cull mask narrowing
the band while leaving every other bit — including the per-player band — untouched, idempotently
and reversibly; `OpenCullMask` undoing any narrowing; `--sky-zone` name → forced state, with junk
names leaving the state machine's answer standing; the per-chapter `fvol` + horizon `zone_id`
census from the real extraction (the `SkyZoneTests` precedent); C2B's `−1` pinned on its own with
C1's `2` beside it as the able-to-fail half; and the two `flaglite` nodes pinned as the data's own
counter-example to a subtree-inherited gate. `DeckRegimeTests` lost its four `CloudsVisible`
assertions to that file and kept the rest.

**Verify — goldens. 7 moved, 6 did not, and all 7 are B12's own** (GOLD-8 — the committed manifest
still holds pre-B11 hashes, so each mover was re-attributed by A/B against a `--no-zone-cull`
render of its own args at its own `--frames`): `c1-flight` **50.00 %** of pixels, `c4-snow`
**27.91 %**, `c1-crash` **0.26 %** (2,398 px — the crash camera looks steeply down, so only a
sliver of the below-deck ceiling is in shot, which is why B11's fog change left this shot inert and
a geometry change does not), plus `c1-waterfall`, `c1c-rain`, `c2b-rain`, `c1-destroy-effects`.
**Every mover is a deck chapter below its band and every non-deck chapter is byte-identical** —
`c1b-night-sea`, `c2-city`, `c3-island`, `c5-city-night`, `viewer-bhawk`, `empty-stage` (GOLD-5).
The mechanism in each is the same: the deck's `zone_id 2` ceiling is gone below the deck until B14
puts `horizon/zone1`'s cap there. *Not re-blessed — the orchestrator re-blesses once per wave.*

**Original approach (kept for reference).**

**Goal.** Nodes carrying a gamez `zone_id` draw only when that id is in {0, current state} (or
−1), matching `FUN_0056c430` — deck tiles, `cloudparent`, the fvol sprite field and the dome
subtrees swap behind the whiteout, and zone-1 mission content hides above the deck, exactly as
the original culls it.

**Evidence (confidence: traced).** `FUN_0056c430` + the C1 census: tiles/`cloudparent`/`fvol*`
`zone_id 2`, mission targets `zone_id 1`, `horizon` −1. The remake already hides/shows both
cloud populations per camera regime (`WeatherRig.DeckRegime`) — that hand-rolled rule is this
gate's special case and gets subsumed.

**Approach.** `WorldBuilder` records `zone_id` per built node group (it already reads nodes.json);
`GameSession` (or the rig) toggles group visibility on state change. Per Decision 4: full gate,
`--no-zone-cull` escape hatch. The fvol *scatter* field inherits the zone of its volumes (2).
Sweep all chapters' `zone_id` census first — C5's volumes carry ids that its `fog_zone`
mechanism ignores at the doc's last reading; the gate must reproduce the original, not an ideal.

**Model recommendation.** high — correctness judgement across every chapter's content census; a
wrong gate silently deletes mission content.

**Verify.** C1 below deck: tiles/facades/sprites absent, targets present; above deck: inverse.
The A7-era probe pair (`.scratch/a6/` tex-override poses) re-shot: green sprite pixels below
deck drop to 0 without the deck mesh trick. Freecam node counts change *by exactly the gated
groups* — take the before-census, assert the delta, per `docs/verification.md`'s "an unchanged
number is not evidence" rule inverted.

**⚠ Traps.** `--sky-zone`/state override must drive this gate too or inspection poses lie.
Danger: `zone_id 1` holds *mission* content — verify objectives/AI logic in the remake never
depended on those nodes being visible (visibility, not existence, is what the gate touches).
Do not gate nodes with `zone_id −1`/absent.

## B13 ☑ Above-deck floor at the authored tile altitude

**Landed (2026-08-09).** `WeatherRig.DeckRegime(cameraY, bandCentre, authoredY)` gained a third
parameter: the above-band branch now returns `authoredY` instead of `bandCentre`. `authoredY` is
`_deckAltitude`, set by the new `WeatherRig.SetDeckAltitude(float)` — called from `GameSession`
beside the existing `SetDeckZoneId(builder.CloudDeckZoneId)` call — from the new
`WorldBuilder.CloudDeckAltitude` property, which exposes `_deckAltitude`: the SAME coverage-
winning altitude bucket `FindCloudDeck` already computes to classify the 144 tiles (C1/C1C/C2B
960, C4 1050), read off the built data rather than hardcoded, per the item's own trap. The
below-band branch (`camera.y + DeckCeilingHeight`) is untouched — still the A7/B12 reconstruction,
B14's to replace with the zone-1 dome.

The C26 rim annulus needed no code change to stay attached: `AddDeckAnnulus` already builds it as
a CHILD of the `deck` node in the tiles' own local coordinates, so it follows whatever world
position `Tick` gives `deck.Position` — the same mechanism that already kept it centred through
`GameSession.AssignCloudDeckIfBuilt`'s AABB re-measurement.

A second static entry point, `WorldBuilder.CloudDeckAltitudeOf(gamez, worldName)`, computes the
identical altitude as a pure function of the raw `GameZ` data — no scene build, no
`TextureArchive` — so `DeckRegimeTests` can pin a chapter's authored altitude against the
extraction without paying for a full world build. Both entry points share one tile test,
`FlatTileOf` (extracted from the former instance-only `FlatTile`, which now delegates to it).

**Verify — tests.** `.\RunTests.ps1`: **779 units** (0 skipped, `CSVM_DATA_ROOT` set), 29/29
engine suites, engine errors clean. `DeckRegimeTests` updated: the above-band fact now asserts
`C1AuthoredDeckY` (960), not `C1Centre` (1047); a new fact pins C4's coincidence (authored 1050 =
its own centre, so C4 is unmoved by this item by construction); a new `[ExtractedDataTheory]`
(`TheDeckMeshStaysAboutTenMetresBelowTheFvolSlabFloor`, 4 cases) reads each deck chapter's real
`gamez` data and asserts `WorldBuilder.CloudDeckAltitudeOf` against the fvol slab floor it sits
under (C1 960/970.00, C1C 960/970.73, C2B 960/970.00, C4 1050/1060.00 — A6's table, restated from
the extraction rather than hand-typed) — the A6 trap's "mesh 10 m under the slab floor"
relationship, unaffected by this item since only the WORLD placement changed, not the mesh's own
authored Y.

**Verify — goldens.** 7 moved, all pre-existing B11/B12 movers, **zero further movement from
B13**: an A/B against the pre-B13 build (source files temporarily reverted, rebuilt, goldens
stage re-run) produced BYTE-IDENTICAL hashes to the B13 build on all 13 shots, including
`c4-snow` (`4312fd169f12525d23b20b7bb9b8caa2` both times) and every other mover. No golden in
`analysis/goldens/manifest.json` holds an above-deck pose (all seven movers' `y` sits below each
chapter's own `CloudCoreBottom`), so the whole set was expected to be untouched by an
above-band-only change — confirmed by A/B, not merely asserted. *Not re-blessed — the
orchestrator re-blesses once per wave.*

**Verify — probe.** `.scratch/b13/c1-above-deck.{log,png}` (`--freecam --chapter=C1
--pos=-7323,1192,-3829 --direction=0,0,-1 --det --mute --log=world:debug --frames=3`): a new
debug-only log line (`WeatherRig.Tick`, `deck: player 0 camera y=1192.0 -> deck y=960.0 (at/above
band, authored floor)`) confirms the applied deck Y is 960 at this pose, against the pre-B13
1047 (the band centre) — the 87 m drop the plan predicted.

**Original approach (kept for reference).**

**Goal.** Above the deck, the deck floor renders at the tiles' authored altitude (960 / 1050) —
world-fixed, never re-pinned to the band centre.

**Evidence (confidence: traced).** Tiles are ordinary world meshes; nothing in the decompile
moves them; A7's centre-pin was C4's coincidence (Decisions 2, disproof 4).

**Approach.** In the above-deck regime, `WeatherRig.Tick` stops repositioning `CloudDeck` and
leaves it where `WorldBuilder` placed it (authored y). The below-deck branch is B14's. Keep the
C26 rim annulus attached to the floor at its new altitude.

**Model recommendation.** medium — small change to one regime branch, but sits inside the
measured deck machinery; medium with care beats low.

**Verify.** C4 unchanged (960→1050 chapters: pixel-identical by construction — its authored =
centre). C1/C1C above-deck pinned poses: floor drops 87 m; A/B against `CAP-12`/`CAP-11`
above-deck stills for rim height and deck-top altitude reading. `DeckRegimeTests` updated to pin
authored altitude.

**⚠ Traps.** The fvol sprite field is top-anchored *relative to the volumes*, not the mesh — its
altitude does not move with this change; only the mesh does. Expect the mesh/sprite relationship
to return to the authored "mesh 10 m under the slab floor" (A6's table) — that relationship is
the check.

## B14 ☐ Below-deck ceiling = the zone-1 dome, scrolling, camera-anchored

**Goal.** Below the deck, the ceiling is the chapter's `horizon/zone1` geometry — textured,
skirted, UV-scrolling at the authored 0.07/s, riding the camera with its cap ~396 m overhead —
replacing the relocated-deck-mesh reconstruction.

**Evidence (confidence: direction sound; geometry traced, integration TUNE).** C1's models
768/769 (Y-levels in the survey); `tex_fx.gw`'s `h_zone1scroll` scroll; disproofs 5/6. The
remake currently builds only the flown zone's dome (`BuildHorizon`), so zone-1 domes for the
deck chapters are unbuilt today.

**Approach.** `BuildHorizon` builds *both* zone subtrees for deck chapters; the B12 gate picks
which is visible. Apply the authored UV scroll to `h_zone*scroll` materials (both zones — zone2's
scrolls too, per C1's `h_zone2scroll`). The dome-vs-fog interaction below deck: the wall/skirt
pattern (`FOG_COLOR` base ring, B18/C26 findings) must be re-checked against *zone1's* dome
authoring, not assumed from zone2's.

**Model recommendation.** high — new render path against calibrated dome findings; the C26 rim
geometry interacts.

**Verify.** Below-deck C1: ceiling texture present overhead at ~cam+396 (measure via the A7
altimetry method in reverse), scroll visible in a 10 s capture; above-deck frames untouched.
A/B: `C1 IA1 Fog river.png` (below deck) — the 27-px band / rim measurements re-run; the "12.6 km
ceiling texture" observation (B15 of overcast-match) re-read against the *dome* ceiling, which is
camera-anchored and therefore never fogs out — this may resolve that anomaly outright.

**⚠ Traps.** The cap centre (396.4) is *dome-local*; the anchor must not apply `HorizonScale` to
it vertically (B15 owns the audit — coordinate). `o28`'s −2000 skirt overlaps the terrain; check
depth/draw-order against the original's below-deck horizon before shipping. C1C's zone1 children
are `g1163`–`g1166` (no `h_zone1scroll`) — per-chapter geometry, no shared assumptions; C1C has
no scroll statement, so no scroll there.

## B15 ☐ Horizon anchor vertical-scale audit

**Goal.** A written, verified statement of how the remake's `HorizonScale`/`HorizonScaleFor`
interacts with metric dome features (the 396 m cap, the skirt depth), and a fix if the zone-1
ceiling lands at the wrong altitude under scaling.

**Evidence (confidence: direction sound).** 396.4 authored ≈ 400 measured ⇒ the original anchors
the dome at metric scale ~1.0 vertically. Uniform scale about the camera preserves *angles* (the
wall/skirt look is scale-invariant) but not *metric altitude* — a 2.5× scale would put the cap at
~990 m over the camera, contradicted by A7's measurement.

**Approach.** Decide per geometry class: angular features (wall gradient, skirt) may keep the
fitted scale; the flyable-through ceiling cap must sit at authored metric height. Options: don't
scale zone-1 domes; or scale XZ only; or anchor the cap band separately. Measure, choose, record
in `architecture.md`.

**Model recommendation.** high, low effort — one judgement call over a small measurement set.

**Verify.** Fly up through the C1 ceiling at the controls: whiteout entry (`BOTTOM` 970) must
precede reaching the visual ceiling exactly as the original footage shows; C1B's 21.8 km dome
(the reason `HorizonScaleFor` exists) still fits the far plane.

**⚠ Traps.** Do not "fix" the wall's authored gradient while in here (C26's ⚠ stands). C1B is the
scale-clamp regression canary.

# Wave C — C5 fog volumes

## C21 ☐ In-volume whiteout (approach/interior ramps, union, `fog_color`)

**Goal.** Flying at C5's street haze produces the original's whiteout: opacity ramps up over the
last `fog_fade_dist` metres of approach, peaks at the wall, decays over `interior_fog_fade_dist`
inside; multiple volumes union as `a+b−a·b`; colour is fogvol's `fog_color`.

**Evidence (confidence: traced).** `FUN_0044e6f0` (mechanics + defaults in the survey);
`FogVolumeSpec` already parses all four keys and the volume shapes are exact convex hulls.

**Approach.** Per-frame in `WeatherRig.Tick` when the chapter's `fog_zone` is set: signed
distance to each volume (reuse the half-space planes; exterior distance needs a new
closest-point-on-hull helper), density per the decompiled ramps, blend into the same screen
whiteout channel the CLOUD_COVER band uses (Decision 6). Armed only by `fog_zone` — C1's
volumes (fog_zone 0) must not whiteout.

**Model recommendation.** high — new geometric code (distance-to-hull) plus a look-critical
blend; the ramps are traced but the compositing order against the band whiteout is a judgement.

**Verify.** New unit tests: ramp values at wall/±fade distances, union of two volumes, C1
disarmed. At the controls: C5 low pass into a street strip — whiteout in, 50 m later mostly
clear inside haze; A/B against any C5 night footage owed (check `playtest.md` for an owed CAP
first — if none exists, mint one rather than tuning blind).

**⚠ Traps.** The interior ramp *decays* going in — full at the wall, ~0 at 20 m deep. That reads
backwards until you hold it next to C22: the volume is a transition curtain; ZONE3's own 50–250 m
fog is what carries the interior look. Implementing C21 without C22 will look wrong and must not
be "fixed" by inverting the ramp.

## C22 ☐ ZONE3 fog while inside a volume

**Goal.** While the camera is inside a C5 volume (state 3), the scene wears ZONE3's fog
(50–250 m, `[16,16,16]`) and sunlight; leaving restores state 1's ZONE1.

**Evidence (confidence: traced).** State 3 in `FUN_0042ee40`; `FUN_00472ea0` applies ZONE3 like
any zone; C5 is the only ZONE3 author and the only `fog_zone 1` chapter — the data cross-check
that settled `fog_zone`.

**Approach.** Pure consumer of A2's state + B11's per-state application path — ZONE3 is just a
third key. The C21 whiteout covers the transition frames, so no extra smoothing is authored
(matching the original: the switch is hard, hidden by the curtain).

**Model recommendation.** medium — B11's machinery with one more state; the risk landed in B11.

**Verify.** C5 street pass capture: visibility collapses to ~250 m inside, restores on exit;
`WeatherState` tests pin the state-3 zone resolution (`ZONE3` present in all 8 C5 missions —
assert from the files, not one).

**⚠ Traps.** ZONE3's `CLIP_RANGES` far is 300 — per B11's kept divergence the remake does not
clip, it fogs; carrying that rule here is deliberate, not an oversight. Do not enable state 3
outside `fog_zone` chapters even where volumes exist (C1).

# Wave D — Residuals & polish

## D31 ☐ Re-baseline the overcast-match fog instruments; deck fog-flag check

**Goal.** Every fog-strength residual recorded in `PLAN-overcast-match` (B15's split verdicts,
C26's rim numbers) is re-measured under zone-correct fog, and each is either closed, re-opened
with new numbers, or handed a backlog item. Plus one specific lead: whether the deck tiles author
`fog: false` (which would explain the original's ceiling texture surviving to ~12.6 km).

**Evidence (confidence: lead-only).** B11 changes the below-deck fog by 2.3×; every below-deck
measurement made before it is suspect. The fog-flag lead: cloud *cards* author `fog: false`
(`fogvol.md`); the tiles' flag is one lookup in the gamez models — but under B14 the below-deck
ceiling is the camera-anchored dome, which may resolve the 12.6 km observation without any flag.

**Approach.** Re-run the recorded poses (river y192, canyon, the C26 rim set) with the
before/after discipline; read the tiles' `fog` flag from `models.json`; write the verdicts into
the plan and `weather.md`.

**Model recommendation.** high — this is instrument work; `docs/verification.md` discipline is
the whole item.

**Verify.** Each residual has a dated verdict with numbers; no TUNE is adjusted in this item.

**⚠ Traps.** INSTR-7: C2 is still the only chapter that exercises the altitude term — do not
promote a C2-only residual to a global fix.

## D32 ☐ In-band turbulence flicker (optional TUNE)

**Goal.** The whiteout opacity inside the band shimmers as the original's does.

**Evidence (confidence: direction sound, constants TUNE).** `FUN_0042ee40`'s log2/atan remap
blended by a rand-driven drift (rate read from a weather-struct field ≈ +0x934). The curve shape
is decompiled; the perceptual match is a TUNE against in-cloud footage.

**Approach.** Small time-varying term on `WeatherState.WhiteoutColor`'s opacity while inside the
band, `--det`-stable (seeded from `Rng`, not wall clock). Mark constants TUNE in `backlog.md`.

**Model recommendation.** medium, low effort — contained cosmetic with a written spec.

**Verify.** In-cloud capture vs any original in-band footage (`CAP-12` C4 take has in-cloud
frames); `--det` twice at a pose inside the band → identical pixmd5 at matched frame indices.

**⚠ Traps.** Whiteout is measured by `FlatColorTests`/deck instruments at *static* poses — keep
the flicker zero at t=0 or those pins break.
