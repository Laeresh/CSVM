# weather.json — per-mission atmosphere (validated on this install)

Part of the [format documentation](README.md) (see also [zrdr.md](zrdr.md),
[world-structure.md](world-structure.md)). Covers the mission's `weather.json` reader: distance fog, the cloud-cover
whiteout band, wind, and the shared **colour-triple encoding rule**. Consumed by
`CSVM/src/Flight/Weather.cs` (`WeatherState`) + `Session.WeatherRig.Build`.

Seeded 2026-07-18 with the item-3 fog-colour decode; grown the same day with precipitation
(item 5) and the `SUNLIGHT_*` world-lighting decode (item 6, the night/overcast brightness
calibration).

## Location & shape

One `weather.json` per mission folder, in the mission's own zrdr archive
(`extracted/<chapter>/<mission>/zrdr/`), alongside `ia.json`/`objectives.json` (see
[spawns.md](spawns.md)). Some folders are multiplayer-only or lack the file — the loader
returns "no fog" then.

`weather.json` root[0] is one alternating **dict** (`key, [values…]`, see the
[shared conventions](README.md#shared-conventions-zrdr-readers)). Its blocks:

| Block | Read as | Notes |
|---|---|---|
| `VIEWING_RANGE` | dict | not used by the remake |
| `WIND` | **bare-scalar block** | `STATIC_VELOCITY [x,y,z]`, `RANDOM_MAX_SPEED s`, `RANDOM_ACCEL a` |
| `CLOUD_COVER` | **bare-scalar block** | `TOP`, `BOTTOM`, `THICKNESS` (metres) + optional `TOP_COLOR`/`BOTTOM_COLOR` |
| `ZONE<n>` | dict (+ `SW_*` twin) | per-zone fog; the `SW_*` software-renderer twin is ignored. **The names are per chapter — see below** |

**Bare-scalar blocks** (`CLOUD_COVER`, `WIND`, and the item-5 precipitation block) pair
each key with a *bare* value (`"TOP", 1124`) or a list (`"TOP_COLOR", [192,192,192]`), not
the uniform `key,[list]` the dict view assumes — so `ZrdrDict.FromAlternating` can't read
them; Weather.cs walks them as raw key/next-element pairs (`ScalarAfter`/`ListAfter`/
`Vec3After`). The per-zone blocks are all list-valued and go through the normal dict.

## Colour-triple encoding (the item-3 decode, 2026-07-18)

Colour triples in weather.json use **two coexisting encodings**, even within one file:

- **Normalized float** `[0.69, 0.69, 0.69]` (C1 `FOG_COLOR`; C1B/C3 night-blue
  `[0.063, 0.094, 0.188]`; a C2 sky-fog `[0.80, 0.84, 1.0]`).
- **Integer 0–255** `[192, 192, 192]` (C4/C5 `FOG_COLOR`; C5 `[16,16,16]`; **every**
  `TOP_COLOR`/`BOTTOM_COLOR` seen — `[192]³`, C5 `[220]³`/`[64]³`, C1B/C3 `[32,56,72]` —
  integer-encoded even in the float-fog night chapters).

Rule (`Weather.ParseColor`): **divide the triple by 255 iff any component is strictly
> 1.** Verified unambiguous across *every* weather.json in the install:
- the only `1.0`-bearing colour is the float sky-fog `[0.80, 0.84, 1.0]` — its max is
  *exactly* 1, so strict `>1` correctly leaves it a float (a `>=1` test would wrongly
  shrink it);
- no integer colour is all-{0,1} (smallest non-zero integer component is 16); `[0,0,0]`
  is black under either interpretation.

The normalized value is a **DX7 sRGB framebuffer colour**: `WeatherRig` converts it
sRGB→linear before handing it to the world shader (which mixes fog in linear space).
Round-trip check: a fully-fogged pixel renders back at its source byte value — C4's 192
measures 192 gray, C1's 0.69 measures 176 (0.69·255).

*Bug this fixed:* the integer chapters previously built `Color(192,192,192)` (an HDR
colour far above 1), which sRGB→linear then blew to pure white — C4's Rocky-Mountains fog
was a blown-white wall with a hard horizon cut instead of its data's 192 haze.

## Per-zone fog (`ZONE<n>`)

### The zone names are per chapter, not a fixed `ZONE1`/`ZONE2` pair (2026-07-22)

Surveyed across all 53 `weather.json` in the install:

| chapter | zones (file order) |
|---|---|
| C1, C1B, C1C, C2, C2B, C3, C4 | `ZONE1`, `ZONE2` |
| **C5** | `ZONE1`, **`ZONE3`** — all 8 missions, no `ZONE2` at all |

Corroborated by the gamez `horizon` subtree, whose children carry the same names: C5's are
`zone3`/`zone1`, everyone else's `zone1`/`zone2`. (Note the two orders disagree — C5's
weather.json lists `ZONE1` first, its horizon lists `zone3` first — so "the first zone" is
only well defined per file.)

*Bug this fixed (polish-3 item 2):* the reader hardcoded `{ "ZONE1", "ZONE2" }`, so C5's
`ZONE3` was never read and the `zone2` default matched nothing — **every C5 flight rendered
with no fog and `WorldLight` 1 (fullbright)**, and its skydome built empty because
`BuildHorizon("zone2")` skipped both of C5's zone children. The zone table is now read from
whatever `ZONE<digits>` keys the file carries (the `SW_*` twins stay excluded), and an absent
request falls back to the file's first zone (`WeatherState.ResolveZone`), logged once.
**The default stays `zone2`** — see the selection note below.

### Which zone a mission flies is not in any file (searched exhaustively 2026-07-22)

Not in the mission `zrdr` (`ia`, `objectives`, `targets`, `dzones`, `aiv`, `location`, `map`,
`egen`, `net`, `startanims`), not in the 53 mission `.gw` interp scripts (1,215 statements,
zero zone mentions), not in the ROF/DLL string tables (`DEBUGINFO.TXT`'s zone strings are
Danger-Zones **UI widget** names — `ozonestitle`, `o_radbutzone`). Zone selection therefore
happens engine-side in the binary — the same shape as the `fire2` trigger (see
[anim-definitions.md](anim-definitions.md#fire-templates-flipbooks-and-a-trigger-that-lives-in-the-exe)).

**`interp.json` neither (re-read in full 2026-08-06).** Four zone-bearing strings in its 98
scripts, and none of them selects anything:

- `support\c1\load.gw` `CameraSetHorizonXZ zone2_cloud_floor` (×2) and `support\c1b\load.gw`
  `CameraSetHorizonXZ moon_reflection` (×2). **Neither argument is a node either chapter's gamez
  holds** — both are dangling. All 8 chapters additionally carry the plain
  `CameraSetHorizon horizon` (×2 each), so the verb pair is camera anchoring by node name, and
  `moon_reflection` shows the `XZ` form is not about zones at all.
- `support\c1\tex_fx.gw` and `support\c1b\tex_fx.gw` `FindNode h_zone1scroll` +
  `Object3DSetScroll on 0.07 0.0` — a UV scroll on the sky band, not a selection. C1 has that
  node; **C1B does not** (its `horizon/zone1` children are `g1163`–`g1166`), so C1B's statement is
  dead as well. `MissionSetup` already treats an unmatched `FindNode` as expected, not a warning.

⚠ **This retires the "C1's own scripts disagree" reading**, which had made C1 the priority for the
remaining `BL-100` A/B. The two C1 strings are not two zone selections: one names a node C1 does
not have, the other scrolls a texture. Nothing in `interp.json` bears on which zone a mission
flies.

So the remake selects it via `--sky-zone` (default `zone2` = night). **Every chapter is now
settled**, in three passes and by three different instruments:
[the horizon's own geometry](#the-horizons-own-geometry-settles-three-chapters-2026-08-06) decides
C1B, C2 and C3 from the data; C5 fell to a user A/B in 2026-07; and
[the last four](#the-remaining-four-are-settled-c1-c1c-c2b-and-c4-all-fly-zone2-2026-08-08) fell to
a dome-identity render in 2026-08. C5's two candidates were the far-apart pair:

| | `ZONE1` | `ZONE3` |
|---|---|---|
| `FOG_COLOR` | `[0,0,0]` | `[16,16,16]` |
| `FOG_RANGES` | 1500 – 2250 | **50 – 250** |
| `CLIP_RANGES` | 5 – 2500 | **5 – 300** |
| `FOG_ALTITUDE` | 9000 – 10000 | 9000 – 10000 (identical) |

Note the identical `FOG_ALTITUDE`: whatever else distinguishes C5's two zones, altitude cannot be
it. ⚠ **`FOG_ALTITUDE` does not select a zone in any chapter** — the altitude-triggered
zone-switch reading died on C2, whose `ZONE1` band top (1024 m) is routinely flown while its
`horizon/zone2` holds **zero** meshes, so a switch there would open a hole in the sky
(`PLAN-overcast-match` B11). It is a fade inside one zone's fog, nothing more; see
[the zone keys](#zone-keys) below.

#### C5 is settled: `zone1` (user A/B against the original, 2026-07-22)

The user flew C5/IA1 in the original and **can see across the city**, which `ZONE3`'s 50–250 m
fog and 300 m clip make impossible. So C5 = `zone1`, and the far-apart candidates above are no
longer an open question.

The remake already renders it: the `zone2` default matches nothing in C5 and
`WeatherState.ResolveZone` falls back to the file's first zone, which is `ZONE1`. That is
**stable, not lucky** — all 8 C5 missions list `ZONE1` before `ZONE3`, so every one resolves to
`zone1`. Verified at runtime: `weather: C5/IA1 has no 'zone2' (zones: zone1/zone3) — rendering
'zone1'`, fog 1500–2250.

⚠ **Do not "simplify" the fallback into taking the horizon subtree's first zone instead.** The
two orders disagree — C5's weather.json lists `ZONE1` first, its horizon lists `zone3` first
(above) — so that change would silently render the sky of one zone with the fog of another.
`WeatherRig`'s `LoadWeather` step resolves against weather.json *first* and passes the result into
`BuildHorizon`, which is what keeps the pair consistent; `BuildHorizon`'s own fallback is a
no-op in that path and exists only for a mission with no weather.json at all.

#### The horizon's own geometry settles three chapters (2026-08-06)

`ZONE2` being *defined* is not the same as it being *flown*. In **C1B, C2 and C3** the gamez
`horizon/zone2` node is a bare marker — `model_index: -1`, `child_indices: []` — so the `zone2`
default resolved to itself (they do carry `ZONE2` fog) and `BuildHorizon("zone2")` built a dome of
**zero meshes**: what rendered was the engine's clear colour, with a hard horizon cut. The fog was
wrong the same way (C3 wore night-blue on a mission its own data lights at diffuse 1.5, sun 25° up;
C1B fogged only the 1128–1256 m band).

Meshed nodes under each `horizon/zone*` subtree, install-wide (the zone node included, gamez child
order — which is **not** zone-number order):

| chapter | horizon zones | flown |
|---|---|---|
| C1 | zone1 **2**, zone2 **4** | **zone2** (both build — settled by render, below) |
| **C1B** | zone1 **4**, zone2 **0** | **zone1** |
| C1C | zone2 **4**, zone1 **1** | **zone2** (both build — settled by asset parity, below) |
| **C2** | zone2 **0**, zone1 **3** | **zone1** |
| C2B | zone2 **2**, zone1 **1** | **zone2** (both build — settled by render, below) |
| **C3** | zone2 **0**, zone1 **3** | **zone1** |
| C4 | zone2 **4**, zone1 **1** | **zone2** (both build — settled by render, below) |
| C5 | zone3 **1**, zone1 **2** | zone1 (no `zone2` at all — the fallback above) |

`BL-036`'s node counts agree: C1B and C3 author their worlds under zone 1 (2,101 and 1,647 nodes,
against 2 in zone 2), C2 likewise (766 against 1).

So `WeatherState.PreferPopulatedHorizonZone` swaps the request for the one zone that has geometry,
and only then — the rule is deliberately narrow, and every other shape keeps the request:

- the request builds a dome → kept (C1, C1C, C2B, C4 are untouched, and their choice stays the
  open fidelity question below);
- the request is not one of the horizon's zones at all → kept, so `ResolveZone`'s weather-file
  fallback owns it (C5). **This is why the rule must not simply take the horizon's first zone**:
  the two orders disagree (C5's weather.json lists `ZONE1` first, its horizon lists `zone3` first),
  and doing so would render one zone's sky under another's fog;
- no zone, or more than one, builds geometry → kept (not decidable from geometry).

The correction is applied only if the mission's own weather.json also defines fog for the new zone,
so the sky and the fog can never come from different zones — and it is skipped entirely under an
explicit `--sky-zone=`, which stays a literal request (empty dome and all) for inspection and for
the repro poses recorded in `analysis/`. Coverage: `CSVM.Tests/SkyZoneTests.cs` pins both the rule
and the real per-chapter census.

⚠ **A dome bigger than the far plane is clipped open.** The dome is camera-anchored, so its far
wall sits at (its own radius × `GameSession.HorizonScale`) from the eye. Every chapter's dome is
6.4–12.0 km and clears the 40 km far plane at the 2.5× anchor scale — except **C1B's zone1 at
21.8 km**, where 2.5× reaches 54.5 km and the sky renders as a hole onto the engine clear colour
(seen at the controls the moment this selection first chose that zone). `HorizonScaleFor` therefore
treats 2.5× as a maximum and fits the dome inside `HorizonFarFraction` of the far plane; C1B lands
at ~1.65×, every other chapter keeps 2.5× exactly.

#### The dome's below-horizon skirt is painted `FOG_COLOR` (2026-08-08, `PLAN-overcast-match` B18)

Every chapter's dome is **two pieces sharing one ring at local Y = 0** — which, the dome being
camera-anchored, is always the camera's own altitude, i.e. the horizon line:

- the **wall**, textured (`Sky1.tif`/`c4sky2.tif`/`c5sky2.tif`/`sky2.tif`), running Y 0 → +1.6…+4.1 km
  with a vertex-colour gradient, closed by a flat cap polygon at the top;
- the **skirt**, an inward-tapering cone running Y 0 → −3.0…−11.7 km, closed by a flat disc.

The skirt carries **no texture**: one `Colored` material, and its colour is the flown zone's own
`FOG_COLOR`, byte-exact in six of the seven chapters that have one:

| chapter (flown zone) | `FOG_COLOR` | skirt `Colored` | skirt node |
|---|---|---|---|
| C1 `zone2` | 176,176,176 | 176,176,176 | `g1155` |
| C1B `zone1` | 16,24,48 | 16,24,48 | `g1165` |
| C1C `zone2` | 176,176,176 | 176,176,176 | `g1155` |
| C2 `zone1` | 205,215,255 | 205,215,255 | `g1155` |
| C2B `zone2` | 176,176,176 | 176,176,176 | `g1168` |
| C3 `zone1` | 201,201,201 (0.79) | 200,200,200 | `g1155` |
| C4 `zone2` | 192,192,192 | 192,192,192 | `g1166` |
| C5 `zone1` | 0,0,0 | textured `c5sky2.tif`, vertex colour 0,0,0 | `g1171` |

**That is how the original hides the horizon seam.** Terrain fades to `FOG_COLOR` with distance and
stops at the map edge; below the horizon the dome simply *is* that same colour, so there is nothing
to blend. The seam needs no gradient, no fog on the dome, and no scaling change — B18's whole fix
was to stop applying that one authored colour twice (`docs/architecture.md`, `SceneBuilder.cs`).
⚠ **A skirt colour that does not match its zone's `FOG_COLOR` means the wrong zone is being flown**,
not that the dome needs painting — the pair is the check.

#### The wall's LOWEST ring is painted `FOG_COLOR` too, then grades to sky (2026-08-09, `C26`)

The wall shares more than a ring with the skirt: its bottom row of vertices carries the same
`FOG_COLOR` value, so the two pieces meet at one continuous colour and the join is invisible by
construction. Install-wide, flown zone, no exceptions:

| chapter | `FOG_COLOR` | wall base ring | grades to (next ring up) |
|---|---|---|---|
| C1 / C1C / C2B | 176,176,176 | 176,176,176 | 99,112,154 |
| C1B | 16,24,48 | 16,24,48 | 36,48,72 |
| C2 | 205,215,255 | 205,215,255 | 178,193,255 |
| C3 | 200,200,200 | 200,200,200 | 199,207,218 |
| C4 | 192,192,192 | 192,192,192 | 173,180,203 |
| C5 | 0,0,0 | 0,0,0 | 16,18,27 |

⚠ **The gradient above that ring is authored and steep, and it is NOT a defect to paint over.** On
C1's wall the next ring up sits at only **9.8° elevation**, so the first degree above the horizon has
already lost ~7 units — measured on our own render as **176.00 at the horizon falling to 168.60 at
13 px** (1.24°), which is the authored interpolation to the decimal. The wall's texture cannot mask
it either: the shipped `rtexture*` `sky1` carries a **27-row white band** top and bottom, so the
lowest ~13 px of wall multiplies the vertex colour by exactly 1.0 and the authored value lands once,
unmodulated (a flat-white `--tex-override` there leaves the frame byte-identical). **The original's
own frames show no such gradient near the horizon** — `C1 IA1 Fog river.png` is dead-flat `175.00`
(per-row sd 0.00) for 36 px above its horizon — so whatever hides it in the original is *not* a wall
painting rule. Do not "fix" the wall's colours; see `PLAN-overcast-match` `C26`.

#### The remaining four are settled: C1, C1C, C2B and C4 all fly `zone2` (2026-08-08)

These four define `ZONE2` *and* build a dome for it, so the geometry above cannot decide them, and
the `interp.json` re-read had already retired the "C1's scripts disagree" tiebreak. The **render**
decides, at a pose where the dome fills the frame — and all four land on the zone the remake
already defaults to, so nothing in the selection code changes (`PLAN-overcast-match` B12).

| chapter | verdict | what settled it |
|---|---|---|
| **C1** | **`zone2`** | `OriginalScreenshots/C1 IA1 Cloud Puffs and Moon.png` shows a crater-textured **moon**; `C1 IA1 Fog above clouddeck.png` shows a **faint star field** (8 isolated 1–2 px points, +13…+29 over a sky of sd 3.4). Only `horizon/zone2` holds a `moon` or `stars` mesh — `zone1` is `h_zone1scroll` + `o28`. At the pinned above-deck pose our `zone2` measures **(66.1, 74.6, 105.0)** against the original's **(64.9, 73.6, 103.1)**, inside ±10 on every channel; `zone1` renders flat **(169.4, 169.6, 170.7)** — off by +104/+96/+68 and neutral grey where the original is blue |
| **C4** | **`zone2`** | `playtest/CAP-12/c4/t21.5-1230m-above.png` shows the same moon disc cut by the top-left frame edge. C4's originals are video-graded, so absolute levels are not comparable and the within-frame statistic is the blue shift: the original runs **B−R +8.6…+15.6**, our `zone2` **+29.7**, our `zone1` **exactly 0.0** |
| **C2B** | **`zone2`** | `playtest/CAP-11/t50-c2b-above-deck.png` sky **(71.7, 77.6, 110.3)** vs our `zone2` **(64.3, 72.3, 100.5)** — inside ±10; `zone1` is flat `176³` |
| **C1C** | **`zone2`** — **asset parity only** | no original C1C footage exists. Its `horizon/zone2` is the *same four meshes with the same bboxes* as C1's (`moon`, `stars`, `g1155`, `h_zone2scroll`); its `zone1` is a single mesh rendering a featureless field. This one verdict rests on identity with a settled chapter, not on a comparison |

⚠ **A daylit mission does render a moon and a star field.** Three arguments pointed at `zone1` —
the moon/stars read as nocturnal on missions lit as day; only `ZONE1` is per-mission-tuned across
the 53 weather files; `zone1`'s `FOG_ALTITUDE` equals `[CLOUD_COVER BOTTOM, band centre]` exactly
3/3 — and all three rest on that single unstated premise, which the original's own stills refute.

⚠ **Shoot the comparison with fog neutralised on BOTH sides** (`--no-fog`). With the dome fogged,
either zone renders the same flat grey below the band and the test is degenerate (`METHOD-1`).

⚠ **The gamez `zone_id` node census is not admissible evidence about which zone a mission flies**,
even though it happened to agree. Nothing in `CSVM/src` reads `zone_id`; its agreement with the
four already-settled chapters is degenerate (three of them have no zone-2 world to disagree with,
and C5 — the one that could speak — disagrees); and in C1 the visibility reading contradicts
itself, since `zone_id 1` holds the mission's own `ap_transmitter` and `dz1`–`dz5` targets while
`zone_id 2` holds the entire cloud deck, all 9 `fvol` volumes and the dome, so a draw-only-the-flown-zone
gate leaves mission content unbuilt whichever zone is chosen. It is a partition tag of one world
whose runtime meaning is unknown. Do not re-cite it.

**The skirt/`FOG_COLOR` pair agrees — but it cannot discriminate here.** Each dome's untextured
skirt is authored in its zone's own `FOG_COLOR` (above), and every render of these four is
consistent with that: C1/C1C/C2B show a 176 skirt against 176 fog, C4 a 192 skirt against 192. It
is a *consistency* check only in these four chapters, because both of their zones ship the **same**
`FOG_COLOR` (C1/C1C/C2B `0.69³`, C4 `192³`) — so the pair would look right under either verdict.
Where the two zones' colours differ it is a real check, which is the form the ⚠ above states it in.

### Zone keys

List-valued dict keys:

| Key | Meaning |
|---|---|
| `FOG_COLOR` | fog colour (dual-encoded, above) |
| `FOG_RANGES` | `[near, far]` metres of **horizontal** view distance (the original's fog volume is a vertical cylinder around the camera, not a sphere). A D3D `FOGSTART`/`FOGEND` pair, ramped **linearly** between them — see below |
| `FOG_ALTITUDE` | `[low, high]` metres: full fog at/below `low`, none at/above `high` — the cylinder's vertical fade. C4's `[10000, 11000]` sits above every flyable altitude ⇒ a pure cylinder, full fog at all heights (matches RM's valley haze) |
| `CLIP_RANGES` | `[near, far]` hard clip; `far` kept as informational (the remake's far plane is much larger — fog, not the clip, hides distant terrain) |

#### The ramp between `near` and `far` is LINEAR, and the authored values are used unscaled (2026-08-08, B15)

**Confidence: the curve is traced to a field in the data; the render could not discriminate it.**

`FOG_RANGES` is a D3D `FOGSTART`/`FOGEND` pair, and the *mode* is declared beside it in the
gamez: every chapter's `world1` node carries a `World` struct whose `fog_state` field is the raw
u32 **1 = LINEAR** (mech3ax `nodes/src/cs/world/data.rs` **asserts** it, so it is present and
identical in all eight chapters; `0` in that field would mean OFF and `2` EXPONENTIAL). The rest
of that struct — `fog_color`, `fog_range`, `fog_altitude`, `fog_density` — is zeroed, which is
what makes a *written* 1 significant rather than doubtful: the runtime fog parameters come from
this file, and the node declares only the mode. So `csky_fog_amount` ramps
`clamp((d − near) / (far − near), 0, 1)`; it used a `smoothstep` until B15, and that S-curve was
the remake's own invention with nothing behind it.

⚠ **The renders did not decide this, and the record should not pretend they did.** A smoothstep
is *below* the linear ramp for the near half of the range and *above* it for the far half, so it
trades one end for the other and every measured pose splits accordingly: at C3's canyon pose the
smoothstep leaves more terrain unwashed (canyon slope 86 vs 106, vegetation 29 % vs 22 % of the
lower frame, against the original's 36.5 and 47.8 %), while at C1's river pose and on C1B's
moonlit cloud tops the linear ramp reaches further (deck-ceiling texture surviving to 3.7 km vs
3.6 km; cloud p90 187 vs 193 against originals at 155–182). Both are "our fog is still too
strong", at opposite ends — which is a statement about a residual, not evidence for a curve. The
authored field breaks the tie.

**`VIEWING_RANGE` does not scale them.** All eight chapters ship HIGH `FOG_SCALE`/`CLIP_SCALE`
**1.0** (MED 0.85, LOW 0.7) — every multiplier in the block is ≤ 1, so nothing in the shipped data
shortens a range. The remake used to halve them with a `fogRangeFactor = 2.0` in `WeatherRig`
("this does not seem to be radius but diameter"); that TUNE predated the fog-colour sRGB fix and
is **deleted**. Measured at C1's river pose, the halved range washed the overcast ceiling to flat
fog at ~1.8 km against the original still's ~12.6 km; the authored range takes that to ~3.7 km.

**The fade is by FRAGMENT altitude, not by the camera's** (2026-08-08, `PLAN-overcast-match`
B13). Settled at the controls of the original in **C2** — the only chapter whose flown band sits
inside the flight envelope, so the only place the two readings differ at all: climbing well above
`ZONE1`'s 1024 m top, the distant city **"still dissolves into haze"**. A camera-altitude fade
would have switched the fog off entirely up there and handed the ground back its unfogged
luminance, ~100 units away on distant ground, so the observation is categorical rather than a
judgement of degree. That is exactly what `csky_fog_amount` computes
(`CSVM/shaders/csky_atmosphere.gdshaderinc`), and this observation is the whole of the evidence
for it.

⚠ **The old corroboration is retired: "C1/IA1 `zone1` 970→1047 is exactly cloud-band-bottom →
whiteout-centre" belongs to a zone C1 never flies.** C1 flies `zone2` (settled above), whose band
is 4000→5000 m — above the 2500 m flight ceiling, i.e. night fog at every flyable altitude. The
identity itself is real and it is **3/3** across every chapter whose band is reachable at all —
C1 970/1047, C1C 1055/1082.5, C2B 924/1024, each exactly `[CLOUD_COVER BOTTOM,
WeatherState.CloudBandCentre]` — but it is an identity in the **unflown** zone in all three, so it
cannot be evidence for what `FOG_ALTITUDE` does at runtime. It is recorded here as a real and
unexplained property of the authoring, not deleted (`PLAN-overcast-match` B11/B12).

⚠ **Exactly one flown band in the whole install is inside the flight envelope, so exactly one
chapter exercises the altitude term.** Every other chapter's flown zone authors 4000–5000 m
(C1, C1C), 9000–10000 m (C2B, C3, C5) or 10000–11000 m (C1B, C4) — all above the 2500 m ceiling,
where the term is a constant 1.0 and `FOG_ALTITUDE` is inert. Only **C2 `ZONE1`'s 256–1024 m** can
tell one altitude rule from another. A degenerate census is a fact about the instrument, not about
the question (`INSTR-7`): measure any change to the altitude term in C2, and do not tune it
against a scene that cannot exercise it.

## World lighting (`SUNLIGHT_*`, the item-6 decode, 2026-07-18)

Each zone also carries a `SUNLIGHT_*` block — the directional light the original uses to
light the baked-vertex world. List-valued keys:

| Key | Meaning |
|---|---|
| `SUNLIGHT_ACTIVE` | `[1]`/`[0]` |
| `SUNLIGHT_ORIENTATION` | `[pitch, yaw, roll]°` sun direction |
| `SUNLIGHT_DIFFUSE` | directional intensity (`[0.4]`…`[2.0]` across the install) |
| `SUNLIGHT_AMBIENT` | ambient floor (`[0.15]`…`[0.6]`) |
| `SUNLIGHT_COLOR_DIFFUSE` / `SUNLIGHT_COLOR_AMBIENT` | light colours (usually white) |
| `SUNLIGHT_STATIC` / `SUNLIGHT_BICOLORED` | flags |

`DIFFUSE`/`AMBIENT` **vary per mission and track the scene**: C1B night `0.6 / 0.15`
(dim), C1/IA1 overcast `1.2 / 0.25`, C1C day missions `2.0 / 0.6` (bright). This is the
data source for the remake's **world-brightness calibration** (item 6): the remake renders
the world fullbright (texture × baked vertex colour), which is *brighter* than the original
because the original also modulates by this SUNLIGHT. The original's directional term,
averaged over the predominantly up-facing world (ground + cloud deck), collapses to a
per-mission scalar `WorldLight = clamp(AMBIENT + DIFFUSE·k, 0.15, 1)`, with `k ≈ 0.46` the
average up-facing sun incidence — **one TUNE constant** calibrated to the C1/IA1 reference
(`OriginalScreenshots/C1 IA1 Zone1 environment Spawn3.png`: overcast deck 210→169, terrain
→~57). It then self-scales from the data: C1/IA1 → 0.80, C1B night → 0.43, C1C day →
clamp 1.0. **Confirmed against original footage 2026-08-07** (`CAP-11`, matched-pose A/B at
0.426 / 0.784 / clamp 1.0 — C1B terrain −12%, C2B deck tops −9%, C2 suburb +5–15%;
`git log --grep=BL-110`, evidence `playtest/CAP-11/README.md`). Two exemptions the original
applies that we don't yet: water renders unmodulated (`BL-304`), and night cloud sprites are
directionally moonlit rather than uniformly dimmed (`BL-118`). `Weather.WorldLightFactor` computes it (`ZoneFog.WorldLight`); `WeatherRig` sets
the global shader scalar `csky_world_light` — **linearised** first, so the shader's
linear-space `ALBEDO ×` lands the dimming in gamma space (matching the DX7 chain
texel×vertex×light, all sRGB-space; a raw linear ×0.80 only reaches 210→190, gamma-space
lands 210→169). Applied before the fog mix, so `FOG_COLOR` is unaffected. Paired with the
**gamma-space vertex modulate** (the other item-6 half — see `SceneBuilder.cs`), which fixes
the terrain's washed-yellow → saturated-green hue independent of brightness.

⚠ **It does not reach every surface, and that is the data's decision, not a special case.** Since
2026-08-02 a model authored `flags.lighting: false` skips the multiply entirely — the original turns
D3D lighting off for it, so it draws at full brightness ([gamez.md](gamez.md)). That is 3,003 models
install-wide: the sprite cards, clutter trees, glows, effect meshes, lit signage and the whole
skydome. Light-source glow flares were already exempt by a hand-rolled rule; the flag turns out to
agree with it, and now covers the rest. The visible consequence is that a night mission's clouds,
splashes and beacons stay bright while its terrain and water still dim.

*Caveat:* `k` rests on the single C1 overcast reference; the night/day self-scaling is a
principled prediction pending a matched C1B-night and a bright-day original to confirm/refine
the constant.

## Cloud cover (`CLOUD_COVER`)

The whiteout band (a vertical altitude band the plane vanishes inside), bare-scalar block:

| Key | Meaning |
|---|---|
| `TOP` / `BOTTOM` | band edges (metres altitude); sight is clear at both |
| `THICKNESS` | depth of the fully-opaque **core**, centred on the band midpoint — **not** an edge transition. C1/IA1 970–1124 ±30 ⇒ clear at 970/1124, total only in 1032–1062, linear ramps between |
| `TOP_COLOR` / `BOTTOM_COLOR` | *(optional)* the **band's** colours (integer RGB) — what the in-cloud whiteout paints. Absent in C1/IA1. Decoded into `CloudTopColor`/`CloudBottomColor` and consumed by `WeatherState.WhiteoutColor` |

⚠ **`TOP_COLOR`/`BOTTOM_COLOR` are not the deck mesh's face tints**, whatever the names
suggest (this page said so until 2026-08-08). Three of the four chapters that author them —
C1B, C3, C5 — ship **no `CloudDeck` mesh at all** (`WorldBuilder`'s coverage table), so there
is nothing there to tint. They track the cloud band, and the render confirms it: C4 authors
`[192]³` and the original's in-cloud veil measures a flat 192 (`BL-118`, `CAP-12`).

**The band's MIDPOINT is load-bearing twice over** (`WeatherState.CloudBandCentre`, one spelling
for both; `A7`, 2026-08-08). It centres the opaque core above, and it is also the altitude at
which the cloud **deck** changes regime — below it the deck is a ceiling carried with the camera,
at/above it a world-fixed floor sitting exactly on the centre, and both ambient cloud populations
are hidden below / shown above, per camera. That is a rendering rule, not a datum: nothing in
`CLOUD_COVER` says so, and it is decoded from the original at the controls. What makes it
invisible is that the two are the same altitude — the deck's jump happens in the middle of the
fully opaque core, so **moving the band or thinning `THICKNESS` exposes a hard pop**. C1/IA1:
flip at 1047, core 1032–1062. See `WeatherRig.DeckRegime` and `docs/architecture.md`'s
`WeatherRig.cs` entry for the mechanism and the ceiling distance's derivation.

**Inferred, and marked as such:** where a mission authors both keys the whiteout lerps
`BOTTOM_COLOR` → `TOP_COLOR` across the band by camera altitude. **The shipped data cannot
falsify this.** Of the four chapters whose band is reachable (C1 970–1124, C1C 1055–1110,
C2B 924–1124, C4 1000–1100) only C4 authors colours and its pair is *equal*, so every blend
rule renders the same picture; the one chapter that would discriminate is C5 (top `[220]³`,
bottom `[64]³`) and its band sits at 9950–10150 m, which no one can fly to. C1B and C3 are
likewise 10 km up — plausibly left in and parked out of reach rather than curated, so their
values are not evidence of intent either.

## Wind (`WIND`)

Bare-scalar block: `STATIC_VELOCITY [x,y,z]` (steady wind m/s) + `RANDOM_MAX_SPEED` /
`RANDOM_ACCEL` (random-gust bounds). Parsed in full and **currently unconsumed**: it used to drive
the drift of the hand-tuned `CloudPuffs` field, which the authored `fogvol.zrd` clutter replaced
on 2026-08-06 (`BL-273`, [fogvol.md](fogvol.md)) — that field is static world geometry and the
reader says nothing about wind moving it.

## Precipitation (the item-5 decode, 2026-07-18)

Some missions end with a precipitation block — the last thing in the root dict, **after
`SHADOW_ANGLES`**, as bare-scalar top-level siblings (not nested under a key, and not a
sub-dict). Decoded fully on this install:

| Key | Type | Meaning |
|---|---|---|
| `TYPE` | string | `SNOW` or `RAIN` (its presence is what gates the whole block) |
| `PARTICLES` | int | **RAIN only** — density hint (all RAIN missions: `100`). SNOW omits it |
| `COLOR` | int-RGB triple | particle tint — every mission seen is `[128, 128, 128]` (mid-gray) |
| `WIND_DIR` | float | drift heading, degrees (all seen: `0.0`) — the precipitation's *own* wind, separate from the cloud `WIND` block |
| `WIND_VEL` | float | drift speed, data units (all seen: `0.8`) |
| `GRAVITY` | float | fall-rate multiplier, data units — **SNOW `1.0`, RAIN `3.0`** (rain falls ~3× faster) |
| `ALPHA_GRADIENT` | float pair | `[0.5, 0.0]` everywhere — `[0]` is the peak opacity (the field is quite translucent) |

Observed values (this install): **SNOW** — C4/IA1 + C4/M01. **RAIN** — C1C/IA1 + C2B/IA1
(byte-identical to each other). C1/C5 IA1 carry **no** block. (The user recalled "rain" in
the Rocky Mountains while the data says SNOW — at flight speed gray streaking flakes read
either way; the data drives it, the A/B confirms.)

**Parsing gotcha** — same as `CLOUD_COVER`/`WIND`: these keys pair with *bare* scalars
(`"TYPE", "SNOW"`, `"WIND_DIR", 0.0`), so `ZrdrDict.FromAlternating` (which needs list
values) can't read them — it treats a bare-scalar key as a valueless flag and drops the
value. `Weather.cs` walks the raw `inner` list instead (`StringAfter`/`ScalarAfter`/
`ListAfter`/`Vec2After`). The keys are unique at `inner`'s top level (the zone sub-lists'
`FOG_COLOR`/`SUNLIGHT_*` are nested one level down, which the flat walkers never descend
into), so first-match is always the right one. Note all numbers arrive as `float` via
mech3ax's `GetSingle()`, so `PARTICLES 100` is `100.0f` — read with `ScalarAfter` and cast.

`COLOR` is dual-encoded like the other colour triples (here always the integer form) and
normalized by `ParseColor`; it's a DX7 sRGB framebuffer value, so the renderer converts it
sRGB→linear (same as `FOG_COLOR`).

Rendered by `CSVM/src/Effects/Precipitation.cs` as one camera-following MultiMesh
field the plane flies through (SNOW = billboarded flakes, RAIN = fall-aligned streak quads);
the data→look scale factors (fall m/s, particle count, box size, streak length) are marked
`TUNE` there.
