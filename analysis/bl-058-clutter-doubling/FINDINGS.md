# `BL-058` — does C5 draw doubled clutter buildings?

**Read-only measurement, 2026-08-04, for `docs/PLAN-m3-polish-6.md` item A3.** Run after A1
(`BL-051`, node `active`) and A2 (`BL-056`, overlay passes) both landed, per the plan's dependency
note. No engine code was changed for this item — the question is answered from a live build's own
log plus the existing `analysis/item9-depth-bias/CBLOCK-LOD.md` geometry measurement.

## Verdict

**Yes. C5 places both disjoint clutter-building sets at once, at the same footprint, unconditionally.**
`cblock1/2/3`'s district (`cb00a`–`cb11a` + their `det01` halves) and `cblock4/5/6`'s district
(`cb12a`–`cb24a` + their `det01` halves) are **both stamped in every build of C5** — the base layer's
buildings are not skipped just because a subface layer of a different district sits on top of the
same ground polygons. This is not a hypothetical: it is what today's engine actually does, confirmed
by both a census of a live build's clutter log and a capture at the known repro site.

## 1. Why the two layers can double up at all

`analysis/item9-depth-bias/CBLOCK-LOD.md` §1c/§2 (2026-07-22/23, unrelated investigation that
surfaced this as a side question) already measured the geometry: `cblock4/5/6` are C5's *base*
ground layer and `cblock1/2/3` are OpenFlight **subfaces** — coplanar overlay polygons — laid
directly on top of them, same 256 m tiling, same y-plane. True triangle∩triangle overlap:

| pair | overlap as % of the base's own area |
|---|---|
| `cblock1` × `cblock4` | 88.5% |
| `cblock2` × `cblock5` | 99.88% |
| `cblock3` × `cblock6` | **100.000%** |

So a `cblock4/5/6` base polygon is, for practical purposes, *always* also present under a
`cblock1/2/3` subface polygon at the same X/Z — the two textures coexist as real, separate polygons
at (very nearly) the same footprint. `interp.json`'s `support\c5\adjust.gw` registers all seven
templates (`AddClutterTemplates cblock1`…`cblock7`), and each has its **own, disjoint** building
list — `cblock1`'s `cb00a`–`cb11a` share nothing with `cblock4`'s `cb12a`–`cb24a`.

## 2. `Clutter.cs` does not know about subfaces

`ClutterBuilder.PlaceOnMesh` (`CSVM/src/Mech3/Clutter.cs:632-661`) matches a template to a polygon
by **texture name only**:

```csharp
var tex = _gamez.Materials[poly.MaterialIndex].TextureName;
if (tex == null || !templates.TryGetValue(tex, out var template))
    continue;
```

It never reads `GameZPolygon.Subface`. So a `cblock4`-textured base polygon and the `cblock1`-textured
subface polygon sitting exactly on top of it are two independent, unrelated hits: each stamps its
own template's buildings onto the same grid cell, with no notion that one polygon is meant to be
buried under the other for rendering purposes. The subface fix (`SceneBuilder`'s `SubfaceBias`,
landed before this plan) only changes which *ground texture* wins the z-fight — it has no effect on
`ClutterBuilder`, which runs from a completely separate walk over the same gamez tree.

## 3. Census: a live C5 build stamps both districts

`--freecam --chapter=C5` at the item9 repro pose (`--pos=-9533.178,76.319,-3367.413
--lookat=-9451.281,28.148,-3398.597`, `--no-fog`; log: `.scratch/bl058.log` in this run, not
committed) prints `ClutterBuilder.Summary` in full:

```
clutter: 139388 sprites + 79306 3D decorations (cb00a.flt ×3933, cb01a.flt ×2964, cb02a.flt ×2976,
cb03a.flt ×1979, cb04a.flt ×1998, cb05a.flt ×1984, cb00det01.flt ×3933, cb01det01.flt ×2964, …
cb12a.flt ×12069, cb14a.flt ×13248, cb13a.flt ×4824, … cb24a.flt ×90, …)
```

