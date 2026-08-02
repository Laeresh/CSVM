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
Danger-Zones **UI widget** names — `ozonestitle`, `o_radbutzone`). The only zone references in
the install are chapter-level and mutually inconsistent: `support\c1\load.gw` says
`CameraSetHorizonXZ zone2_cloud_floor` while `support\c1\tex_fx.gw` says
`FindNode h_zone1scroll`; six other `load.gw` name a plain `horizon`. Zone selection therefore
happens engine-side in the binary — the same shape as the `fire2` trigger (see
[anim-definitions.md](anim-definitions.md#fire-templates-flipbooks-and-a-trigger-that-lives-in-the-exe)).

So the remake selects it via `--sky-zone` (default `zone2` = night), and settling the real
answer needs an A/B against the original — C5 most of all, whose two candidates are far apart:

| | `ZONE1` | `ZONE3` |
|---|---|---|
| `FOG_COLOR` | `[0,0,0]` | `[16,16,16]` |
| `FOG_RANGES` | 1500 – 2250 | **50 – 250** |
| `CLIP_RANGES` | 5 – 2500 | **5 – 300** |
| `FOG_ALTITUDE` | 9000 – 10000 | 9000 – 10000 (identical) |

Note the identical `FOG_ALTITUDE`: in C1 the zones read as altitude bands (zone1 970–1047 at
the cloud floor, zone2 4000–5000), but in C5 altitude cannot be what selects between them.

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

**Still open for C1–C4**, which all define `zone2` and resolve to themselves — C1 most of all,
the one chapter whose own scripts disagree (`load.gw` → zone2, `tex_fx.gw` → zone1).

### Zone keys

List-valued dict keys:

| Key | Meaning |
|---|---|
| `FOG_COLOR` | fog colour (dual-encoded, above) |
| `FOG_RANGES` | `[near, far]` metres of **horizontal** view distance (the original's fog volume is a vertical cylinder around the camera, not a sphere) |
| `FOG_ALTITUDE` | `[low, high]` metres: full fog at/below `low`, none at/above `high` — the cylinder's vertical fade. C4's `[10000, 11000]` sits above every flyable altitude ⇒ a pure cylinder, full fog at all heights (matches RM's valley haze) |
| `CLIP_RANGES` | `[near, far]` hard clip; `far` kept as informational (the remake's far plane is much larger — fog, not the clip, hides distant terrain) |

C1/IA1 corroborates the altitude semantics: `zone1` 970→1047 is exactly cloud-band-bottom
→ whiteout-centre (fog hands over to the whiteout while climbing into the overcast);
`zone2` 4000→5000 sits above the 2500 m flight ceiling (night fog at every flyable
altitude).

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
clamp 1.0. `Weather.WorldLightFactor` computes it (`ZoneFog.WorldLight`); `WeatherRig` sets
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
| `TOP_COLOR` / `BOTTOM_COLOR` | *(optional)* the deck's face tints (integer RGB). Absent in C1/IA1. Decoded into `CloudTopColor`/`CloudBottomColor`; **unused this milestone** — reserved for the item-6 night-brightness calibration |

## Wind (`WIND`)

Bare-scalar block: `STATIC_VELOCITY [x,y,z]` (steady wind m/s) + `RANDOM_MAX_SPEED` /
`RANDOM_ACCEL` (random-gust bounds). Parsed in full; drives the ambient cloud-puff drift
(see `CloudPuffs.cs`).

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
