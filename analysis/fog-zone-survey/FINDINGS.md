# All-chapter fog/sky zone survey — `survey_zones.py`

Read-only survey of every chapter's authored fog zones, written for `docs/plans/PLAN-overcast-match.md`
item **B11** (2026-08-08). The instrument is `survey_zones.py`; run it with no arguments to
reproduce every number quoted here and in the plan's B11 section:

```
python analysis/fog-zone-survey/survey_zones.py            # IA1 of every chapter
python analysis/fog-zone-survey/survey_zones.py --mission M01
```

It reads `Z:\CSVM\extracted` in place and writes nothing. Colours go through the reader's own
rule (`Weather.ParseColor`: divide by 255 **iff any component is strictly > 1**), so the printed
byte triples are what the renderer receives, not what the JSON looks like.

## What it reports

1. **Per chapter × zone** — `FOG_COLOR` (raw → decoded byte), `FOG_RANGES`, `FOG_ALTITUDE`,
   `CLIP_RANGES`, `SUNLIGHT_DIFFUSE`/`_AMBIENT` (+ the derived `WorldLight`),
   `SUNLIGHT_ORIENTATION`, plus `VIEWING_RANGE` HIGH `FOG_SCALE`/`CLIP_SCALE`, the `CLOUD_COVER`
   band with its midpoint and any `TOP_COLOR`/`BOTTOM_COLOR`, and the precipitation type.
2. **Per chapter (gamez)** — the `horizon` subtree's zone children with their meshed-node counts
   and `zone_id`; the `fvol*` volumes' altitude band, count and `zone_id`; the `World` node's own
   engine fog struct.
3. **Per chapter (`fogvol.zrd`)** — `fog_zone`, `distance`, and C5's interior-fog keys.
4. **A cross-mission check** — every mission of every chapter, collapsed to distinct zone
   signatures, so a per-mission difference cannot hide behind IA1.

`IA1` exists for all eight chapters, so nothing is substituted.

## Findings

The full argument, the complete table and the H1–H4 prediction matrix live in
`docs/plans/PLAN-overcast-match.md` § `B11`. In brief:

- **`FOG_ALTITUDE` is inert in the zone every chapter is currently flown in, except C2.** The
  bands are 4000–5000 / 9000–10000 / 10000–11000 in seven of the eight flown zones — above the
  flight envelope *and* above every dome fragment. Only C2's flown `ZONE1` (256–1024 m) is
  reachable. A camera-altitude fade and a per-fragment fade therefore predict the same picture
  everywhere except C2.
- **Every authored skydome is far too low to reach its own 9000–11000 m band.** Local dome tops
  measure +982 m (C4 `zone1`) to +4108 m (C1B `zone1`); the tallest object anywhere is C1B's
  moon sphere at ±5605 m. Even at the remake's 2.5× anchor scale only C5's `zone1` apex
  (3766.8 × 2.5 = 9417 m) touches a band at all. So no altitude fade of any kind can un-fog a
  dome in C3, C2B(zone2), C5 or C1B — which is why our renders of those three read as the flat
  authored fog colour (201.0 / 176.0 / 0.1 measured, `CAP-11`) while the originals show a dome.
- **`zone1`'s `FOG_ALTITUDE` equals `[CLOUD_COVER BOTTOM, CLOUD_COVER centre]` in all three
  chapters whose cloud band is reachable** — C1 970/1047, C1C 1055/1082.5, C2B 924/1024 — an
  exact three-for-three identity against the band's own midpoint (`WeatherState.CloudBandCentre`).
- **`zone1` is the per-mission-tuned zone; `zone2` is boilerplate.** Across all 53 weather files
  only C1 and C4 vary anything, and both vary `zone1`'s `FOG_RANGES` per mission while leaving
  `zone2` at a constant 1000–4000 / 1000–4500.
- **`SUNLIGHT` is identical between a chapter's two zones in seven of eight chapters** — C2 is
  the sole exception (`ZONE1` 1.1/0.5 → WorldLight 1.0 vs `ZONE2` 0.4/0.6 → 0.784). So world
  brightness can only ever test the zone choice in C2, and `CAP-11`'s C2 clamp point already
  matches 1.0.
- **The cloud deck, the `fvol` volumes and every `cloudparent` cluster are authored
  `zone_id: 2`** in C1/C1C/C4 (C2B's deck likewise), the same tag as those chapters' moon/stars
  dome, while `zone_id: 1` holds no node at deck altitude at all in C1. Under `docs/HISTORY.md`'s
  recorded reading of `zone_id` (`-1` = always, `1`/`2`/`3` = that zone only), that is a data-side
  argument that C1/C1C/C2B/C4 fly **zone 2** — and it contradicts the dome-content argument
  (their `zone2` dome carries `moon` + `stars` on daylit missions). Not resolved here; it is
  item B12's.
- **`fogvol.zrd`'s `fog_zone` reads as a 0-based index into the weather file's own zone list**,
  naming the zone a volume's *interior* fog uses: C5's `fog_zone` 1 → `ZONE3`, and C5's
  `fogvol.zrd` `fog_color` `[16,16,16]` is byte-identical to `ZONE3`'s `FOG_COLOR`. This does
  **not** re-open the mission-zone decode (`PLAN-overcast-match` wrong-claim #6 stands — under
  this reading C5's volumes name `ZONE3` while the mission is settled `zone1`, which is exactly
  why `fog_zone` cannot be the mission selector). C5's `fog_fade_dist` / `interior_fog_fade_dist`
  are **16.0 m** — a boundary blend width, not a fog range.

No conclusion from this survey has been written into `docs/formats/`; per the plan's ground rules
those wait for B14's render verification.
