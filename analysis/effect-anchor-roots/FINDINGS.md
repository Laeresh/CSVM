# Which template roots the world-effects runtime must stage

**Question (M3-polish-3 D31 / `BL-016`).** The rocket explosion "looks a lot different" from the
original, and the user found per-type ring textures (`ring_ap.png` / `ring_he.png` /
`ring_sonic.png`) in the texture archives. Where does the data author those rings, and does our
explosion build that part of the effect?

Run: `python analysis/effect-anchor-roots/anchor_roots.py extracted`

## Answer

**The rings are authored, and we were not building them.** They are gamez *meshes* driven by
ordinary animation definitions, not particles:

| Rocket | `IMPACT` anim | ring def (`NAME` → `ANIMATION_NAME`) | ring node | texture |
|---|---|---|---|---|
| ARMOR (`wep_05`) | `ap_ground_effect` | `ap_effect` → `call_cracks` | `ap_cracks` | `ring_ap` |
| BOOM (`wep_06`) | `he_ground_effect` | `he_ring` → `call_he_ring`, `he_ring1` → `call_he_ring1` | `he_ringer`, `he_ringer1` | `ring_he` |
| SONIC (`wep_08`) | `sonic_ground_effect` | `sonic_ring1..5` → `ring_up1..4` / `ring_down1` | `sonic_ring` (one per root) | `ring_sonic` |

Each is a flat 8.4 m quad (C1 models 110–123, materials 126/128/129 → textures 122/124/125) that
the def activates, scales up and fades: the HE ground ring 1→7 over 1.6 s with a 0.2 s fade-in and
0.6 s fade-out, the HE upper ring (at +12 m) 1→20 over 2.0 s.

## Why nothing showed

A definition anchors on the gamez node named by its `NAME`. The world-effects runtime binds the
**call closure** of its effect names but staged only 19 template roots, so **14 of the 28 anchor
roots that closure needs were absent** — every def anchored on one was unanchored and played
nothing at all, silently: no ring, no smoke-trail column, no sonic puff cluster, no torpedo ripple.

Missing before D31: `he_ring1`, `sonic_ring1`–`sonic_ring5`, `ap_trails`, `he_trails`,
`flak_trails`, `carnage_trails`, `carnage_ring`, `sonic_puff1`, `sonic_puff2`, `hg_splash`,
`ripple` (plus `ap_cracks`, which is reachable as a child of the staged `ap_effect`).

Every needed root exists as a **single parentless root in all 8 chapters** (`flak_trails` has 4
occurrences, 1 of them a root) — so the staged set can be one static list.

The second half of the miss is rendering: the staged subtree was built under a `Visible = false`
stage, so even the roots that *were* staged (`he_ring`, `ap_effect`) drew no mesh. The measurement
that hid this is `--effects-test`'s "built a puffer" column — it answers a particle question and is
blind to an effect's mesh half (`docs/verification.md` WORLD-19).

## Measured effect of staging the full set (C1, `--effects-test`)

| | before | after |
|---|---|---|
| template roots staged | 19/19 | 34/34 (all 8 chapters) |
| names building a puffer | 25/30 | 27/30 |
| `PufferState(no host node)` | 20 | 0 |
| `he_ground_effect` | started, no puffer | puffer[5] |
| `ap_ground_effect` | puffer[2] | puffer[7] |
| `torpedo_ground_effect` | started, no puffer | puffer[5] |

## 2026-08-02 — the same question for the per-player crash rig (`BL-059` item 2)

Run: `python analysis/effect-anchor-roots/anchor_roots.py extracted player_crash_dirt player_crash_water`

The crash rig stages its own template copy per player (`WorldEffectsFactory.EffectTemplateRoots`),
so it needs the same treatment. The two crash variants' closure is **16 definitions** anchored on
**13 roots**; `player` and `player_pfighter` are the aircraft's own anchors (0 gamez occurrences —
supplied by the crash root and the plane model), leaving **11 gamez template roots**:

| | roots |
|---|---|
| shared / dirt only | `yellow_spark_01`, `flame_ball_01`, `black_smoke_ball_01`, `fire_here`, `carnage_trails`, `flydirt`, `apassengers` |
| sea dive only | `huge_splash_model`, `hg_splash`, `ripple`, `white_water_impact` |

The rig staged 7 before the water variant landed; the four sea-dive roots were the gap, and
`white_water_impact` (`large_steam_spray`) is in **no** staged list anywhere — the world-effects
runtime does not bind that name either. All eleven exist as a single parentless root in all 8
chapters. `yellow_spark_02` is staged but is not in this closure; it is left alone.

Measured after staging them (C1 sea dive, `--debug-anim`): `11 effect template(s)` at bind, every
retarget resolves, 7 crash-rig puffers live (`fire_n_smoke`, `trailpuffer2`, `spurtpuffer1..5`) and
the mesh half draws too (`splash_polys`, `ripple1..3`, `fly_trail1..5` all reported visible).

No game data is stored here — the script reads the player's own `extracted/` tree.