Summed by district (script: a one-line regex tally over the log's `name × count` pairs):

| district | building names | total placements |
|---|---|---|
| `cblock1/2/3` (`cb00a`–`cb11a` + `det01`) | 24 distinct kinds | **43,873** |
| `cblock4/5/6` (`cb12a`–`cb24a` + `det01`) | 26 distinct kinds | **35,433** |
| (sprites: `lightpole.tif`/`poleflare.tif` glows) | — | 139,388 |

Both district totals are large and nonzero **in the same build** — this is not an edge case at one
node, it is the whole-chapter behaviour. `SolidCount` (79,306) is exactly their sum.

## 4. Capture

`.scratch/bl058_repro.png` (eye-level, the item9 repro pose, `--no-fog`) shows a beige-textured
building slicing diagonally through the frame and clipping into the neighbouring dark towers at an
angle inconsistent with a single authored city grid — the visual signature of two independently
laid-out building sets occupying the same ground. `.scratch/bl058_top.png` (bird's-eye over the same
area) shows the same dense, irregular stacking. Neither screenshot is committed (git-ignored
`.scratch/`, per repo convention); reproduce with the command above.

## What this is not

This is not a rendering-order question (that is A4/`BL-053`'s territory, and the C5 ground z-fight
is already fixed by the subface bias). It is a **clutter-placement** defect: two districts' worth of
buildings exist in the built world at once where the original almost certainly shows only one
(whichever ground layer is meant to be "the" street level at that point — undetermined by this
measurement, see below).

## Open for the fix, not decided here

Per the plan's ground rule against inventing semantics, this item does **not** guess which layer the
original actually shows on top, nor which is meant to spawn no clutter. Candidate mechanisms not
distinguished by anything measured here: (a) the original may simply never draw the base layer's
clutter at all (base clutter roots the covered case, i.e. `PlaceOnMesh` should skip a `Subface`
**base** — but note `Subface` is a property of the *overlay*, not the base, so the base polygon
carries no flag to key off; the subface/base pairing would have to be inferred the same way
`CBLOCK-LOD.md` did it), or (b) the original's own clutter system might have a coplanar-aware skip
this reader has not found evidence for yet. Both are equally plausible from the data gathered here;
neither is implemented. See the new backlog entry `BL-250`.

## Follow-up (same session): a strong candidate fix, still gated on a playtest

Prompted by the user pointing out the two ground textures "look like the same layout" — checked, and
they are. `cblock1.tif`/`cblock2.tif`/`cblock3.tif` (256², night, individually distinguishable
rooftops + lit windows — a finished art pass) and `cblock4.tif`/`cblock5.tif`/`cblock6.tif` (64²,
blurry, no readable building shapes) are not unrelated content: `CBLOCK-LOD.md` §1a already measured
structural correlation `cblock1`↔`4` r=+0.704, `2`↔`5` r=+0.720, `3`↔`6` r=+0.702 (cross-pairs only
0.39–0.53) — independently reproduced here (grayscale-normalized cross-correlation of `cblock1`
downsampled to 64² against `cblock4`: **r=0.705**). Each pair is the *same* painted city-block scene
at two fidelities, not two different districts.

A footprint-matching attempt (`match_footprints.py` — pulling each template's decoration local
positions within its 256 m cell to check whether one district's buildings respect the texture's
painted street gap and the other doesn't) was **inconclusive**: both districts scatter buildings
across their whole cell (e.g. `cblock1`'s `cb00a` at local x∈[28,228] z∈[48,240] vs `cblock4`'s
`cb12a` at x∈[16,246] z∈[100,156]) without an obvious respected/ignored street band either way. This
would need the ground quad's UV-to-world orientation verified before trusting a positional match —
not done here.

**The basis for the candidate fix is structural, not positional:** `CBLOCK-LOD.md` already
established that no live state (day/night, zone, LOD) ever picks between the two passes — the
high-res pass always wins the ground z-fight, so the low-res pass is **always** buried (97.0/99.9/
100.0% overlapped per §2's coverage table, not merely "usually"). A district whose ground art can
never be seen is a reasonable candidate for "its buildings were never meant to be seen either" — but
this is still an inference from the ground layer's fate, not a direct observation of what the
original's downtown actually shows. `playtest.md` `CAP-22` is now queued to settle it before any code
lands. See `backlog.md` `BL-250` for the exemption-list fix shape (mirrors
`TextureArchive.KnownAbsentFromGameData`) and the reasoning for gating it on the capture.

## Ruled out: the unparsed node `zone_id` (`BL-036`) is not the filter

`BL-036` (found 2026-07-22, still open) already flags that `GameZ` parses no node's `zone_id` field
at all. Since C5 uniquely ships `zone1`/`zone3` (every other chapter is `zone1`/`zone2`), and the
node `zone_id` value set matches (`-1`/`1`/`3` in C5 vs `-1`/`1`/`2` elsewhere — see `BL-036`'s
per-chapter counts), it was worth checking whether zone activation is actually the missing filter
behind the clutter doubling. It is not, checked two ways (`zone_check.py` in this directory):

- **Same node, both textures.** The repro node CBLOCK-LOD.md uses (`g4664`, node index 1847) carries
  `zone_id=1` for the *whole node*, while hosting both the `cblock1` subface polygon and the
  `cblock4` base polygon. A node-level value cannot separate two polygons that live on the same node.
- **No clean split at the district level either.** Across every C5 node carrying a `cblock*` texture:
  `cblock1/2/3` nodes span `zone_id` `-1`/`1`/`3`; `cblock4/5/6` nodes are only `-1`/`1` (never `3`).
  If zone selection resolved the doubling, the base district (`cblock4/5/6`) would need to sit
  entirely in the zone the subface district (`cblock1/2/3`) does not — it doesn't.

This corroborates and extends `BL-036`'s own note that both the C5 ground z-fight's surfaces are
`zone_id=1` — the same "checked, not the cause" verdict, now also true for the clutter case.
