# Player global blocks

Part of: [vehicle definitions](../vehicle.md).

## `player.json`, the player-global blocks

One shared reader, flat alternating `KEY, [values…]`, ~45 top-level keys. Flight globals
(`nom_gravity`, `maxAOA`, `liftAOAs`, `highGs`/`lowGs`, the `yaw_*`/`turn_*` fade curves,
`stall_mag`, `drag_factor`) feed `PlaneStats`; the sound curve blocks (`engine_sound`,
`prop_sound`, `rattle`) are in [sounds.md](../sounds.md). Several whole subsystems in it are
**undocumented elsewhere**, and they stand on different evidence: the aim assist and the autohead
turn lead are each **decoded from the executable** ([`org/aim-assist.md`](../../org/aim-assist.md);
autohead below, `FUN_0042d010`'s idle tail block); the smokescreen stun and the near-miss counter
have data-confirmed key names and values with meanings **inferred** from those names, the near-miss
counter implemented on that inferred reading. None appears in the original design document, so they
are shipped-only features.

| Keys | Values | Reading |
|---|---|---|
| `sticky_bullet_catchup_rate` `_inaccuracy` `_forget_interval` `_dist_factor` | 5.0 / 1.0 / 1.5 / 0.0 | **The player's gun aim assist, decoded from `crimson.exe`, [`org/aim-assist.md`](../../org/aim-assist.md).** ⚠ **Nothing steers a round in flight**; the assist rotates the *firing vector* at spawn. Per muzzle, the engine picks the most gun-axis-aligned enemy whose constant-velocity intercept is inside `RANGE` and inside an assist cone, slerps a plane-local gun line toward that intercept at `catchup_rate` per second, and scatters the result inside a cone of `inaccuracy` **degrees** (× π/180 at parse). `forget_interval` unwinds the gun line to centre that many seconds after the barrel **last fired** (not after a lock is lost). `dist_factor` is a per-metre penalty in target *selection*, not range scaling, and 0 disables it, the executable's own default is `2.5e-4`. AI gunnery does **not** use this path. |
| `warning_shot_max` `_dissipation` `_interval` `_sound` | 2.0 / 2.0 / 1.0 / `bullet_warning_sg` | **Near-miss feedback, implemented** (`WarningShotCue`). A counter of rounds passing close by, capped at `max`, decaying at `dissipation` per second, with the sound group re-triggering no faster than `interval`. **The units are not in the data**: the remake accrues 1.0 per pass, which makes `interval` the term a pilot hears. **Nor is the trigger distance**, nothing here says how close is close, and the sound def's `RANGE [20,200]` is the 3D falloff window, not a radius. Pairs with `bullet_hit_sound`, still unbuildable (nothing can strike an aircraft). |
| `smokescreen_stun_range` `_angle` `_interval` | 600 m / 170° / 5.0 s | **The smokescreen weapon's blind effect**, who it stuns: within 600 m, inside a 170° arc, re-evaluated every 5 s. Matches the design's stun-recovery pilot skill and the flare/sonic-rocket stun. |

| `autohead_turn_time` `_turn_max` `_turn_min_pitch` | 0.75 / 2.86° / −3.0° | **The Cockpit head's idle turn lead, decoded and implemented** (`HeadLook.AutoheadTarget`; the decode with addresses is [`org/cameraViews.md`](../../org/cameraViews.md), "Autohead"). With no look input and the pilot in Cockpit (never Nose), the head aims where the nose will point `turn_time` seconds on at the present angular rate, so it leads into a turn and returns to centre as the rates die. The plane's **angular** velocity (its half-angle rate, `+0x16c`), rotated into the body frame, is scaled by `turn_time` (seconds), capped in magnitude at `turn_max` (half-angle radians), and turned into a rotation by the plant's own exp map, which turns the forward vector by twice the capped magnitude; elevation and azimuth are that vector's padlock angles, elevation floored at `turn_min_pitch`, below `HeadLook`'s input-path floor (level) on purpose. ⚠ **Linear velocity plays no part**: aiming along it holds the head outside a coordinated turn, since the velocity lags the nose. ⚠ **The loader doubles `turn_max` but not its own compiled default**: the authored degrees convert ×π/180 THEN ×2 (2.86° → 0.0998 stored, a lead of at most 0.1996 rad = 11.4°); the compiled fallback (0.1) is not doubled again (`PlaneStats.Load`). `turn_min_pitch` converts once, no doubling (−3° → −0.0524 rad), and its compiled fallback equals that same value. The toggle is `headLook.autohead` in `Config`, default OFF, and the Game Options Auto Head Turn row, mirroring the original's option byte (`DAT_0071dacc`); no decoded evidence pins that byte's default state. |

Also worth naming, all data-confirmed: `crash` (`armor_damage_range`, `health_damage_range`,
`bounce_factor`, see [the hp pair](../vehicle.md#armor-and-hit-points));
`groundblow_elev 400` / `groundblow_mag 10` / `ai_groundblow 0.5` (ground blow, the design's
§4.1.7 proximity repulsion from large objects, decoded and implemented, see
[`org/flightModel.md`](../../org/flightModel.md)'s "Ground blow". ⚠ `groundblow_elev` is a ray LENGTH
in **metres**, not a trigger range, and the design's named emitter list, ground, cliff walls,
zeppelins, is the outcome of a rule that never tests vehicle type, not the rule itself.
`ai_groundblow` scales a *different* law on the AI path, so it is not the player term's magnitude);
`rogue` (three `[fameThreshold, soundName]` steps warning a player who is shooting allies);
`respawn_rad`/`respawn_el` (multiplayer respawn ring); `score_kill`/`_zep`/`_suicide`/
`_return_flag`/`_enemy_flag` (multiplayer scoring); `min_ai_active_dist` 2000 m, the AI
activation radius, and the fallback for every roster whose own volume fields are unauthored (all of
them).

**`ai_skill_parameters` is now decoded**, one `[value@skill1, value@skill9]` pair per pilot stat,
the endpoints a 1–9 rating interpolates between, covering aiming cone, shot-angle cone, break-off
chance, stun duration, bail-out chance and more. Full table, and the roster slots that index it, in
[ai-rosters.md](../ai-rosters.md#ai-skill-parameters). It settles
that the pilot-skill scale is **1–9 and nothing else**.
