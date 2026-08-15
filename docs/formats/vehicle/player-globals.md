# Player global blocks

Part of: [vehicle definitions](../vehicle.md).

## `player.json` — the player-global blocks

One shared reader, flat alternating `KEY, [values…]`, ~45 top-level keys. Flight globals
(`nom_gravity`, `maxAOA`, `liftAOAs`, `highGs`/`lowGs`, the `yaw_*`/`turn_*` fade curves,
`stall_mag`, `drag_factor`) feed `PlaneStats`; the sound curve blocks (`engine_sound`,
`prop_sound`, `rattle`) are in [sounds.md](sounds.md). Three whole subsystems in it are
**undocumented elsewhere**, and they stand on different evidence: the aim assist is **decoded from
the executable** ([`org/aim-assist.md`](../org/aim-assist.md), 2026-08-10); the other two have
data-confirmed key names and values with meanings **inferred** from those names, one of which (the
near-miss counter) is implemented on that inferred reading. None appears in the original design
document, so they are shipped-only features.

| Keys | Values | Reading |
|---|---|---|
| `sticky_bullet_catchup_rate` `_inaccuracy` `_forget_interval` `_dist_factor` | 5.0 / 1.0 / 1.5 / 0.0 | **The player's gun aim assist — decoded from `crimson.exe`, [`org/aim-assist.md`](../org/aim-assist.md).** ⚠ **Nothing steers a round in flight**; the assist rotates the *firing vector* at spawn. Per muzzle, the engine picks the most gun-axis-aligned enemy whose constant-velocity intercept is inside `RANGE` and inside an assist cone, slerps a plane-local gun line toward that intercept at `catchup_rate` per second, and scatters the result inside a cone of `inaccuracy` **degrees** (× π/180 at parse). `forget_interval` unwinds the gun line to centre that many seconds after the barrel **last fired** (not after a lock is lost). `dist_factor` is a per-metre penalty in target *selection*, not range scaling, and 0 disables it — the executable's own default is `2.5e-4`. AI gunnery does **not** use this path. |
| `warning_shot_max` `_dissipation` `_interval` `_sound` | 2.0 / 2.0 / 1.0 / `bullet_warning_sg` | **Near-miss feedback — implemented** (`WarningShotCue`, 2026-08-02). A counter of rounds passing close by, capped at `max`, decaying at `dissipation` per second, with the sound group re-triggering no faster than `interval`. **The units are not in the data**: the remake accrues 1.0 per pass, which makes `interval` the term a pilot hears. **Nor is the trigger distance** — nothing here says how close is close, and the sound def's `RANGE [20,200]` is the 3D falloff window, not a radius. Pairs with `bullet_hit_sound`, still unbuildable (nothing can strike an aircraft). |
| `smokescreen_stun_range` `_angle` `_interval` | 600 m / 170° / 5.0 s | **The smokescreen weapon's blind effect** — who it stuns: within 600 m, inside a 170° arc, re-evaluated every 5 s. Matches the design's stun-recovery pilot skill and the flare/sonic-rocket stun. |

Also worth naming, all data-confirmed: `crash` (`armor_damage_range`, `health_damage_range`,
`bounce_factor` — see [the hp pair](#the-hp-pair-armor--hit-points));
`groundblow_elev 400` / `groundblow_mag 10` / `ai_groundblow 0.5` (ground blow — the design's
§4.1.7 proximity repulsion from large objects, decoded and implemented, see
[`org/flightModel.md`](../org/flightModel.md)'s "Ground blow". ⚠ `groundblow_elev` is a ray LENGTH
in **metres**, not a trigger range, and the design's named emitter list — ground, cliff walls,
zeppelins — is the outcome of a rule that never tests vehicle type, not the rule itself.
`ai_groundblow` scales a *different* law on the AI path, so it is not the player term's magnitude);
`autohead_turn_time`/`_max`/`_min_pitch` (the padlock/look camera's head-turn rate limits — see
the [command inventory](strings.md#the-bindable-command-table-messagesjson)); `rogue` (three
`[fameThreshold, soundName]` steps warning a player who is shooting allies);
`respawn_rad`/`respawn_el` (multiplayer respawn ring); `score_kill`/`_zep`/`_suicide`/
`_return_flag`/`_enemy_flag` (multiplayer scoring); `min_ai_active_dist` 2000 m — the AI
activation radius, and the fallback for every roster whose own volume fields are unauthored (all of
them).

**`ai_skill_parameters` is now decoded** — one `[value@skill1, value@skill9]` pair per pilot stat,
the endpoints a 1–9 rating interpolates between, covering aiming cone, shot-angle cone, break-off
chance, stun duration, bail-out chance and more. Full table, and the roster slots that index it, in
[ai-rosters.md](ai-rosters.md#ai_skill_parameters--what-a-19-rating-actually-means). It settles
that the pilot-skill scale is **1–9 and nothing else**.
