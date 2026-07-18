# weather.json — per-mission atmosphere (validated on this install)

Part of the project's format documentation (see also `zrdr.md`, `world-structure.md`,
`gamez.md`). Covers the mission's `weather.json` reader: distance fog, the cloud-cover
whiteout band, wind, and the shared **colour-triple encoding rule**. Consumed by
`CrimsonSkies/src/Flight/Weather.cs` (`WeatherState`) + `PlaneViewer.SetupWeather`.

Seeded 2026-07-18 with the item-3 fog-colour decode; precipitation (item 5) and the
night-brightness deck tints (item 6) grow it as those land.

## Location & shape

One `weather.json` per mission folder, in the mission's own zrdr archive
(`extracted/<chapter>/<mission>/zrdr/`), alongside `ia.json`/`objectives.json` (see
`zrdr.md`). Some folders are multiplayer-only or lack the file — the loader returns "no
fog" then.

`weather.json` root[0] is one alternating **dict** (`key, [values…]`, see the zrdr
conventions). Its blocks:

| Block | Read as | Notes |
|---|---|---|
| `VIEWING_RANGE` | dict | not used by the remake |
| `WIND` | **bare-scalar block** | `STATIC_VELOCITY [x,y,z]`, `RANDOM_MAX_SPEED s`, `RANDOM_ACCEL a` |
| `CLOUD_COVER` | **bare-scalar block** | `TOP`, `BOTTOM`, `THICKNESS` (metres) + optional `TOP_COLOR`/`BOTTOM_COLOR` |
| `ZONE1` / `ZONE2` | dict (+ `SW_*` twin) | per-zone fog; the `SW_*` software-renderer twin is ignored |

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

The normalized value is a **DX7 sRGB framebuffer colour**: PlaneViewer converts it
sRGB→linear before handing it to the world shader (which mixes fog in linear space).
Round-trip check: a fully-fogged pixel renders back at its source byte value — C4's 192
measures 192 gray, C1's 0.69 measures 176 (0.69·255).

*Bug this fixed:* the integer chapters previously built `Color(192,192,192)` (an HDR
colour far above 1), which sRGB→linear then blew to pure white — C4's Rocky-Mountains fog
was a blown-white wall with a hard horizon cut instead of its data's 192 haze.

## Per-zone fog (`ZONE1`/`ZONE2`)

Which zone a mission actually shows is **not** in these readers — the remake selects it
via `--sky-zone` (default `zone2` = night). List-valued dict keys:

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

## Precipitation *(item 5 — not yet decoded here)*

Several missions carry a bare-scalar precipitation block after `SHADOW_ANGLES`
(`TYPE SNOW|RAIN`, `COLOR`, `WIND_DIR`, `WIND_VEL`, `GRAVITY`, `ALPHA_GRADIENT`;
RAIN adds `PARTICLES`). C4 (SNOW), C1C/C2B (RAIN); C1/C5 IA1 have none. Documented when
item 5 lands.
