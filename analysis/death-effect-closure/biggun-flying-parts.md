# `BL-061` item 3 disproven — `biggun_flying_parts` has no `fly_trail*`/puffer connection

**Question (M3 polish-5 B5).** Does `biggun_flying_parts` build no puffer in `--effects-test`
because its `spurtpuffer1..5` ride `fly_trail1..5` sub-trail roots that are missing from
`EffectStageRoots`?

Run: `python analysis/death-effect-closure/biggun_probe.py`

## Answer: no. The named mechanism does not exist in the data.

`biggun_flying_parts` (`extracted/zrdr/zep_nbgun_pieces.zrd.json`) has a single-event body: one
`CALL_ANIMATION dblcannon_flying_parts AT_NODE zep_ng_dstry1.flt`. `zep_ng_dstry1.flt` is **already**
in `EffectStageRoots` (`WorldEffectsFactory.cs:85`), and a live `--effects-test --debug-anim` run
confirms the retarget resolves cleanly — `anim: retarget 'dblcannon_flying_parts' onto
'zep_ng_dstry1.flt' (zep_ng_dstry1.flt) [caller biggun_flying_parts]`, no `(UNRESOLVED)` tag.

`dblcannon_flying_parts` (`extracted/zrdr/zep_dblcan_pieces.zrd.json`) is 8 `OBJECT_MOTION` +
`OBJECT_ACTIVE_STATE` pairs on `part1`..`part8` (real mesh nodes, `model_index` set, non-zero
`model_bbox`) — **zero `PUFFER_STATE` events**. There is nothing named `spurtpuffer1..5` or
`fly_trail1..5` anywhere in either file, or in any file reachable from `biggun_flying_parts`'s
CALL_ANIMATION closure.

`fly_trail1..5` (4 occurrences per chapter, never a parentless root — always a child) is real, but
belongs to an unrelated effect family entirely: `call_trails_up`/`ap_trails` (`ap_effects.zrd.json`),
`call_hetrails_up`/`he_trails` (`he_effects.zrd.json`), `call_pd_trails`/`pd_trails` and
`call_crash_trails`/`carnage_trails` (`player_plane_destruct.zrd.json`), `call_car_trails`/
`carnage_trails` (`torpedo_effects.zrd.json`) — rocket-impact and player-crash trails, not zeppelin
debris. `ap_trails`/`he_trails`/`carnage_trails` are already staged; the one gap in that family,
`pd_trails`, is scoped to the player crash rig (`BuildFlightCrashRuntime`'s own `EffectTemplateRoots`
build), not the world-effects runtime `BL-061` item 3 is about.

## Why "started, built no puffer" is the correct, not a broken, result

`--effects-test` (seeded, C4) reports `biggun_flying_parts` in the same bucket as `flash_effect`/
`rear_flash_effect`: **"started, built no puffer (light/model/container effect)"**. That bucket is
exactly right here — `dblcannon_flying_parts` has no particle state to build. The zeppelin
"flying parts" effect is debris chunks (`part1`-`part8` meshes flung by `OBJECT_MOTION`), not smoke;
`BL-061` item 3's own goal statement ("so a zeppelin kill smokes") was the wrong expectation from the
start — there is no smoke effect bound to this call anywhere in the data.

## A genuinely separate observation (not this item, not chased further)

`dblcannon_flying_parts`'s 8 `OBJECT_MOTION` events carry `TRANSLATION_RANGE_MIN`/`MAX` and
`FORWARD_ROTATION` (the ballistic-debris shape `AnimRuntime.cs:1890-1932` recognizes) but **no
`RUN_TIME`** key at all (`grep -c RUN_TIME` on both `.zrd.json` files: 0). `AnimRuntime`'s
`ballTime = ev.Data.Num("run_time") ?? 0f` therefore reads 0, which takes the `ballTime <= 0f` branch
(`motion.Seek(0f)` — pose at the launch start, no live motion ever added) rather than launching a
tumble. Whether that is a faithful "these particular debris pieces never actually launch" reading or
a decode gap specific to this def is an open question or a possible new backlog lead — **out of
scope for `BL-061` item 3**, which was about the puffer/anchor claim, and not chased further here per
the "confirm the trace, don't extend it" instruction.

No game data is stored here — the script reads the player's own `extracted/` tree.
