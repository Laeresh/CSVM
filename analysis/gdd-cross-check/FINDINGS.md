# `gdd-cross-check/` — the repo's claims vs the shipped data

**Question.** A reading of the original pre-release design document said several things `docs/`
states are wrong, and named several extracted structures nobody had decoded. Which corrections
survive contact with the shipped data, and what can the undocumented readers be made to say?

**Standing rule: shipped extracted data beats the design document.** It is a July 1999
pre-release spec — its *structure* has held up repeatedly, its *numbers* have not. Claims below
are **measured** (from `extracted/**`), **inferred** (structure plus reasoning), or
**design-informed** (meaning from the document, shape confirmed in data).

Run any probe from this directory — `python <probe>.py`. Each locates `extracted/` by walking up
from the script, so it works from the main tree or a worktree; `CS_EXTRACTED` overrides.

| Probe | Question | Verdict |
|---|---|---|
| `gun_cone.py` | Is `gun_pitch`/`gun_yaw` a turret arc? | **No** — the AI's forward-gun cone |
| `dzpath_gates.py` | Does a Danger Zone ship gate geometry? | **Yes** — route + exactly two matched outlines, 80/80 |
| `chapter_distinct.py` | Are C1/C1B/C1C one terrain re-lit? | **No** — separate worlds |
| `damage_pools.py` | Is the `destroyable_parts` hp pair (armor, hit points)? | Strongly supported, **not decidable from this data** |
| `reader_census.py` | What do `ia`/`dzones`/`zeppelins`/`egen` carry? | Full key inventories |
| `commands.py` | Which player commands shipped? | 74 bindable; spyglass + padlock **shipped** |

`zrdrlib.py` is the shared reader (mirrors `CSVM/src/Mech3/Zrdr.cs`). No game data is embedded in
any file here — everything is read from `extracted/**` at runtime.

## Held

- **`gun_pitch`/`gun_yaw` = the AI's forward-gun cone.** 12 defs own them, all `[-11, 11]`, all
  AI airframes. **7 of 12 have no turret**; **0 of 12 player defs carry or inherit them,
  including all five turret airframes**; 60 of 63 non-player defs resolve them (the 3 that do not
  are abstract `basic_airplane` + the two surface vehicles). Presence tracks *is an AI aircraft*.
  Also: a `turrets` entry holds only `title` and `node` install-wide — **turret rotation limits
  are in no reader and stay undecoded.**
- **Danger Zones have gate geometry** (`missions.md` said they had none). **All 80 `dzpathN`
  meshes carry exactly three polygons** — a route ribbon plus a *matched pair* of closed outlines
  (29/80 bit-equal in area, 64/80 within 10 %), separated along the route by a median 11.7 m
  (0 m at a thin slit, 1.65 km down a tunnel run). The design specifies an entry volume and an
  exit volume, both of which must be crossed, so a tangential clip cannot score — hence two.
- **C1/C1B/C1C are separate worlds.** The Sea Haven airfield nodes are in C1 and in **neither**
  C1B nor C1C; C2 and C2B share **zero** identical terrain meshes; danger-zone name sets are
  disjoint. C1C/C2B have no `dz*` markers or `dzpaths` group at all.
- **Damage never degrades performance — by design, not deferred.** Caveat kept: the shipped data
  still sets an `engine` flag, and it is **not tail-only** (tail on 2 planes, nose on 6, both
  wings on 3) — it marks where the engines physically are.
- **Spyglass and padlock shipped.** An earlier pass called them cut because no `*spyglass*`/
  `*padlock*` asset exists. `MSG_CAM2_TOG` = "Toggle Spyglass" (a *camera* command) and all three
  padlock modes plus nine hat directions are in the shipped string table.

## Did not hold

- **`vehicle.md`'s "AI variants differ (25/20)" is false.** **All 88 `destroyable_parts` entries
  across all 22 defs have hp1 == hp2** — no exceptions. The `r*` AI defs mirror their player
  counterparts exactly. The brief that prompted this work repeated the 25/20 claim.
- **The design document does not settle stock ordnance.** Its per-plane hardpoint counts disagree
  with the retail fit, so its mixed-load table is not evidence of what shipped. Our uniform-HE
  table is a retail-UI observation; mixing is already expressible in the engine, only
  `stock_loadouts.json`'s `{count, stock}` is too narrow. A schema limit, not a bug.

## Hypothesis, deliberately not promoted

**The `destroyable_parts` pair is (armor, hit points).** `MSG_HUD_HEALTH` =
`Armor: %1%% Health: %2%%`; 46 weapons carry both `ARMOR_DAMAGE` and `HEALTH_DAMAGE` and **18
differ** — the ammo tiers are built from that split (`wep_31` DD 1.5/4.5 vs `wep_32` AP 4.5/1.5,
mirrored at every calibre); `player.json`'s `crash` splits armor from health ranges; and the
design gives each of four identically-named zones its own armor and hit-point pool, armor first.

**Why it stays a hypothesis:** every shipped pair is equal, so nothing here can separate
(armor, hp) from (hp, hp) or (max, current). **Falsification, at the controls:** rounds-to-kill
one zone with `wep_31` (DD) vs `wep_32` (AP). Different counts support two pools; equal counts
kill it.

`ace_stats` is a weaker second hypothesis — 9 values, and `vehicle.json` has exactly nine
pilot-skill keys emitted in one identical order by all 26 defs carrying them, with `accentID`
broken out just as `ia.json` breaks out `ace_accentID`. But all 8 chapters store `[9]x9`, so the
order is **inferred**; a chapter with non-uniform `ace_stats` would settle it and none exists.

## Instrument bugs hit on the way

- **A "mean terrain height per cell" grid compared clouds to sea and called two different worlds
  identical.** The gamez `terrain` flag is **not applied consistently across chapters** — land
  tiles in C1/C2, only the cloud deck or water plane in C1B/C1C/C2B. It reported C1B and C1C as
  100 % identical (both all-water) and C1C as a uniform y=295 plane (the deck). Replaced by
  landmark presence + vertex-identity hashing. → `docs/verification.md` rule 110.
- **`location.json`/`map.json` cannot discriminate chapters.** Seven of eight name the same
  `map_c1m04`; C1/C1C/C2B ship a byte-identical preset list and C2 those four plus one — Sea
  Haven bookmarks on two Hollywood maps. `location.json` was the proposed instrument for the
  chapter question and would have given a confident wrong answer. → same rule.
- **A generic `unwrap()` silently read zero fields from `zeppelins.json`/`egen.json`.** Their
  roots are lists of *records*; the shared unwrap stopped at the wrapper, so the census walked
  the wrapper as one record and reported instances with no keys — no error.
  `zrdrlib.instances()` handles this shape and carries the warning.
- **Two entries look like commands and are not** — `MSG_WINGMAN_SHOT_DOWN` (a notification) and
  `MSG_DLG_CONTROLS` (a dialog title). Including them inflated the count to 75; it is 74.
